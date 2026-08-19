using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models.ChangeHistory;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Audit;

public static class EntityChangeHistoryRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyDictionary<string, string?> CaptureSite(Site site)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DR"] = Format(site.DR),
            ["Traffic"] = Format(site.Traffic),
            ["Location"] = site.Location,
            ["Language"] = site.Language,
            ["Sponsored tag"] = site.SponsoredTag,
            ["DF links"] = Format(site.NumberDFLinks),
            ["Niche"] = site.Niche,
            ["Categories"] = site.Categories,
            ["Quarantined"] = Format(site.IsQuarantined),
            ["Quarantine reason"] = site.QuarantineReason
        };

        foreach (var price in site.PriceOptions.OrderBy(price => price.PriceType).ThenBy(price => price.TermKey, StringComparer.Ordinal))
        {
            values[$"Price · {price.PriceType} · {price.TermKey}"] = Format(price.AmountUsd);
        }

        foreach (var availability in site.ServiceAvailabilities.OrderBy(availability => availability.ServiceType))
        {
            values[$"Availability · {availability.ServiceType}"] = availability.Status.ToString();
        }

        if (site.PriceOptions.Count == 0 && site.ServiceAvailabilities.Count == 0)
        {
            values["Price · Main"] = Format(site.PriceUsd);
            values["Price · Casino"] = Format(site.PriceCasino);
            values["Availability · Casino"] = site.PriceCasinoStatus.ToString();
            values["Price · Crypto"] = Format(site.PriceCrypto);
            values["Availability · Crypto"] = site.PriceCryptoStatus.ToString();
            values["Price · LinkInsertion"] = Format(site.PriceLinkInsert);
            values["Availability · LinkInsertion"] = site.PriceLinkInsertStatus.ToString();
            values["Price · LinkInsertionCasino"] = Format(site.PriceLinkInsertCasino);
            values["Availability · LinkInsertionCasino"] = site.PriceLinkInsertCasinoStatus.ToString();
            values["Price · Dating"] = Format(site.PriceDating);
            values["Availability · Dating"] = site.PriceDatingStatus.ToString();
            values["Effective term"] = FormatTerm(site.TermType, site.TermValue, site.TermUnit);
        }

        return values;
    }

    public static IReadOnlyDictionary<string, string?> CaptureWebmasterOffer(SiteWebmasterOffer offer)
    {
        var values = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Status"] = offer.Status.ToString(),
            ["Effective term"] = FormatTerm(offer.TermType, offer.TermValue, offer.TermUnit),
            ["Contact"] = offer.ContactRawText,
            ["Outreach sender"] = offer.OutreachSenderRawText,
            ["Linkbuilder mailbox raw text"] = offer.LinkbuilderMailboxRawText,
            ["Link policy"] = offer.LinkPolicyText,
            ["DF links"] = offer.DfLinksRawText,
            ["Sponsored tag"] = offer.SponsoredTagRawText,
            ["Comments"] = offer.CommentText,
            ["Client"] = offer.ClientRawText,
            ["Linkbuilder mailboxes"] = string.Join(", ", offer.LinkbuilderMailboxes
                .OrderBy(link => link.LinkbuilderMailbox.Email, StringComparer.OrdinalIgnoreCase)
                .Select(link => link.LinkbuilderMailbox.Email))
        };

        foreach (var price in offer.Prices.OrderBy(price => price.PriceType))
        {
            var prefix = $"Price · {price.PriceType}";
            values[$"{prefix} · availability"] = price.AvailabilityStatus.ToString();
            values[$"{prefix} · USD"] = Format(price.WebmasterPriceUsd);
            values[$"{prefix} · details"] = price.WebmasterPriceDetails;
        }

        return values;
    }

    public static bool RecordUpdate(
        ApplicationDbContext context,
        string entityType,
        string entityId,
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after,
        string source,
        string? userEmail,
        DateTime changedAtUtc)
    {
        var changes = BuildChanges(before, after);
        if (changes.Count == 0)
        {
            return false;
        }

        context.EntityChangeHistories.Add(new EntityChangeHistory
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = EntityChangeHistoryConstants.UpdatedAction,
            Source = source,
            ChangedBy = AuditUserFormatter.Format(userEmail),
            ChangedAtUtc = changedAtUtc,
            ChangesJson = JsonSerializer.Serialize(changes, JsonOptions)
        });

        return true;
    }

    public static async Task<IReadOnlyList<EntityChangeHistoryDto>> GetHistoryAsync(
        ApplicationDbContext context,
        string entityType,
        string entityId,
        CancellationToken cancellationToken)
    {
        var rows = await context.EntityChangeHistories
            .AsNoTracking()
            .Where(history => history.EntityType == entityType && history.EntityId == entityId)
            .OrderByDescending(history => history.ChangedAtUtc)
            .ThenByDescending(history => history.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(history => new EntityChangeHistoryDto
        {
            Id = history.Id,
            Action = history.Action,
            Source = history.Source,
            ChangedBy = history.ChangedBy,
            ChangedAtUtc = history.ChangedAtUtc,
            Changes = DeserializeChanges(history.ChangesJson)
        }).ToList();
    }

    private static IReadOnlyList<EntityFieldChangeDto> BuildChanges(
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after)
    {
        var fields = before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(field => field, StringComparer.Ordinal);
        var changes = new List<EntityFieldChangeDto>();

        foreach (var field in fields)
        {
            before.TryGetValue(field, out var oldValue);
            after.TryGetValue(field, out var newValue);
            oldValue = Normalize(oldValue);
            newValue = Normalize(newValue);

            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new EntityFieldChangeDto
            {
                Field = field,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        return changes;
    }

    private static IReadOnlyList<EntityFieldChangeDto> DeserializeChanges(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<EntityFieldChangeDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Format(double value)
        => value.ToString("0.################", CultureInfo.InvariantCulture);

    private static string Format(long value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string? Format(int? value)
        => value?.ToString(CultureInfo.InvariantCulture);

    private static string? Format(decimal? value)
        => value?.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Format(bool value)
        => value ? "Yes" : "No";

    private static string FormatTerm(TermType? termType, int? termValue, TermUnit? termUnit)
        => termType switch
        {
            TermType.Permanent when termValue is null && termUnit is null => "Permanent",
            TermType.Finite when termValue is > 0 && termUnit == TermUnit.Year =>
                termValue.Value == 1 ? "1 year" : $"{termValue.Value} years",
            _ => "No term"
        };
}
