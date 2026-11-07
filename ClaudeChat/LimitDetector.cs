using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClaudeChat;

/// <summary>
/// Recognises the usage-limit error and, where the message says, when the limit resets.
/// Known shapes: "Claude AI usage limit reached|1730000000" and
/// "You've hit your limit · resets 3pm (Europe/Zagreb)" / "resets at 15:00".
/// </summary>
public static class LimitDetector
{
    private static readonly Regex Epoch = new(@"\|(\d{10,13})\b", RegexOptions.Compiled);
    private static readonly Regex Clock = new(
        @"resets?\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool LooksLikeUsageLimit(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string t = text.ToLowerInvariant();
        return (t.Contains("limit") && (t.Contains("usage") || t.Contains("reset") || t.Contains("hit your")))
               || t.Contains("rate_limit") || t.Contains("429");
    }

    public static DateTimeOffset? ParseReset(string text, DateTimeOffset now)
    {
        var m = Epoch.Match(text);
        if (m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long n))
            return DateTimeOffset.FromUnixTimeSeconds(n > 10_000_000_000 ? n / 1000 : n).ToLocalTime();

        m = Clock.Match(text);
        if (!m.Success) return null;
        int hour = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        int minute = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (m.Groups[3].Success)
        {
            bool pm = m.Groups[3].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);
            hour %= 12;
            if (pm) hour += 12;
        }
        if (hour > 23 || minute > 59) return null;
        // The time is given in the user's own zone; take its next occurrence.
        var local = now.ToLocalTime();
        var candidate = new DateTimeOffset(local.Year, local.Month, local.Day, hour, minute, 0, local.Offset);
        if (candidate <= local) candidate = candidate.AddDays(1);
        return candidate;
    }
}
