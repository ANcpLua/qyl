// =============================================================================
// qyl.protocol - SchemaVersion Primitive
// Represents an OpenTelemetry semantic conventions schema version
// Owner: qyl.protocol | Consumers: [collector, mcp]
// =============================================================================

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace qyl.protocol.Primitives;

/// <summary>
///     Represents an OpenTelemetry semantic conventions schema version.
///     Used to track and normalize telemetry from different SDK versions.
/// </summary>
public sealed partial record SchemaVersion : IComparable<SchemaVersion>
{
    /// <summary>Creates a new schema version.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SchemaVersion(int major, int minor, int patch)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(major);
        ArgumentOutOfRangeException.ThrowIfNegative(minor);
        ArgumentOutOfRangeException.ThrowIfNegative(patch);

        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>Major version number.</summary>
    public int Major { get; }

    /// <summary>Minor version number.</summary>
    public int Minor { get; }

    /// <summary>Patch version number.</summary>
    public int Patch { get; }

    // =========================================================================
    // Well-Known Versions
    // =========================================================================

    /// <summary>OTel semconv v1.21.0 - http.method → http.request.method</summary>
    public static SchemaVersion V1_21_0 { get; } = new(1, 21, 0);

    /// <summary>OTel semconv v1.24.0 - db.statement → db.query.text</summary>
    public static SchemaVersion V1_24_0 { get; } = new(1, 24, 0);

    /// <summary>OTel semconv v1.27.0 - gen_ai.system → gen_ai.provider.name</summary>
    public static SchemaVersion V1_27_0 { get; } = new(1, 27, 0);

    /// <summary>OTel semconv v1.38.0 - Current target version for qyl</summary>
    public static SchemaVersion V1_38_0 { get; } = new(1, 38, 0);

    /// <summary>Current target version for normalization.</summary>
    public static SchemaVersion Current => V1_38_0;

    // =========================================================================
    // IComparable<SchemaVersion>
    // =========================================================================

    /// <inheritdoc />
    public int CompareTo(SchemaVersion? other)
    {
        if (other is null) return 1;

        var majorCmp = Major.CompareTo(other.Major);
        if (majorCmp is not 0) return majorCmp;

        var minorCmp = Minor.CompareTo(other.Minor);
        if (minorCmp is not 0) return minorCmp;

        return Patch.CompareTo(other.Patch);
    }

    // =========================================================================
    // Schema URL
    // =========================================================================

    /// <summary>Gets the official OpenTelemetry schema URL for this version.</summary>
    public Uri ToSchemaUrl() => new($"https://opentelemetry.io/schemas/{this}");

    /// <inheritdoc />
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static bool operator <(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(SchemaVersion left, SchemaVersion right) => left.CompareTo(right) >= 0;

    // =========================================================================
    // Parsing
    // =========================================================================

    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)$")]
    private static partial Regex VersionRegex();

    /// <summary>Parses a schema version from a string like "1.38.0".</summary>
    public static SchemaVersion Parse(string version)
    {
        Throw.IfNullOrWhitespace(version);

        var match = VersionRegex().Match(version);
        if (!match.Success)
            throw new FormatException($"Invalid schema version format: {version}");

        return new SchemaVersion(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
    }

    /// <summary>Parses a schema version from a URL like "https://opentelemetry.io/schemas/1.38.0".</summary>
    public static SchemaVersion ParseFromUrl(string schemaUrl)
    {
        Throw.IfNullOrWhitespace(schemaUrl);

        var lastSlash = schemaUrl.LastIndexOf('/');
        if (lastSlash < 0)
            throw new FormatException($"Invalid schema URL format: {schemaUrl}");

        return Parse(schemaUrl[(lastSlash + 1)..]);
    }

    /// <summary>Tries to parse a schema version string.</summary>
    public static bool TryParse(string? version, out SchemaVersion? result)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            result = null;
            return false;
        }

        try
        {
            result = Parse(version);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }

    /// <summary>Tries to parse a schema version from a URL.</summary>
    public static bool TryParseFromUrl(string? schemaUrl, out SchemaVersion? result)
    {
        if (string.IsNullOrWhiteSpace(schemaUrl))
        {
            result = null;
            return false;
        }

        try
        {
            result = ParseFromUrl(schemaUrl);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
}
