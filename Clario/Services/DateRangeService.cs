using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Clario.Services;

/// <summary>Resolves a named date range option into concrete start/end dates and a display label.</summary>
public static class DateRangeService
{
    private static readonly CultureInfo Culture = new("en-US");

    /// <param name="option">The named range key (e.g. "This Month", "Custom Range").</param>
    /// <param name="customDates">Required when option is "Custom Range".</param>
    /// <returns>Null Start/End means "All Time" (no filter). Label is already uppercased for display.</returns>
    public static (DateTime? Start, DateTime? End, string Label) Resolve(string option, IList<DateTime>? customDates = null)
    {
        var now = DateTime.Now;
        return option switch
        {
            "Today"        => (now.Date, now.Date, now.ToString("MMM d, yyyy", Culture).ToUpper()),
            "This Week"    => ResolveThisWeek(now),
            "This Month"   => ResolveThisMonth(now),
            "Last Month"   => ResolveLastMonth(now),
            "This Quarter" => ResolveThisQuarter(now),
            "This Year"    => (new DateTime(now.Year, 1, 1), new DateTime(now.Year, 12, 31), now.Year.ToString()),
            "Custom Range" => ResolveCustomRange(customDates, now),
            _              => (null, null, "ALL TIME")
        };
    }

    private static (DateTime?, DateTime?, string) ResolveThisWeek(DateTime now)
    {
        var start = now.Date.AddDays(-(int)now.DayOfWeek);
        return (start, start.AddDays(6), "THIS WEEK");
    }

    private static (DateTime?, DateTime?, string) ResolveThisMonth(DateTime now)
    {
        var start = new DateTime(now.Year, now.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1), now.ToString("MMMM yyyy", Culture).ToUpper());
    }

    private static (DateTime?, DateTime?, string) ResolveLastMonth(DateTime now)
    {
        var lm = now.AddMonths(-1);
        var start = new DateTime(lm.Year, lm.Month, 1);
        return (start, start.AddMonths(1).AddDays(-1), lm.ToString("MMMM yyyy", Culture).ToUpper());
    }

    private static (DateTime?, DateTime?, string) ResolveThisQuarter(DateTime now)
    {
        var quarterMonth = now.Month - ((now.Month - 1) % 3);
        var start = new DateTime(now.Year, quarterMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);
        return (start, end, $"Q{(now.Month - 1) / 3 + 1} {now.Year}");
    }

    private static (DateTime?, DateTime?, string) ResolveCustomRange(IList<DateTime>? dates, DateTime now)
    {
        if (dates is null || dates.Count == 0)
            return (now.Date, now.Date, now.ToString("MMM d, yyyy", Culture).ToUpper());

        var ordered = dates.Select(d => d.Date).Distinct().OrderBy(d => d).ToList();
        var start = ordered.First();
        var end = ordered.Last();

        var label = ordered.Count == 1
            ? start.ToString("MMM dd, yyyy", Culture).ToUpper()
            : $"{start.ToString("MMM dd", Culture)} - {end.ToString("MMM dd, yyyy", Culture)}".ToUpper();

        return (start, end, label);
    }

    /// <summary>Formats a date as "Today - MMM dd", "Yesterday - MMM dd", or "MMM dd, yyyy".</summary>
    public static string FormatGroupHeader(DateTime date)
    {
        var now = DateTime.Now.Date;
        if (date.Date == now) return "Today — " + date.ToString("MMM dd", Culture);
        if (date.Date == now.AddDays(-1)) return "Yesterday — " + date.ToString("MMM dd", Culture);
        return date.ToString("MMM dd, yyyy", Culture);
    }
}
