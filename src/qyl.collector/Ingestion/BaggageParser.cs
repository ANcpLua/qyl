// =============================================================================
// qyl.collector - W3C Baggage Parser
// Parses W3C baggage header format into Baggage container
// https://www.w3.org/TR/baggage/
// =============================================================================

using qyl.protocol.Baggage;

namespace qyl.collector.Ingestion;

/// <summary>
///     Parses W3C baggage header format.
///     Format: key1=value1,key2=value2;metadata,key3=value3
/// </summary>
public static class BaggageParser
{
    private const int MaxBaggageLength = 8192; // 8KB limit per W3C spec
    private const int MaxEntries = 180; // Max entries per W3C spec
    private const char ListDelimiter = ',';
    private const char KeyValueDelimiter = '=';
    private const char MetadataDelimiter = ';';

    /// <summary>
    ///     Parse W3C baggage header value.
    /// </summary>
    /// <param name="headerValue">The baggage header value (may be null/empty).</param>
    /// <returns>Parsed Baggage instance, or empty if invalid/null.</returns>
    public static Baggage Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return Baggage.Empty;

        if (headerValue.Length > MaxBaggageLength)
            return Baggage.Empty; // Exceeds limit, ignore

        var entries = new List<KeyValuePair<string, BaggageEntry>>();

        // Split by comma (list-member delimiter)
        var members = headerValue.AsSpan();
        var entryCount = 0;

        while (!members.IsEmpty && entryCount < MaxEntries)
        {
            // Find next comma
            var commaIndex = members.IndexOf(ListDelimiter);
            var member = commaIndex >= 0 ? members[..commaIndex] : members;
            members = commaIndex >= 0 ? members[(commaIndex + 1)..] : ReadOnlySpan<char>.Empty;

            // Trim whitespace (OWS = optional whitespace)
            member = member.Trim();
            if (member.IsEmpty)
                continue;

            // Parse key=value;metadata
            var entry = ParseMember(member);
            if (entry.HasValue)
            {
                entries.Add(entry.Value);
                entryCount++;
            }
        }

        return entries.Count is 0 ? Baggage.Empty : Baggage.Create(entries);
    }

    private static KeyValuePair<string, BaggageEntry>? ParseMember(ReadOnlySpan<char> member)
    {
        // Find = delimiter
        var equalsIndex = member.IndexOf(KeyValueDelimiter);
        if (equalsIndex <= 0)
            return null; // No key or no delimiter

        // Extract key (before =)
        var keySpan = member[..equalsIndex].Trim();
        if (keySpan.IsEmpty)
            return null;

        // Extract value and optional metadata (after =)
        var valueAndMeta = member[(equalsIndex + 1)..];

        // Check for metadata delimiter
        var semicolonIndex = valueAndMeta.IndexOf(MetadataDelimiter);
        ReadOnlySpan<char> valueSpan;
        string? metadata = null;

        if (semicolonIndex >= 0)
        {
            valueSpan = valueAndMeta[..semicolonIndex].Trim();
            var metaSpan = valueAndMeta[(semicolonIndex + 1)..].Trim();
            if (!metaSpan.IsEmpty)
                metadata = metaSpan.ToString();
        }
        else
        {
            valueSpan = valueAndMeta.Trim();
        }

        // Decode percent-encoded values
        var key = PercentDecode(keySpan);
        var value = PercentDecode(valueSpan);

        if (string.IsNullOrEmpty(key))
            return null;

        return new KeyValuePair<string, BaggageEntry>(key, new BaggageEntry(value, metadata));
    }

    /// <summary>
    ///     Decode percent-encoded characters.
    /// </summary>
    private static string PercentDecode(ReadOnlySpan<char> input)
    {
        if (input.IsEmpty)
            return string.Empty;

        // Fast path: no percent encoding
        if (input.IndexOf('%') < 0)
            return input.ToString();

        // Slow path: decode percent-encoded chars
        var result = new char[input.Length];
        var resultIndex = 0;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '%' && i + 2 < input.Length)
            {
                // Try to parse hex digits
                if (TryParseHex(input[i + 1], input[i + 2], out var decoded))
                {
                    result[resultIndex++] = decoded;
                    i += 2;
                    continue;
                }
            }

            result[resultIndex++] = c;
        }

        return new string(result, 0, resultIndex);
    }

    private static bool TryParseHex(char high, char low, out char result)
    {
        result = '\0';

        var highVal = HexValue(high);
        var lowVal = HexValue(low);

        if (highVal < 0 || lowVal < 0)
            return false;

        result = (char)((highVal << 4) | lowVal);
        return true;
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'A' and <= 'F' => c - 'A' + 10,
        >= 'a' and <= 'f' => c - 'a' + 10,
        _ => -1
    };

    /// <summary>
    ///     Extract baggage from OTLP span attributes.
    ///     OTLP doesn't have a dedicated baggage field, but some instrumentations
    ///     store baggage in attributes with "baggage." prefix.
    /// </summary>
    public static Baggage ExtractFromAttributes(Dictionary<string, string>? attributes)
    {
        if (attributes is null || attributes.Count is 0)
            return Baggage.Empty;

        var entries = new List<KeyValuePair<string, BaggageEntry>>();

        foreach (var kvp in attributes)
        {
            if (kvp.Key.StartsWith("baggage.", StringComparison.Ordinal))
            {
                var baggageKey = kvp.Key[8..]; // Remove "baggage." prefix
                if (!string.IsNullOrEmpty(baggageKey))
                    entries.Add(new KeyValuePair<string, BaggageEntry>(baggageKey, new BaggageEntry(kvp.Value)));
            }
        }

        return entries.Count is 0 ? Baggage.Empty : Baggage.Create(entries);
    }
}
