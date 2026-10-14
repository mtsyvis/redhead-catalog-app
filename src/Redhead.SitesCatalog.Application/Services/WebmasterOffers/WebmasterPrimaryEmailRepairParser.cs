using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Redhead.SitesCatalog.Application.Services.WebmasterOffers;

// Resolves the required primary contact while the public field still uses the PrimaryEmail name.
internal static class WebmasterPrimaryEmailRepairParser
{
    private static readonly Regex EmailRegex = new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ReplyRegex = Markers([
        "ответили здесь", "отвечают здесь", "ответили тут", "отвечают тут", "ответил тут",
        "ответ здесь", "ответ тут", "ответы тут", "отвечает тут", "ответ туту", "ответт тут",
        "ответ тту", "ответ утт", "отв тут", "otv tut", "primary email", "primary", "ответ", "answer", "reply", "отв", "otv",
        "главный контакт", "основной контакт", "основная почта", "основной email", "отвечают", "отвечает", "ответили", "ответила", "ответил",
        "answ", "ans", "answear", "amswer", "anwer", "answe", "answered", "answers", "otvet", "primare email",
        "переписка", "общение", "for communication", "новая почта", "новый контакт", "для быстрого ответа",
        "пишет отсюда", "пишет осюда", "пишет отсюа", "пишет тут", "пишет ут", "пишут ту", "пишут ут", "пишут тут",
        "ответили отсюда", "отвечали с этой", "отвтили отсюда", "отвеот утт", "jndtn nen", "jndtn", "jответ",
        "отыет", "овтет", "0твет", "отве", "ответы", "ответа",
        "более актуальная цена", "актуальная цена", "более актульная цена", "более актуальаня цена",
        "второй дешевле", "новый"
    ]);
    private static readonly Regex DirectionRegex = Markers([
        "писать сюда", "написать сюда", "написала сюда", "направили сюда", "write here", "сюда", "здесь", "тут", "here"
    ]);
    private static readonly HashSet<string> ExplicitDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "писать сюда", "написать сюда", "написала сюда", "направили сюда", "write here"
    };
    private static readonly Regex RestrictionRegex = new(
        @"не\s+(пишем|писать|пиши|отвеч|работа|использ|актуал|владеет)|устарел|неактуал|недейств|больше\s+нет|неверн|карантин|quarantine|игнор|do\s+not\s+(contact|write|use)|don't\s+(contact|write|use)|no\s+(reply|answer|working)|not\s+working|address\s+not\s+found|\bno\s+w\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BeforeGapRegex = new(@"^[ \t=:(\[\-–—]*$");
    private static readonly Regex AfterGapRegex = new(@"^[ \t=:()\[\]\-–—]*$");
    private static readonly Regex OpeningBracketRegex = new(@"[\(\[]");
    private static readonly Regex ClosingBracketRegex = new(@"^[ \t]*[\)\]]");

    public static string Resolve(string? contact, Guid webmasterId)
    {
        if (string.IsNullOrWhiteSpace(contact))
        {
            return $"Contact not specified ({webmasterId:D})";
        }

        var marked = TryExtract(contact);
        if (marked is not null)
        {
            return marked;
        }

        foreach (Match match in EmailRegex.Matches(contact))
        {
            var email = ValidEmailOrNull(match.Value.ToLowerInvariant());
            if (email is not null)
            {
                return email;
            }
        }

        return contact.Trim();
    }

    public static string? Normalize(string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return ValidEmailOrNull(text.ToLowerInvariant()) is not null ? text.ToLowerInvariant() : text;
    }

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
        // A standalone note belongs to the preceding line only when that line is a bare email.
        // Do not bridge a blank line or a line with another address/comment.
        if (lineStart > 0 && Regex.IsMatch(prefix + suffix, @"^[ \t=:(\[)\]\-–—]*$"))
        {
            var precedingLine = contact[..lineStart].TrimEnd('\r', '\n').Split(['\r', '\n'])[^1];
            var precedingEmails = EmailRegex.Matches(precedingLine).Cast<Match>().ToArray();
            if (precedingEmails.Length == 1 && Regex.IsMatch(
                precedingLine.Remove(precedingEmails[0].Index, precedingEmails[0].Length), @"^[ \t;<>]*$"))
            {
                // Require exactly one intervening newline (CRLF counts as one).
                var previousEnd = lineStart - (contact[lineStart - 1] == '\n' && lineStart > 1 && contact[lineStart - 2] == '\r' ? 2 : 1);
                if (previousEnd > 0 && contact[previousEnd - 1] is not '\r' and not '\n')
                {
                    return ValidEmailOrNull(precedingEmails[0].Value.ToLowerInvariant());
                }
            }
        }
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
