using System.Text.RegularExpressions;

namespace Qyl.Collector;

/// <summary>
/// The one rule every contract time is read by, the MCP's input schema's as the Collector's routes': RFC 3339 §5.6
/// date-time with <c>T</c> and <c>Z</c> in either case, a numeric offset with its colon, a second from 00 to 59 and a
/// fraction of any length, and no space in place of <c>T</c>. A leap second is refused: no instant either side reads
/// holds a second 60. https://www.rfc-editor.org/rfc/rfc3339#section-5.6
/// </summary>
internal static partial class ContractDateTime
{
    /// <summary>Whether <paramref name="text"/> is a contract date-time, whether or not a <see cref="DateTimeOffset"/> holds its instant.</summary>
    internal static bool IsValid(string text) => Read(text) is not null;

    /// <summary>
    /// A contract date-time's instant and its fraction's digits, or null when <paramref name="text"/> is none. The instant
    /// keeps the offset it was written with, or is UTC for an offset beyond the 14 hours a <see cref="DateTimeOffset"/>
    /// keeps; it is null outside the years 0001 to 9999. Digits past the seventh, below a tick, are left to the caller.
    /// </summary>
    internal static (DateTimeOffset? Instant, string Fraction)? Read(string text)
    {
        if (Grammar().Match(text) is not { Success: true } match) return null;
        int Part(string name) => int.Parse(match.Groups[name].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
        int year = Part("year"), month = Part("month"), day = Part("day"), hour = Part("hour"), minute = Part("minute"), second = Part("second");
        // Year 0 follows the same 400-year leap rule as 2000; DateTime starts at year 1.
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year is 0 ? 2000 : year, month) || hour > 23 || minute > 59 || second > 59) return null;
        var offset = TimeSpan.Zero;
        if (match.Groups["sign"].Success)
        {
            int offsetHours = Part("offsetHour"), offsetMinutes = Part("offsetMinute");
            if (offsetHours > 23 || offsetMinutes > 59) return null;
            offset = new TimeSpan(offsetHours, offsetMinutes, 0) * (match.Groups["sign"].ValueSpan is "-" ? -1 : 1);
        }
        var fraction = match.Groups["fraction"].Value;
        if (year is 0) return (null, fraction);
        var ticks = fraction.Length is 0 ? 0 : long.Parse(fraction.Length > 7 ? fraction[..7] : fraction.PadRight(7, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        var local = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(ticks);
        try
        {
            return (offset.Duration() <= TimeSpan.FromHours(14) ? new DateTimeOffset(local, offset) : new DateTimeOffset(local - offset, TimeSpan.Zero), fraction);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, fraction);
        }
    }

    /// <summary>Whether a fraction is finer than the 100 ns tick an instant holds: a digit past the seventh that is not zero.</summary>
    internal static bool FinerThanTicks(string fraction) => fraction.Length > 7 && fraction.AsSpan(7).ContainsAnyExcept('0');

    [GeneratedRegex("^(?<year>[0-9]{4})-(?<month>[0-9]{2})-(?<day>[0-9]{2})[Tt](?<hour>[0-9]{2}):(?<minute>[0-9]{2}):(?<second>[0-9]{2})(?:\\.(?<fraction>[0-9]+))?(?:[Zz]|(?<sign>[+-])(?<offsetHour>[0-9]{2}):(?<offsetMinute>[0-9]{2}))\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Grammar();
}
