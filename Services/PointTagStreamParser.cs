using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ClickyWindows.Services;

public sealed class PointTagStreamParser
{
    private static readonly Regex TagRegex = new(
        """\[\s*(?<action>POINT|CLICK|DOUBLE_CLICK)\s*:\s*(?<x>-?\d+(?:\.\d+)?)\s*(?<xp>%?)\s*,\s*(?<y>-?\d+(?:\.\d+)?)\s*(?<yp>%?)\s*(?:,|:)\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<plain>[^\]]*?))\s*\]""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly StringBuilder _pending = new();
    private readonly Action<PointTarget> _onPoint;

    public PointTagStreamParser(Action<PointTarget> onPoint) => _onPoint = onPoint;

    public string Append(string chunk)
    {
        _pending.Append(chunk);
        return Drain(complete: false);
    }

    public string Complete() => Drain(complete: true);

    public static IReadOnlyList<PointTarget> Parse(string text)
    {
        var points = new List<PointTarget>();
        foreach (Match match in TagRegex.Matches(text))
        {
            if (TryCreatePoint(match, out var point))
                points.Add(point);
        }
        return points;
    }

    public static string Strip(string text) => TagRegex.Replace(text, "").Trim();

    private string Drain(bool complete)
    {
        var input = _pending.ToString();
        var visible = new StringBuilder();
        var consumed = 0;

        foreach (Match match in TagRegex.Matches(input))
        {
            visible.Append(input, consumed, match.Index - consumed);
            if (TryCreatePoint(match, out var point))
                _onPoint(point);
            consumed = match.Index + match.Length;
        }

        var remainder = input[consumed..];
        if (complete)
        {
            var incompletePointStart = FindPossibleTagStart(remainder);
            visible.Append(incompletePointStart >= 0 ? remainder[..incompletePointStart] : remainder);
            _pending.Clear();
            return visible.ToString();
        }

        var holdFrom = FindPossibleTagStart(remainder);
        if (holdFrom < 0)
        {
            visible.Append(remainder);
            _pending.Clear();
        }
        else
        {
            visible.Append(remainder[..holdFrom]);
            _pending.Clear();
            _pending.Append(remainder[holdFrom..]);
        }
        return visible.ToString();
    }

    private static int FindPossibleTagStart(string text)
    {
        var open = text.LastIndexOf('[');
        if (open < 0)
            return -1;
        var suffix = text[open..];
        string[] prefixes = ["[POINT:", "[CLICK:", "[DOUBLE_CLICK:"];
        return prefixes.Any(prefix =>
            suffix.Length <= prefix.Length && prefix.StartsWith(suffix, StringComparison.OrdinalIgnoreCase)
            || suffix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? open : -1;
    }

    private static bool TryCreatePoint(Match match, out PointTarget point)
    {
        point = default!;
        if (!double.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return false;

        var label = FirstValue(match, "double", "single", "plain").Trim();
        var action = match.Groups["action"].Value.ToUpperInvariant() switch
        {
            "CLICK" => PointAction.Click,
            "DOUBLE_CLICK" => PointAction.DoubleClick,
            _ => PointAction.Guide,
        };
        point = new PointTarget(
            x,
            y,
            string.IsNullOrWhiteSpace(label) ? "this area" : label,
            match.Groups["xp"].Value == "%",
            match.Groups["yp"].Value == "%",
            action);
        return true;
    }

    private static string FirstValue(Match match, params string[] names)
    {
        foreach (var name in names)
        {
            if (match.Groups[name].Success)
                return match.Groups[name].Value;
        }
        return "";
    }
}
