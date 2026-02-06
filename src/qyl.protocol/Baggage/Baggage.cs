// =============================================================================
// qyl.protocol - W3C Baggage Support
// OTel Baggage passthrough for collector storage and query
// Owner: qyl.protocol | Consumers: collector
// =============================================================================

using System.Collections.Immutable;

namespace qyl.protocol.Baggage;

/// <summary>
///     W3C Baggage entry with value and optional metadata.
///     https://www.w3.org/TR/baggage/
/// </summary>
public readonly record struct BaggageEntry(string Value, string? Metadata = null)
{
    /// <summary>Check if this entry has metadata.</summary>
    public bool HasMetadata => Metadata is not null;

    public override string ToString() => HasMetadata ? $"{Value};{Metadata}" : Value;
}

/// <summary>
///     Immutable W3C Baggage container.
///     Represents name/value pairs for cross-cutting concern propagation.
/// </summary>
public sealed class Baggage
{
    /// <summary>Empty baggage instance.</summary>
    public static readonly Baggage Empty = new([]);

    private readonly ImmutableDictionary<string, BaggageEntry> _entries;

    private Baggage(ImmutableDictionary<string, BaggageEntry> entries) => _entries = entries;

    /// <summary>Number of entries in the baggage.</summary>
    public int Count => _entries.Count;

    /// <summary>Check if baggage is empty.</summary>
    public bool IsEmpty => _entries.IsEmpty;

    /// <summary>All entry keys.</summary>
    public IEnumerable<string> Keys => _entries.Keys;

    /// <summary>All entries.</summary>
    public IEnumerable<KeyValuePair<string, BaggageEntry>> Entries => _entries;

    /// <summary>
    ///     Get value for a given key.
    /// </summary>
    /// <param name="name">The key to look up.</param>
    /// <returns>The value if found, null otherwise.</returns>
    public string? GetValue(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry.Value : null;

    /// <summary>
    ///     Get full entry (value + metadata) for a given key.
    /// </summary>
    public BaggageEntry? GetEntry(string name) =>
        _entries.TryGetValue(name, out var entry) ? entry : null;

    /// <summary>
    ///     Set a value, returning a new Baggage instance.
    /// </summary>
    /// <param name="name">The key (must be valid token per RFC7230).</param>
    /// <param name="value">The value (UTF-8 string).</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>New Baggage with the value set.</returns>
    public Baggage SetValue(string name, string value, string? metadata = null)
    {
        if (string.IsNullOrEmpty(name))
            return this;

        return new Baggage(_entries.SetItem(name, new BaggageEntry(value, metadata)));
    }

    /// <summary>
    ///     Remove a value, returning a new Baggage instance.
    /// </summary>
    public Baggage RemoveValue(string name)
    {
        if (string.IsNullOrEmpty(name) || !_entries.ContainsKey(name))
            return this;

        return new Baggage(_entries.Remove(name));
    }

    /// <summary>
    ///     Create baggage from a dictionary of values.
    /// </summary>
    public static Baggage Create(IEnumerable<KeyValuePair<string, string>> values)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, BaggageEntry>(StringComparer.Ordinal);
        foreach (var kvp in values)
        {
            if (!string.IsNullOrEmpty(kvp.Key))
                builder[kvp.Key] = new BaggageEntry(kvp.Value);
        }

        return builder.Count is 0 ? Empty : new Baggage(builder.ToImmutable());
    }

    /// <summary>
    ///     Create baggage from entries with metadata.
    /// </summary>
    public static Baggage Create(IEnumerable<KeyValuePair<string, BaggageEntry>> entries)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, BaggageEntry>(StringComparer.Ordinal);
        foreach (var kvp in entries)
        {
            if (!string.IsNullOrEmpty(kvp.Key))
                builder[kvp.Key] = kvp.Value;
        }

        return builder.Count is 0 ? Empty : new Baggage(builder.ToImmutable());
    }

    /// <summary>
    ///     Convert to dictionary for JSON serialization.
    /// </summary>
    public Dictionary<string, string> ToDictionary()
    {
        var dict = new Dictionary<string, string>(_entries.Count, StringComparer.Ordinal);
        foreach (var kvp in _entries)
            dict[kvp.Key] = kvp.Value.Value;
        return dict;
    }

    /// <summary>
    ///     Serialize to W3C baggage header format.
    ///     Format: key1=value1,key2=value2;metadata
    /// </summary>
    public string ToHeaderValue()
    {
        if (_entries.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();
        var first = true;

        foreach (var kvp in _entries)
        {
            if (!first) sb.Append(',');
            first = false;

            sb.Append(PercentEncode(kvp.Key));
            sb.Append('=');
            sb.Append(PercentEncode(kvp.Value.Value));

            if (kvp.Value.Metadata is not null)
            {
                sb.Append(';');
                sb.Append(kvp.Value.Metadata);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Percent-encode characters outside baggage-octet range.
    /// </summary>
    private static string PercentEncode(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            // baggage-octet = %x21 / %x23-2B / %x2D-3A / %x3C-5B / %x5D-7E
            if (c is >= '\x21' and <= '\x7E' and not ',' and not ';' and not '\\' and not '"')
                sb.Append(c);
            else
                sb.Append($"%{(int)c:X2}");
        }

        return sb.ToString();
    }

    public override string ToString() => $"Baggage[{Count} entries]";
}
