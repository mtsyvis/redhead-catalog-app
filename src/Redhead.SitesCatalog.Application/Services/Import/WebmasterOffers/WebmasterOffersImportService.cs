using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services.Import.Artifacts;
using Redhead.SitesCatalog.Application.Services.Import.Common;
using Redhead.SitesCatalog.Application.Services.Import.Csv;
using Redhead.SitesCatalog.Application.Services.Import.ValueParsers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.Import.WebmasterOffers;

public sealed class WebmasterOffersImportService : IWebmasterOffersImportService
{
    private const int BatchSize = 1000;

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly string[] PrimaryEmailMarkerTexts =
    [
        "ответили здесь",
        "отвечают здесь",
        "ответили тут",
        "отвечают тут",
        "ответил тут",
        "ответ здесь",
        "ответ тут",
        "ответы тут",
        "отвечает тут",
        "ответ туту",
        "ответт тут",
        "ответ тту",
        "ответ утт",
        "отв тут",
        "otv tut",
        "писать сюда",
        "написать сюда",
        "написала сюда",
        "направили сюда",
        "write here",
        "ответ",
        "answer",
        "reply",
        "отв",
        "otv",
        "сюда",
        "здесь",
        "тут",
        "here"
    ];

    private static readonly Regex PrimaryEmailMarkerRegex = new(
        BuildPrimaryEmailMarkerPattern(),
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<PriceColumnPair> PriceColumns =
    [
        new(WebmasterOfferPriceType.Main, ImportConstants.WebmasterOffersImportColumns.MainWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.MainWebmasterPriceDetails),
        new(WebmasterOfferPriceType.Casino, ImportConstants.WebmasterOffersImportColumns.CasinoWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.CasinoWebmasterPriceDetails),
        new(WebmasterOfferPriceType.Crypto, ImportConstants.WebmasterOffersImportColumns.CryptoWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.CryptoWebmasterPriceDetails),
        new(WebmasterOfferPriceType.Dating, ImportConstants.WebmasterOffersImportColumns.DatingWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.DatingWebmasterPriceDetails),
        new(WebmasterOfferPriceType.LinkInsertion, ImportConstants.WebmasterOffersImportColumns.LinkInsertionWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.LinkInsertionWebmasterPriceDetails),
        new(WebmasterOfferPriceType.LinkInsertion18Plus, ImportConstants.WebmasterOffersImportColumns.LinkInsertion18PlusWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.LinkInsertion18PlusWebmasterPriceDetails),
        new(WebmasterOfferPriceType.Banner, ImportConstants.WebmasterOffersImportColumns.BannerWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.BannerWebmasterPriceDetails),
        new(WebmasterOfferPriceType.Banner18Plus, ImportConstants.WebmasterOffersImportColumns.Banner18PlusWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.Banner18PlusWebmasterPriceDetails),
        new(WebmasterOfferPriceType.HomepageTextLink, ImportConstants.WebmasterOffersImportColumns.HomepageTextLinkWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.HomepageTextLinkWebmasterPriceDetails),
        new(WebmasterOfferPriceType.HomepageTextLink18Plus, ImportConstants.WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceUsd, ImportConstants.WebmasterOffersImportColumns.HomepageTextLink18PlusWebmasterPriceDetails)
    ];

    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebmasterOffersImportService> _logger;
    private readonly IImportArtifactStorageService _importArtifactStorageService;

    public WebmasterOffersImportService(
        ApplicationDbContext context,
        ILogger<WebmasterOffersImportService> logger,
        IImportArtifactStorageService importArtifactStorageService)
    {
        _context = context;
        _logger = logger;
        _importArtifactStorageService = importArtifactStorageService;
    }

    public async Task<WebmasterOffersImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Webmaster offers import started. FileName={FileName}, UserId={UserId}, UserEmail={UserEmail}",
            fileName,
            userId,
            userEmail);

        var result = new WebmasterOffersImportResult();
        var invalidRowsPayload = new InvalidRowsImportArtifactPayload();
        var unmatchedRowsPayload = new UnmatchedRowsImportArtifactPayload();
        var warningRowsPayload = new WarningRowsImportArtifactPayload();
        var validRows = new List<WebmasterOfferImportRow>();
        var invalidRowsCount = 0;

