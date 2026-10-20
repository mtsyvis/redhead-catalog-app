using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Columns = Redhead.SitesCatalog.Domain.Constants.ImportConstants.WebmasterOffersImportColumns;

namespace Redhead.SitesCatalog.Application.Exports;

public static class WebmasterOffersExcelExportGenerator
{
    public static MemoryStream Generate(WebmasterOffersMultiSearchResult<WebmasterOfferDto> result)
    {
        var columns = ImportConstants.WebmasterOffersImportColumnsInOrder.ToList();
        columns.Insert(columns.IndexOf(Columns.LinkbuilderMailboxRawText) + 1, "LinkbuilderMailboxParsed");
        columns.Add("PrimaryEmail");
        var headers = columns.Select(column => column.Replace("RawText", string.Empty, StringComparison.Ordinal)).ToArray();
        var widths = columns.Select((column, index) => Math.Max(
            column.EndsWith("Usd", StringComparison.Ordinal) ? 16d : 32d,
            headers[index].Length + 4d)).ToArray();
        var rows = result.Results.SelectMany(group => group.Offers.Select(offer =>
        {
            var cells = columns.Select(column => CreateCell(column, group.Domain, offer));
            return (IReadOnlyList<XlsxCell>)(offer.Status == SiteWebmasterOfferStatus.Inactive
                ? cells.Select(cell => cell.WithUnavailableRowStyle()).ToList()
                : cells.ToList());
        })).ToList();

        return XlsxWorkbookWriter.CreateWorkbook([
            new XlsxSheet("Webmaster Offers", headers, rows, widths, WrapText: true, DataRowHeight: 75)
        ]);
    }

    private static XlsxCell CreateCell(string column, string domain, WebmasterOfferDto offer)
        => column switch
        {
            Columns.Domain => XlsxCell.Text(domain),
            "PrimaryEmail" => XlsxCell.Text(offer.PrimaryEmail),
            Columns.ContactRawText => XlsxCell.Text(offer.ContactRawText),
            Columns.OutreachSenderRawText => XlsxCell.Text(offer.OutreachSenderRawText),
            Columns.LinkbuilderMailboxRawText => XlsxCell.Text(offer.LinkbuilderMailboxRawText),
            "LinkbuilderMailboxParsed" => XlsxCell.Text(string.Join('\n', offer.LinkbuilderMailboxes.Select(mailbox => mailbox.Email))),
            Columns.LinkPolicyText => XlsxCell.Text(offer.LinkPolicyText),
            Columns.DfLinksRawText => XlsxCell.Text(offer.DfLinksRawText),
            Columns.SponsoredTagRawText => XlsxCell.Text(offer.SponsoredTagRawText),
            Columns.CommentText => XlsxCell.Text(offer.CommentText),
            Columns.ClientRawText => XlsxCell.Text(offer.ClientRawText),
            Columns.Term => XlsxCell.Text(offer.TermLabel),
            _ => CreatePriceCell(column, offer.Prices)
        };

    private static XlsxCell CreatePriceCell(string column, IReadOnlyList<WebmasterOfferPriceDto> prices)
    {
        var isAmount = column.EndsWith("WebmasterPriceUsd", StringComparison.Ordinal);
        var suffix = isAmount ? "WebmasterPriceUsd" : "WebmasterPriceDetails";
        var type = Enum.Parse<WebmasterOfferPriceType>(column[..^suffix.Length]);
        var price = prices.FirstOrDefault(item => item.PriceType == type);
        if (price is null)
        {
            return XlsxCell.Blank();
        }
        if (!isAmount)
        {
            return XlsxCell.Text(price.WebmasterPriceDetails);
        }
        if (price.WebmasterPriceUsd.HasValue)
        {
            return XlsxCell.Number(price.WebmasterPriceUsd, XlsxCellStyle.Decimal);
        }

        return XlsxCell.Text(price.AvailabilityStatus switch
        {
            ServiceAvailabilityStatus.AvailableWithUnknownPrice => "YES",
            ServiceAvailabilityStatus.NotAvailable => "NO",
            _ => null
        });
    }
}
