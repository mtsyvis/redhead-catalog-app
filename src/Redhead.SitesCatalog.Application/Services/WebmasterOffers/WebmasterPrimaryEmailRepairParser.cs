using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

// Conservative additions to import extraction. Ambiguous legacy behavior stays with the importer.
internal static class WebmasterPrimaryEmailRepairParser
{
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ReplyRegex = Markers([
        "ответили здесь", "отвечают здесь", "ответили тут", "отвечают тут", "ответил тут",
        "ответ здесь", "ответ тут", "ответы тут", "отвечает тут", "ответ туту", "ответт тут",
        "ответ тту", "ответ утт", "отв тут", "otv tut", "primary email", "primary", "ответ", "answer", "reply", "отв", "otv",
        "главный контакт", "основной контакт", "основная почта", "основной email", "отвечают", "отвечает", "ответили", "ответила", "ответил"
    ]);
    private static readonly Regex DirectionRegex = Markers([
        "писать сюда", "написать сюда", "написала сюда", "направили сюда", "write here", "сюда", "здесь", "тут", "here"
    ]);
    private static readonly HashSet<string> ExplicitDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "писать сюда", "написать сюда", "написала сюда", "направили сюда", "write here"
    };
    private static readonly Regex RestrictionRegex = new(
        @"не\s+(пишем|писать|пиши|отвеч|работает|использ|актуал)|устарел|неактуал|недейств|do\s+not\s+(contact|write|use)|don't\s+(contact|write|use)|no\s+(reply|answer)|not\s+working",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BeforeGapRegex = new(@"^[ \t=:(\[\-–—]*$");
    private static readonly Regex AfterGapRegex = new(@"^[ \t=:()\[\]\-–—]*$");
    private static readonly Regex OpeningBracketRegex = new(@"[\(\[]");
    private static readonly Regex ClosingBracketRegex = new(@"^[ \t]*[\)\]]");

    public static string? TryExtract(string? contact)
    {
        if (string.IsNullOrWhiteSpace(contact) || RestrictionRegex.IsMatch(contact))
        {
            return null;
        }

        var emailMatches = EmailRegex.Matches(contact).Cast<Match>().ToArray();
        var distinctEmails = emailMatches.Select(match => match.Value.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        if (distinctEmails.Length == 1)
        {
            return ValidEmailOrNull(distinctEmails[0]);
        }

        Match[] OutsideEmails(Regex regex) => regex.Matches(contact).Cast<Match>()
            .Where(marker => !emailMatches.Any(email => marker.Index >= email.Index && marker.Index < email.Index + email.Length)).ToArray();
        var markers = OutsideEmails(ReplyRegex);
        if (markers.Length == 0)
        {
            var directions = OutsideEmails(DirectionRegex);
            if (directions.Length == 1 && ExplicitDirections.Contains(directions[0].Value))
            {
                markers = directions;
            }
        }
        if (markers.Length != 1)
        {
            return null;
        }

        var marker = markers[0];
        var lineStart = Math.Max(contact.LastIndexOf('\n', marker.Index), contact.LastIndexOf('\r', marker.Index)) + 1;
        var prefix = contact[lineStart..marker.Index];
        var suffix = contact[(marker.Index + marker.Length)..].Split(['\r', '\n'])[0];
        var before = EmailRegex.Matches(prefix).Cast<Match>().LastOrDefault();
        var after = EmailRegex.Matches(suffix).Cast<Match>().FirstOrDefault();
        var beforeGap = before is null ? null : prefix[(before.Index + before.Length)..];
        var afterGap = after is null ? null : suffix[..after.Index];
        var beforeAdjacent = beforeGap is not null && BeforeGapRegex.IsMatch(beforeGap);
        var afterAdjacent = afterGap is not null && AfterGapRegex.IsMatch(afterGap);
        if (beforeAdjacent && afterAdjacent)
        {
            return OpeningBracketRegex.IsMatch(beforeGap!) && ClosingBracketRegex.IsMatch(afterGap!)
                ? ValidEmailOrNull(before!.Value.ToLowerInvariant())
                : null;
        }
        return beforeAdjacent ? ValidEmailOrNull(before!.Value.ToLowerInvariant())
            : afterAdjacent ? ValidEmailOrNull(after!.Value.ToLowerInvariant()) : null;
    }

    private static Regex Markers(IEnumerable<string> markers)
        => new($@"(?<![\p{{L}}\p{{N}}])(?:{string.Join("|", markers.OrderByDescending(marker => marker.Length).Select(Regex.Escape))})(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string? ValidEmailOrNull(string email)
        => email.Length <= 320 && MailAddress.TryCreate(email, out var parsed)
            && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase) && parsed.Host.Contains('.', StringComparison.Ordinal)
            ? email : null;
}
