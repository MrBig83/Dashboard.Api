using System.Text.RegularExpressions;
using System.Xml.Linq;
using Dashboard.Api.Models;

namespace Dashboard.Api.Services;

public class SchoolMenuParser
{
    private static readonly string[] DayOrder =
    [
        "Måndag",
        "Tisdag",
        "Onsdag",
        "Torsdag",
        "Fredag"
    ];

    private static readonly Dictionary<string, string> DayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mandag"] = "Måndag",
        ["måndag"] = "Måndag",
        ["tisdag"] = "Tisdag",
        ["onsdag"] = "Onsdag",
        ["torsdag"] = "Torsdag",
        ["fredag"] = "Fredag"
    };

    public SchoolMenuResponse Parse(string xmlText)
    {
        var document = XDocument.Parse(xmlText);
        var menuByDay = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in document.Descendants("item"))
        {
            var title = item.Element("title")?.Value?.Trim() ?? string.Empty;
            var dayLabel = title.Split('-')[0].Trim();
            if (string.IsNullOrWhiteSpace(dayLabel))
            {
                continue;
            }

            var day = NormalizeDay(dayLabel);
            var description = item.Element("description")?.Value ?? string.Empty;
            menuByDay[day] = CleanMenuItems(description).ToArray();
        }

        var days = DayOrder
            .Select(day => new SchoolMenuDay(
                day,
                menuByDay.TryGetValue(day, out var items) ? items : []))
            .ToArray();

        return new SchoolMenuResponse(days);
    }

    private static string NormalizeDay(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        return DayMap.TryGetValue(key, out var day) ? day : value.Trim();
    }

    private static IEnumerable<string> CleanMenuItems(string description)
    {
        var withLineBreaks = Regex.Replace(description, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, "<.*?>", string.Empty);
        var plainText = withoutTags.Replace("&nbsp;", " ");

        var uniqueParts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in plainText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cleaned = Regex.Replace(part, "\\s+", " ").Trim();
            cleaned = Regex.Replace(cleaned, "\\bSvenskproducerat\\b", string.Empty, RegexOptions.IgnoreCase).Trim();
            cleaned = Regex.Replace(cleaned, "[,\\s]+$", string.Empty).Trim();

            var parenthesisIndex = cleaned.IndexOf('(');
            if (parenthesisIndex >= 0)
            {
                cleaned = cleaned[..parenthesisIndex].Trim();
            }

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var dedupeKey = Regex.Replace(cleaned, "\\s+", " ").Trim().TrimEnd(',');
            if (seen.Add(dedupeKey))
            {
                uniqueParts.Add(cleaned);
            }
        }

        return uniqueParts;
    }
}
