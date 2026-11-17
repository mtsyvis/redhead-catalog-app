using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

internal static class BulkOfferPriceReports
{
    internal static BulkOfferPriceReport Update(BulkOfferPriceOperationSnapshot snapshot)
    {
        var result = snapshot.Operation.Result;
        var offers = snapshot.Offers.ToDictionary(offer => offer.OfferId);
        var changes = new List<IReadOnlyList<XlsxCell>>();
        foreach (var row in result.Rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.Ready))
        {
            var saved = offers[row.OfferId!.Value];
            var before = saved.Before.ToDictionary(price => price.PriceType);
            foreach (var after in saved.After)
            {
                before.TryGetValue(after.PriceType, out var old);
                if (old == after && row.CurrentStatus == row.ProposedStatus)
                {
                    continue;
                }
                var oldTerm = old is null ? row.CurrentTerm : Term(old.Term);
                var termChanged = oldTerm != Term(after.Term);
                var notes = new List<string>();
                if (termChanged) notes.Add("Term corrected");
                if (row.CurrentStatus != row.ProposedStatus) notes.Add("Offer reactivated");
                if (old?.Value != after.Value && old?.Value.WebmasterPriceUsd == after.Value.WebmasterPriceUsd
                    && old?.Value.AvailabilityStatus == after.Value.AvailabilityStatus) notes.Add("Source quote changed");
                changes.Add(new XlsxCell[] { Text(row.Domain), Text(after.PriceType.ToString()),
                    Text(termChanged ? $"{oldTerm} → {Term(after.Term)}" : Term(after.Term)),
                    Quote(old?.Value), Quote(after.Value), Text(string.Join("; ", notes)) });
            }
        }
        var sheets = new List<XlsxSheet> { WithReportHeader(
            Sheet("Changes", ["Domain", "Service", "Term", "Before USD", "After USD", "Note"], changes),
            snapshot, "Price list update") };
        var unchanged = result.Rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.NoChanges)
            .Select(row => (IReadOnlyList<XlsxCell>)new[] { Text(row.Domain) }).ToArray();
        if (unchanged.Length > 0) sheets.Add(Sheet("No changes", ["Domain"], unchanged));
        var exceptions = result.Rows.Where(row => row.Outcome == BulkOfferPricePreviewOutcomes.Exceptions)
            .SelectMany(row => row.Inputs.Select(input => (IReadOnlyList<XlsxCell>)new[] {
                XlsxCell.Number(input.RowNumber, XlsxCellStyle.Integer), Text(input.Domain) }
                .Concat(snapshot.Operation.PriceTypes.Select((_, index) => Text(input.Values.ElementAtOrDefault(index))))
                .Append(Text(string.Join("\n", row.Reasons))).ToArray())).ToArray();
        if (exceptions.Length > 0) sheets.Add(Sheet("Exceptions", new[] { "Source row", "Domain" }
            .Concat(snapshot.Operation.PriceTypes.Select(type => type.ToString())).Append("Reason").ToArray(), exceptions));
        using var workbook = XlsxWorkbookWriter.CreateWorkbook(sheets);
        return new(workbook.ToArray(), sheets.Sum(sheet => sheet.Rows.Count));

    }

    internal static BulkOfferPriceReport Calculation(BulkOfferPriceOperationSnapshot snapshot)
    {
        var changed = snapshot.Minima.Where(key => key.BeforeUsd != key.AfterUsd).ToArray();
        if (changed.Length == 0)
        {
            throw new RequestValidationException("No purchase minima changed; there is no catalog calculation data to download.");
        }
        var services = Enum.GetValues<PriceType>().Where(service => changed.Any(key => key.Service == service && key.AfterUsd.HasValue)).ToArray();
        var numeric = changed.Where(key => key.AfterUsd.HasValue).GroupBy(key => (key.Domain, key.Term))
            .OrderBy(group => group.Key.Domain, StringComparer.Ordinal).ThenBy(group => group.Key.Term.TermKey, StringComparer.Ordinal)
            .Select(group => (IReadOnlyList<XlsxCell>)new[] { Text(group.Key.Domain), Text(Term(group.Key.Term)) }
                .Concat(services.Select(service => Money(group.SingleOrDefault(key => key.Service == service)?.AfterUsd))).ToArray()).ToArray();
        var rowsByDomain = snapshot.Operation.Result.Rows.ToDictionary(row => row.Domain, StringComparer.Ordinal);
        var missing = changed.Where(key => key.AfterUsd is null).OrderBy(key => key.Domain, StringComparer.Ordinal)
            .ThenBy(key => key.Term.TermKey, StringComparer.Ordinal).ThenBy(key => key.Service)
            .Select(key => (IReadOnlyList<XlsxCell>)new[] { Text(key.Domain), Text(Term(key.Term)), Text(Service(key.Service)),
                Text(MissingReason(rowsByDomain[key.Domain], key)) }).ToArray();
        var sheets = new List<XlsxSheet>();
        // When all purchase prices disappeared, open directly on the actionable review list.
        if (numeric.Length > 0)
        {
            sheets.Add(WithReportHeader(Sheet("Webmaster costs", new[] { "Domain", "Term" }
                .Concat(services.Select(service => Service(service) + " cost USD")).ToArray(), numeric), snapshot, "Catalog calculation",
                "Filled cell: use this webmaster cost to calculate and update the catalog price for this service and term.\nBlank cell: keep the current catalog price unchanged."));
        }
        if (missing.Length > 0)
        {
            var sheet = Sheet("Needs review", ["Domain", "Term", "Service", "Reason"], missing);
            sheets.Add(sheets.Count == 0 ? WithReportHeader(sheet, snapshot, "Catalog calculation",
                "Review these catalog prices manually. Do not automatically set them to zero or NO.") : sheet);
        }
        using var workbook = XlsxWorkbookWriter.CreateWorkbook(sheets);
        return new(workbook.ToArray(), sheets.Sum(sheet => sheet.Rows.Count));
    }

    private static string MissingReason(BulkOfferPricePreviewRow row, BulkOfferPurchaseMinimum key)
    {
        return row.CurrentTerm != row.ProposedTerm && Term(key.Term) == row.CurrentTerm ? $"Term corrected to {row.ProposedTerm}; no webmaster price remains for {Term(key.Term)}."
            : "No webmaster price is available for this service and term. Review the catalog price manually.";
    }
    private static XlsxSheet WithReportHeader(XlsxSheet sheet, BulkOfferPriceOperationSnapshot snapshot,
        string title, string? note = null)
    {
        List<IReadOnlyList<XlsxCell>> rows =
        [
            [Text(title) with { Style = XlsxCellStyle.ReportTitle }],
            [Text($"Contact: {snapshot.Operation.Result.PrimaryEmail} · Term: {snapshot.Operation.Result.Term}") with { Style = XlsxCellStyle.ReportContext }],
            [Text($"Updated: {snapshot.Operation.AppliedAtUtc.ToString("dd MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC · By: {snapshot.Operation.Actor}") with { Style = XlsxCellStyle.ReportContext }]
        ];
        if (note is not null)
        {
            rows.Add([Text(note) with { Style = XlsxCellStyle.ReportContext }]);
        }
        rows.Add([]);
        return sheet with { IntroRows = rows, FullWidthIntro = true };
    }

    private static XlsxSheet Sheet(string name, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<XlsxCell>> rows)
        => new(name, headers, rows, headers.Select(header => header == "Domain" ? 32d : header == "Reason" || header == "Note" ? 65d : 24d).ToArray(), WrapText: true);
    private static XlsxCell Quote(BulkOfferPriceValue? value) => value?.AvailabilityStatus switch {
        ServiceAvailabilityStatus.Available => Money(value.WebmasterPriceUsd),
        ServiceAvailabilityStatus.NotAvailable => Text("NO"),
        ServiceAvailabilityStatus.AvailableWithUnknownPrice => Text("YES"), _ => Text("—") };
    private static XlsxCell Text(string? value) => XlsxCell.Text(value);
    private static XlsxCell Money(decimal? value) => XlsxCell.Number(value, XlsxCellStyle.Decimal);
    private static string Term(PricingTerm term) => term == PricingTerm.Unknown ? "No term" : term.DisplayLabel;
    private static string Service(PriceType service) => service switch {
        PriceType.LinkInsertion => "Link insertion", PriceType.LinkInsertionCasino => "Link insertion casino", _ => service.ToString() };
}