        await using (var session = await CsvImportSession.OpenAsync(
                         fileStream,
                         expectedHeaderColumnsForDelimiterDetection: ImportConstants.WebmasterOffersImportColumnsInOrder,
                         validateHeader: ValidateHeaderExactOrThrow,
                         ct: cancellationToken))
        {
            invalidRowsPayload.Headers = session.Header.ToArray();
            unmatchedRowsPayload.Headers = session.Header.ToArray();

            await foreach (var (row, rawValues) in ReadRowsAsync(session.Csv, session.Header.Length, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (row.IsEmpty)
                {
                    continue;
                }

                var validationError = ValidateRow(row);
                if (validationError is not null)
                {
                    invalidRowsCount++;
                    ImportRowTrackingHelper.AddInvalidRow(invalidRowsPayload, row.RowNumber, rawValues, validationError);
                    continue;
                }

                row.RawValues = rawValues;
                validRows.Add(row);
            }
        }

        if (validRows.Count == 0)
        {
            AddImportLog(0, 0, invalidRowsCount, 0, userId, userEmail);
            await _context.SaveChangesAsync(cancellationToken);
            AttachSummaryAndDownloads(result, invalidRowsPayload, unmatchedRowsPayload, warningRowsPayload, invalidRowsCount);
            return result;
        }

        var domains = validRows
            .Select(row => row.NormalizedDomain)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existingDomains = await LoadExistingDomainsAsync(domains, cancellationToken);
        var existingFingerprints = await LoadExistingFingerprintsAsync(
            validRows
                .Select(row => row.ImportFingerprint)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            cancellationToken);
        var webmastersByNormalizedContact = await LoadExistingWebmastersAsync(validRows, cancellationToken);
        var mailboxMatcher = await LoadMailboxMatcherAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var importedFingerprints = new HashSet<string>(StringComparer.Ordinal);

        var previousAutoDetectChanges = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var pendingOffersCount = 0;

            foreach (var row in validRows)
            {
                if (!existingDomains.Contains(row.NormalizedDomain))
                {
                    unmatchedRowsPayload.Rows.Add(new UnmatchedImportRowRecord
                    {
                        SourceRowNumber = row.RowNumber,
                        RawValues = row.RawValues.ToList()
                    });
                    continue;
                }

                if (existingFingerprints.Contains(row.ImportFingerprint)
                    || !importedFingerprints.Add(row.ImportFingerprint))
                {
                    result.SkippedDuplicateCount++;
                    continue;
                }

                var webmasterId = GetOrCreateWebmasterId(webmastersByNormalizedContact, row, now);
                var offer = BuildOffer(row, webmasterId, now);
                foreach (var warning in AddMailboxLinks(offer, row, mailboxMatcher, now))
                {
                    warningRowsPayload.Rows.Add(warning);
                }

                _context.SiteWebmasterOffers.Add(offer);
                pendingOffersCount++;
                result.ImportedCount++;

                if (pendingOffersCount >= BatchSize)
                {
                    await SaveCurrentBatchAsync(cancellationToken);
                    pendingOffersCount = 0;
                }
            }

            if (pendingOffersCount > 0)
            {
                await SaveCurrentBatchAsync(cancellationToken);
            }

            AddImportLog(
                result.ImportedCount,
                unmatchedRowsPayload.Rows.Count,
                invalidRowsCount,
                result.SkippedDuplicateCount,
                userId,
                userEmail);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = previousAutoDetectChanges;
        }

        AttachSummaryAndDownloads(result, invalidRowsPayload, unmatchedRowsPayload, warningRowsPayload, invalidRowsCount);

        _logger.LogInformation(
            "Webmaster offers import completed. Imported={Imported}, SkippedDuplicates={SkippedDuplicates}, Unmatched={Unmatched}, Invalid={Invalid}, Warnings={Warnings}, UserId={UserId}",
            result.ImportedCount,
            result.SkippedDuplicateCount,
            result.UnmatchedRowsCount,
            result.InvalidRowsCount,
            result.SavedWithWarningsCount,
            userId);

        return result;
    }

    private async Task<HashSet<string>> LoadExistingFingerprintsAsync(
        List<string> fingerprints,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in ImportBatchingHelper.Chunk(fingerprints, BatchSize))
        {
            var existing = await _context.SiteWebmasterOffers
                .AsNoTracking()
                .Where(offer => chunk.Contains(offer.ImportFingerprint))
                .Select(offer => offer.ImportFingerprint)
                .ToListAsync(cancellationToken);

            foreach (var fingerprint in existing)
            {
                result.Add(fingerprint);
            }
        }

        return result;
    }

    private async Task<HashSet<string>> LoadExistingDomainsAsync(List<string> domains, CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in ImportBatchingHelper.Chunk(domains, BatchSize))
        {
            var existing = await _context.Sites
                .AsNoTracking()
                .Where(site => chunk.Contains(site.Domain))
                .Select(site => site.Domain)
                .ToListAsync(cancellationToken);

            foreach (var domain in existing)
            {
                result.Add(domain);
            }
        }

        return result;
    }

    private async Task<Dictionary<string, Guid>> LoadExistingWebmastersAsync(
        IReadOnlyCollection<WebmasterOfferImportRow> rows,
        CancellationToken cancellationToken)
    {
        var normalizedContacts = rows
            .Select(row => row.NormalizedContactRawText)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var chunk in ImportBatchingHelper.Chunk(normalizedContacts, BatchSize))
        {
            var webmasters = await _context.Webmasters
                .AsNoTracking()
                .Where(webmaster => chunk.Contains(webmaster.NormalizedContactRawText))
                .Select(webmaster => new
                {
                    webmaster.Id,
                    webmaster.NormalizedContactRawText
                })
                .ToListAsync(cancellationToken);

            foreach (var webmaster in webmasters)
            {
                result[webmaster.NormalizedContactRawText] = webmaster.Id;
            }
        }

        return result;
    }

    private async Task<MailboxMatcher> LoadMailboxMatcherAsync(CancellationToken cancellationToken)
    {
        var mailboxes = await _context.LinkbuilderMailboxes
            .AsNoTracking()
            .Where(mailbox => mailbox.IsActive)
            .ToListAsync(cancellationToken);

        return new MailboxMatcher(mailboxes);
    }

    private Guid GetOrCreateWebmasterId(
        IDictionary<string, Guid> webmastersByNormalizedContact,
        WebmasterOfferImportRow row,
        DateTime now)
    {
        if (webmastersByNormalizedContact.TryGetValue(row.NormalizedContactRawText, out var webmasterId))
        {
            return webmasterId;
        }

        webmasterId = Guid.NewGuid();
        var webmaster = new Webmaster
        {
            Id = webmasterId,
            ContactRawText = row.ContactRawText,
            NormalizedContactRawText = row.NormalizedContactRawText,
            PrimaryEmail = ExtractPrimaryEmail(row.ContactRawText),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        webmastersByNormalizedContact[row.NormalizedContactRawText] = webmasterId;
        _context.Webmasters.Add(webmaster);
        return webmasterId;
    }

    private static SiteWebmasterOffer BuildOffer(WebmasterOfferImportRow row, Guid webmasterId, DateTime now)
    {
        var offer = new SiteWebmasterOffer
        {
            Id = Guid.NewGuid(),
            SiteDomain = row.NormalizedDomain,
            WebmasterId = webmasterId,
            ImportFingerprint = row.ImportFingerprint,
            ContactRawText = row.ContactRawText,
            OutreachSenderRawText = TrimToNull(row.OutreachSenderRawText),
            LinkbuilderMailboxRawText = TrimToNull(row.LinkbuilderMailboxRawText),
            LinkPolicyText = TrimToNull(row.LinkPolicyText),
            DfLinksRawText = TrimToNull(row.DfLinksRawText),
            SponsoredTagRawText = TrimToNull(row.SponsoredTagRawText),
            CommentText = TrimToNull(row.CommentText),
            ClientRawText = TrimToNull(row.ClientRawText),
            TermRawText = TrimToNull(row.TermRawText),
            TermType = row.Term.TermType,
            TermValue = row.Term.TermValue,
            TermUnit = row.Term.TermUnit,
            Status = SiteWebmasterOfferStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var price in row.Prices)
        {
            offer.Prices.Add(new WebmasterOfferPrice
            {
                Id = Guid.NewGuid(),
                SiteWebmasterOfferId = offer.Id,
                PriceType = price.PriceType,
                AvailabilityStatus = price.AvailabilityStatus,
                WebmasterPriceUsd = price.WebmasterPriceUsd,
                WebmasterPriceDetails = TrimToNull(price.WebmasterPriceDetails),
                TermType = row.Term.TermType,
                TermValue = row.Term.TermValue,
                TermUnit = row.Term.TermUnit,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        return offer;
    }

    private async Task SaveCurrentBatchAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private static IEnumerable<WarningImportRowRecord> AddMailboxLinks(
        SiteWebmasterOffer offer,
        WebmasterOfferImportRow row,
        MailboxMatcher matcher,
        DateTime now)
    {
        var matchedIds = new HashSet<Guid>();
        foreach (var token in SplitMailboxTokens(row.LinkbuilderMailboxRawText))
        {
            if (!matcher.TryMatch(token, out var mailbox))
            {
                yield return new WarningImportRowRecord
                {
                    Domain = row.NormalizedDomain,
                    Field = ImportConstants.WebmasterOffersImportColumns.LinkbuilderMailboxRawText,
                    RawValue = token,
                    SourceRowNumber = row.RowNumber,
                    Warning = "Linkbuilder mailbox alias could not be mapped."
                };
                continue;
            }

            if (!matchedIds.Add(mailbox.Id))
            {
                continue;
            }

            offer.LinkbuilderMailboxes.Add(new SiteWebmasterOfferLinkbuilderMailbox
            {
                SiteWebmasterOfferId = offer.Id,
                LinkbuilderMailboxId = mailbox.Id,
                Source = LinkbuilderMailboxOfferSource.Import,
                CreatedAtUtc = now
            });
        }
    }

    private static async IAsyncEnumerable<(WebmasterOfferImportRow Row, List<string> RawValues)> ReadRowsAsync(
        CsvReader csv,
        int headerCount,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var rowNumber = 1;
        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var rawRecord = csv.Parser.Record ?? Array.Empty<string>();
            var rawValues = rawRecord.ToList();
            if (rawRecord.Length != headerCount)
            {
                yield return (new WebmasterOfferImportRow
                {
                    RowNumber = rowNumber,
                    RowStructureError = $"Row has {rawRecord.Length} columns but expected {headerCount}."
                }, rawValues);
                continue;
            }

            var row = MapRow(csv, rowNumber);
            yield return (row, rawValues);
        }
    }

    private static WebmasterOfferImportRow MapRow(CsvReader csv, int rowNumber)
    {
        var contactRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.ContactRawText);
        var row = new WebmasterOfferImportRow
        {
            RowNumber = rowNumber,
            DomainRaw = Get(csv, ImportConstants.WebmasterOffersImportColumns.Domain),
            ContactRawText = contactRawText ?? string.Empty,
            NormalizedContactRawText = NormalizeContact(contactRawText),
            OutreachSenderRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.OutreachSenderRawText),
            LinkbuilderMailboxRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.LinkbuilderMailboxRawText),
            TermRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.Term),
            LinkPolicyText = Get(csv, ImportConstants.WebmasterOffersImportColumns.LinkPolicyText),
            DfLinksRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.DfLinksRawText),
            SponsoredTagRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.SponsoredTagRawText),
            CommentText = Get(csv, ImportConstants.WebmasterOffersImportColumns.CommentText),
            ClientRawText = Get(csv, ImportConstants.WebmasterOffersImportColumns.ClientRawText)
        };

        row.NormalizedDomain = DomainNormalizer.Normalize(row.DomainRaw);
        row.Term = ParseTerm(row.TermRawText);

        foreach (var priceColumn in PriceColumns)
        {
            var amountRaw = Get(csv, priceColumn.AmountHeader);
            var details = Get(csv, priceColumn.DetailsHeader);
            if (string.IsNullOrWhiteSpace(amountRaw) && string.IsNullOrWhiteSpace(details))
            {
                continue;
            }

            var parsedAmount = ParseAmount(priceColumn.PriceType, priceColumn.AmountHeader, amountRaw);
            row.Prices.Add(new WebmasterOfferPriceImportRow(
                priceColumn.PriceType,
                priceColumn.AmountHeader,
                amountRaw,
                details,
                parsedAmount.WebmasterPriceUsd,
                parsedAmount.AvailabilityStatus,
                parsedAmount.ValidationError));
        }

        row.ImportFingerprint = BuildImportFingerprint(row);
        return row;
    }

    private static string? ValidateRow(WebmasterOfferImportRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.RowStructureError))
        {
            return row.RowStructureError;
        }

        if (string.IsNullOrWhiteSpace(row.DomainRaw))
        {
            return "Domain is required.";
        }

        if (string.IsNullOrEmpty(row.NormalizedDomain))
        {
            return "Domain could not be normalized.";
        }

        foreach (var price in row.Prices)
        {
            if (price.ValidationError is not null)
            {
                return price.ValidationError;
            }
        }

        return null;
    }

    private static void ValidateHeaderExactOrThrow(string[] actualHeader)
    {
        var expected = ImportConstants.WebmasterOffersImportColumnsInOrder;
        if (actualHeader.Length != expected.Length)
        {
            throw new ImportHeaderValidationException(
                $"CSV header is invalid. Expected exactly {expected.Length} columns: {string.Join(", ", expected)}.");
        }

        for (var i = 0; i < expected.Length; i++)
        {
            var actual = CsvImportHelper.NormalizeHeader(actualHeader[i]);
            if (!string.Equals(actual, expected[i], StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportHeaderValidationException(
                    $"CSV header is invalid. Column {i + 1} must be '{expected[i]}'. Found: '{actual}'.");
            }
        }
    }

    private void AddImportLog(
        int importedCount,
        int unmatchedRowsCount,
        int invalidRowsCount,
        int duplicateRowsCount,
        string userId,
        string userEmail)
    {
        _context.ImportLogs.Add(new ImportLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserEmail = userEmail,
            Type = ImportConstants.ImportTypeWebmasterOffers,
            TimestampUtc = DateTime.UtcNow,
            Inserted = importedCount,
            Matched = importedCount,
            Unmatched = unmatchedRowsCount,
            Duplicates = duplicateRowsCount,
            ErrorsCount = invalidRowsCount
        });
    }

    private static string BuildImportFingerprint(WebmasterOfferImportRow row)
    {
        var builder = new StringBuilder();

        AppendFingerprintPart(builder, row.NormalizedDomain);
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.ContactRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.OutreachSenderRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.LinkbuilderMailboxRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.LinkPolicyText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.DfLinksRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.SponsoredTagRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.CommentText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.ClientRawText));
        AppendFingerprintPart(builder, CanonicalFingerprintText(row.TermRawText));
        AppendTermFingerprintParts(builder, row.Term);

        foreach (var price in row.Prices.OrderBy(price => price.PriceType))
        {
            AppendFingerprintPart(builder, price.PriceType.ToString());
            AppendFingerprintPart(builder, price.AvailabilityStatus.ToString());
            AppendFingerprintPart(builder, CanonicalFingerprintAmount(price.WebmasterPriceUsd));
            AppendFingerprintPart(builder, CanonicalFingerprintText(price.WebmasterPriceDetails));
            AppendTermFingerprintParts(builder, row.Term);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendTermFingerprintParts(StringBuilder builder, WebmasterImportTerm term)
    {
        AppendFingerprintPart(builder, term.TermType?.ToString());
        AppendFingerprintPart(builder, term.TermValue?.ToString(CultureInfo.InvariantCulture));
        AppendFingerprintPart(builder, term.TermUnit?.ToString());
    }

    private static void AppendFingerprintPart(StringBuilder builder, string? value)
    {
        value ??= string.Empty;
        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value)
            .Append('|');
    }

    private static string CanonicalFingerprintText(string? value)
        => TrimToNull(value)?.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') ?? string.Empty;

    private static string CanonicalFingerprintAmount(decimal? value)
        => value?.ToString("0.#############################", CultureInfo.InvariantCulture) ?? string.Empty;

    private void AttachSummaryAndDownloads(
        WebmasterOffersImportResult result,
        InvalidRowsImportArtifactPayload invalidRowsPayload,
        UnmatchedRowsImportArtifactPayload unmatchedRowsPayload,
        WarningRowsImportArtifactPayload warningRowsPayload,
        int invalidRowsCount)
    {
        result.InvalidRowsCount = invalidRowsCount;
        result.UnmatchedRowsCount = unmatchedRowsPayload.Rows.Count;
        result.SavedWithWarningsCount = warningRowsPayload.Rows.Count;

        ImportDownloadItem? invalidRowsDownload = null;
        if (invalidRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreInvalidRows(ImportConstants.ImportArtifactSlugWebmasterOffers, invalidRowsPayload);
            invalidRowsDownload = new ImportDownloadItem { Available = true, Token = handle.Token, FileName = handle.FileName };
        }

        ImportDownloadItem? unmatchedRowsDownload = null;
        if (unmatchedRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreUnmatchedRows(ImportConstants.ImportArtifactSlugWebmasterOffers, unmatchedRowsPayload);
            unmatchedRowsDownload = new ImportDownloadItem { Available = true, Token = handle.Token, FileName = handle.FileName };
        }

        ImportDownloadItem? warningRowsDownload = null;
        if (warningRowsPayload.Rows.Count > 0)
        {
            var handle = _importArtifactStorageService.StoreWarningRows(ImportConstants.ImportArtifactSlugWebmasterOffers, warningRowsPayload);
            warningRowsDownload = new ImportDownloadItem { Available = true, Token = handle.Token, FileName = handle.FileName };
        }

        result.Downloads = new ImportDownloadsInfo
        {
            InvalidRows = invalidRowsDownload,
            UnmatchedRows = unmatchedRowsDownload,
            WarningRows = warningRowsDownload
        };
    }

    private static string? Get(CsvReader csv, string header)
        => csv.GetField(header)?.Trim();

    private static WebmasterPriceAmountParseResult ParseAmount(
        WebmasterOfferPriceType priceType,
        string amountHeader,
        string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return new WebmasterPriceAmountParseResult(null, ServiceAvailabilityStatus.Unknown, null);
        }

        if (DecimalParsingHelper.TryParseDecimalFlexible(trimmed, out var amount))
        {
            return amount > 0
                ? new WebmasterPriceAmountParseResult(amount, ServiceAvailabilityStatus.Available, null)
                : new WebmasterPriceAmountParseResult(null, ServiceAvailabilityStatus.Unknown, $"{amountHeader} must be greater than 0.");
        }

        if (priceType != WebmasterOfferPriceType.Main)
        {
            if (string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return new WebmasterPriceAmountParseResult(null, ServiceAvailabilityStatus.AvailableWithUnknownPrice, null);
            }

            if (string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase))
            {
                return new WebmasterPriceAmountParseResult(null, ServiceAvailabilityStatus.NotAvailable, null);
            }
        }

        return new WebmasterPriceAmountParseResult(null, ServiceAvailabilityStatus.Unknown, $"Invalid {amountHeader} value.");
    }

    private static WebmasterImportTerm ParseTerm(string? rawTerm)
    {
        var trimmed = rawTerm?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || string.Equals(trimmed, "no term", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "unknown term", StringComparison.OrdinalIgnoreCase))
        {
            return WebmasterImportTerm.Unknown;
        }

        if (string.Equals(trimmed, "permanent", StringComparison.OrdinalIgnoreCase))
        {
            return new WebmasterImportTerm(TermType.Permanent, null, null);
        }

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            && value > 0
            && (string.Equals(parts[1], "year", StringComparison.OrdinalIgnoreCase)
                || string.Equals(parts[1], "years", StringComparison.OrdinalIgnoreCase)))
        {
            return new WebmasterImportTerm(TermType.Finite, value, TermUnit.Year);
        }

        return WebmasterImportTerm.Unknown;
    }

    private static string NormalizeContact(string? contactRawText)
    {
        var trimmed = contactRawText?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(trimmed, " ").ToLowerInvariant();
    }

    private static string? ExtractPrimaryEmail(string? contactRawText)
    {
        if (string.IsNullOrWhiteSpace(contactRawText))
        {
            return null;
        }

        var markedEmail = ExtractMarkedPrimaryEmail(contactRawText);
        if (markedEmail is not null)
        {
            return markedEmail;
        }

        var matches = EmailRegex.Matches(contactRawText);
        return matches.Count == 1 ? matches[0].Value.ToLowerInvariant() : null;
    }

    private static string? ExtractMarkedPrimaryEmail(string contactRawText)
    {
        foreach (var line in contactRawText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!PrimaryEmailMarkerRegex.IsMatch(line))
            {
                continue;
            }

            var matches = EmailRegex.Matches(line);
            if (matches.Count > 0)
            {
                return matches[0].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    private static string BuildPrimaryEmailMarkerPattern()
        => $@"(?<![\p{{L}}\p{{N}}])(?:{string.Join("|", PrimaryEmailMarkerTexts
            .OrderByDescending(marker => marker.Length)
            .Select(Regex.Escape))})(?![\p{{L}}\p{{N}}])";

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IEnumerable<string> SplitMailboxTokens(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            yield break;
        }

        var tokens = rawValue.Split(['\r', '\n', '/', ';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var normalized = token.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private sealed record PriceColumnPair(WebmasterOfferPriceType PriceType, string AmountHeader, string DetailsHeader);

    private sealed record WebmasterPriceAmountParseResult(
        decimal? WebmasterPriceUsd,
        ServiceAvailabilityStatus AvailabilityStatus,
        string? ValidationError);

    private sealed record WebmasterImportTerm(TermType? TermType, int? TermValue, TermUnit? TermUnit)
    {
        public static WebmasterImportTerm Unknown { get; } = new(null, null, null);
    }

    private sealed record WebmasterOfferPriceImportRow(
        WebmasterOfferPriceType PriceType,
        string AmountHeader,
        string? AmountRaw,
        string? WebmasterPriceDetails,
        decimal? WebmasterPriceUsd,
        ServiceAvailabilityStatus AvailabilityStatus,
        string? ValidationError);

    private sealed class WebmasterOfferImportRow
    {
        public int RowNumber { get; init; }
        public string? DomainRaw { get; init; }
        public string NormalizedDomain { get; set; } = string.Empty;
        public string ContactRawText { get; init; } = string.Empty;
        public string NormalizedContactRawText { get; init; } = string.Empty;
        public string? OutreachSenderRawText { get; init; }
        public string? LinkbuilderMailboxRawText { get; init; }
        public string? TermRawText { get; init; }
        public string? LinkPolicyText { get; init; }
        public string? DfLinksRawText { get; init; }
        public string? SponsoredTagRawText { get; init; }
        public string? CommentText { get; init; }
        public string? ClientRawText { get; init; }
        public string? RowStructureError { get; init; }
        public string ImportFingerprint { get; set; } = string.Empty;
        public WebmasterImportTerm Term { get; set; } = WebmasterImportTerm.Unknown;
        public List<WebmasterOfferPriceImportRow> Prices { get; } = [];
        public IReadOnlyList<string> RawValues { get; set; } = [];

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(RowStructureError)
            && string.IsNullOrWhiteSpace(DomainRaw)
            && string.IsNullOrWhiteSpace(ContactRawText)
            && string.IsNullOrWhiteSpace(OutreachSenderRawText)
            && string.IsNullOrWhiteSpace(LinkbuilderMailboxRawText)
            && string.IsNullOrWhiteSpace(TermRawText)
            && string.IsNullOrWhiteSpace(LinkPolicyText)
            && string.IsNullOrWhiteSpace(DfLinksRawText)
            && string.IsNullOrWhiteSpace(SponsoredTagRawText)
            && string.IsNullOrWhiteSpace(CommentText)
            && string.IsNullOrWhiteSpace(ClientRawText)
            && Prices.Count == 0;
    }

    private sealed class MailboxMatcher
    {
        private readonly Dictionary<string, LinkbuilderMailbox> _mailboxesByAlias = new(StringComparer.Ordinal);

        public MailboxMatcher(IEnumerable<LinkbuilderMailbox> mailboxes)
        {
            foreach (var mailbox in mailboxes)
            {
                AddAlias(mailbox.Email, mailbox);
                foreach (var alias in mailbox.Aliases)
                {
                    AddAlias(alias, mailbox);
                }
            }
        }

        public bool TryMatch(string token, out LinkbuilderMailbox mailbox)
            => _mailboxesByAlias.TryGetValue(token.Trim().ToLowerInvariant(), out mailbox!);

        private void AddAlias(string? alias, LinkbuilderMailbox mailbox)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                return;
            }

            _mailboxesByAlias.TryAdd(alias.Trim().ToLowerInvariant(), mailbox);
        }
    }
}
