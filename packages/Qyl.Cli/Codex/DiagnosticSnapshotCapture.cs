using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;

namespace Qyl.Cli.Codex;

internal static class DiagnosticSnapshotCapture
{
    internal const string ExtensionId = "qyl.agent.diagnostic.snapshot";
    internal const int FormatVersion = 1;
    internal const int MaxIdentifierLength = 128;
    internal const int MaxVariables = 64;
    internal const int MaxChecks = 64;
    internal const int MaxValueDepth = 8;
    internal const int MaxValueBytes = 16 * 1024;
    internal const int MaxInputBytes = 192 * 1024;
    internal const int MaxCapturedBytes = 64 * 1024;

    private static readonly HashSet<string> s_phases =
        ["input", "output", "error", "checkpoint"];
    private static readonly HashSet<string> s_classifications =
        ["public", "internal", "sensitive", "secret"];
    private static readonly HashSet<string> s_operators =
    [
        "equal",
        "not_equal",
        "exists",
        "type_is",
        "contains",
        "less_than",
        "greater_than"
    ];
    private static readonly HashSet<string> s_valueTypes =
        ["null", "boolean", "integer", "number", "string", "json"];

    public static bool TryCreate(
        ActiveWorkflowRun active,
        JsonElement arguments,
        WorkflowSpoolProtector protector,
        DateTimeOffset submittedAt,
        out DiagnosticSnapshotInboxRequest? request,
        out DiagnosticSnapshotValidationError error)
    {
        request = null;
        error = default;

        if (arguments.ValueKind is not JsonValueKind.Object)
            return Fail("invalid_input", "arguments", out error);
        if (Encoding.UTF8.GetByteCount(arguments.GetRawText()) > MaxInputBytes)
            return Fail("payload_too_large", "arguments", out error);
        if (!HasOnlyProperties(
                arguments,
                ["snapshotId", "probeId", "phase", "variables", "checks"],
                out var invalidRootProperty))
        {
            return Fail("invalid_property", invalidRootProperty, out error);
        }
        if (!TryIdentifier(arguments, "snapshotId", out var snapshotId))
            return Fail("invalid_snapshot_id", "snapshotId", out error);
        if (!TryIdentifier(arguments, "probeId", out var probeId))
            return Fail("invalid_probe_id", "probeId", out error);
        if (!TryRequiredString(arguments, "phase", out var phase) || !s_phases.Contains(phase))
            return Fail("invalid_phase", "phase", out error);
        if (!arguments.TryGetProperty("variables", out var variablesElement) ||
            variablesElement.ValueKind is not JsonValueKind.Array)
        {
            return Fail("invalid_variables", "variables", out error);
        }
        if (variablesElement.GetArrayLength() > MaxVariables)
            return Fail("too_many_variables", "variables", out error);

        var variables = new List<DiagnosticVariable>(variablesElement.GetArrayLength());
        var variablesByName = new Dictionary<string, DiagnosticVariable>(StringComparer.Ordinal);
        var variableIndex = 0;
        foreach (var variableElement in variablesElement.EnumerateArray())
        {
            var path = $"variables[{variableIndex.ToString(CultureInfo.InvariantCulture)}]";
            variableIndex++;
            if (variableElement.ValueKind is not JsonValueKind.Object ||
                !HasOnlyProperties(variableElement, ["name", "classification", "value"], out _))
            {
                return Fail("invalid_variable", path, out error);
            }
            if (!TryIdentifier(variableElement, "name", out var name))
                return Fail("invalid_variable_name", $"{path}.name", out error);
            if (!TryRequiredString(variableElement, "classification", out var classification) ||
                !s_classifications.Contains(classification))
            {
                return Fail("invalid_classification", $"{path}.classification", out error);
            }
            if (!variableElement.TryGetProperty("value", out var value))
                return Fail("missing_value", $"{path}.value", out error);
            if (JsonDepth(value) > MaxValueDepth)
                return Fail("value_too_deep", $"{path}.value", out error);
            byte[] canonicalValue;
            try
            {
                canonicalValue = CanonicalJson(value);
            }
            catch (InvalidDataException)
            {
                return Fail("invalid_json_value", $"{path}.value", out error);
            }
            if (canonicalValue.Length > MaxValueBytes)
                return Fail("value_too_large", $"{path}.value", out error);

            var variable = new DiagnosticVariable(
                name,
                classification,
                ValueType(value),
                value,
                canonicalValue);
            if (!variablesByName.TryAdd(name, variable))
                return Fail("duplicate_variable", $"{path}.name", out error);
            variables.Add(variable);
        }

        var checks = new List<DiagnosticCheck>();
        if (arguments.TryGetProperty("checks", out var checksElement))
        {
            if (checksElement.ValueKind is not JsonValueKind.Array)
                return Fail("invalid_checks", "checks", out error);
            if (checksElement.GetArrayLength() > MaxChecks)
                return Fail("too_many_checks", "checks", out error);

            var checkIds = new HashSet<string>(StringComparer.Ordinal);
            var checkIndex = 0;
            foreach (var checkElement in checksElement.EnumerateArray())
            {
                var path = $"checks[{checkIndex.ToString(CultureInfo.InvariantCulture)}]";
                checkIndex++;
                if (checkElement.ValueKind is not JsonValueKind.Object ||
                    !HasOnlyProperties(
                        checkElement,
                        ["checkId", "operator", "actual", "expected", "expectedType"],
                        out _))
                {
                    return Fail("invalid_check", path, out error);
                }
                if (!TryIdentifier(checkElement, "checkId", out var checkId))
                    return Fail("invalid_check_id", $"{path}.checkId", out error);
                if (!checkIds.Add(checkId))
                    return Fail("duplicate_check", $"{path}.checkId", out error);
                if (!TryRequiredString(checkElement, "operator", out var checkOperator) ||
                    !s_operators.Contains(checkOperator))
                {
                    return Fail("invalid_operator", $"{path}.operator", out error);
                }
                if (!TryIdentifier(checkElement, "actual", out var actualName))
                {
                    return Fail("invalid_actual_variable", $"{path}.actual", out error);
                }
                variablesByName.TryGetValue(actualName, out var actualVariable);

                var hasExpected = checkElement.TryGetProperty("expected", out var expectedElement);
                var hasExpectedType = checkElement.TryGetProperty("expectedType", out var expectedTypeElement);
                string? expectedName = null;
                string? expectedType = null;
                DiagnosticVariable? expectedVariable = null;
                if (checkOperator is "equal" or "not_equal" or "contains" or "less_than" or "greater_than")
                {
                    if (!hasExpected || hasExpectedType ||
                        expectedElement.ValueKind is not JsonValueKind.String ||
                        !IsMachineIdentifier(expectedElement.GetString(), out expectedName))
                    {
                        return Fail("invalid_expected_variable", $"{path}.expected", out error);
                    }
                    variablesByName.TryGetValue(expectedName, out expectedVariable);
                }
                else if (checkOperator == "type_is")
                {
                    if (hasExpected || !hasExpectedType ||
                        expectedTypeElement.ValueKind is not JsonValueKind.String ||
                        (expectedType = expectedTypeElement.GetString()) is null ||
                        !s_valueTypes.Contains(expectedType))
                    {
                        return Fail("invalid_expected_type", $"{path}.expectedType", out error);
                    }
                }
                else if (hasExpected || hasExpectedType)
                {
                    return Fail("unexpected_check_operand", path, out error);
                }

                var checkOutcome = Evaluate(
                    checkOperator,
                    actualVariable,
                    expectedVariable,
                    expectedType);
                checks.Add(new DiagnosticCheck(
                    checkId,
                    checkOperator,
                    actualName,
                    expectedName,
                    expectedType,
                    checkOutcome));
            }
        }

        var failedChecks = checks.Count(static check => check.Outcome == "fail");
        var unknownChecks = checks.Count(static check => check.Outcome == "unknown");
        var outcome = checks.Count == 0
            ? "not_evaluated"
            : failedChecks > 0
                ? "fail"
                : unknownChecks > 0
                    ? "unknown"
                    : "pass";

        var semanticPayload = WritePayload(
            snapshotId,
            probeId,
            phase,
            outcome,
            variables,
            checks,
            captureNonce: null,
            redact: false);
        var payloadDigest = protector.KeyedDigest(semanticPayload);
        CryptographicOperations.ZeroMemory(semanticPayload);
        var captureNonce = RandomNumberGenerator.GetBytes(16);
        var capturedPayload = WritePayload(
            snapshotId,
            probeId,
            phase,
            outcome,
            variables,
            checks,
            Convert.ToHexStringLower(captureNonce),
            redact: true);
        foreach (var variable in variables)
            CryptographicOperations.ZeroMemory(variable.CanonicalValue);
        if (capturedPayload.Length > MaxCapturedBytes)
            return Fail("payload_too_large", "arguments", out error);

        var contentHash = Convert.ToHexStringLower(SHA256.HashData(capturedPayload));
        var chunk = new WorkflowContentChunk
        {
            ContentRef = new WorkflowContentRef($"sha256:{contentHash}"),
            ContentType = "application/json",
            Encoding = WorkflowContentEncoding.Utf8,
            Content = Encoding.UTF8.GetString(capturedPayload)
        };

        request = new DiagnosticSnapshotInboxRequest(
            active.RunId,
            snapshotId,
            probeId,
            phase,
            outcome,
            variables.Count,
            checks.Count,
            failedChecks,
            payloadDigest,
            submittedAt,
            chunk);
        return true;
    }

    private static string Evaluate(
        string checkOperator,
        DiagnosticVariable? actual,
        DiagnosticVariable? expected,
        string? expectedType)
    {
        if (checkOperator == "exists")
            return actual is not null && actual.Value.ValueKind is not JsonValueKind.Null ? "pass" : "fail";
        if (actual is null)
            return "unknown";
        return checkOperator switch
        {
            "type_is" => actual.ValueType == expectedType ? "pass" : "fail",
            "equal" => Equality(actual, expected, negate: false),
            "not_equal" => Equality(actual, expected, negate: true),
            "contains" => expected is null ? "unknown" : Contains(actual.Value, expected.Value),
            "less_than" => expected is null
                ? "unknown"
                : CompareNumbers(actual.Value, expected.Value, lessThan: true),
            "greater_than" => expected is null
                ? "unknown"
                : CompareNumbers(actual.Value, expected.Value, lessThan: false),
            _ => "unknown"
        };
    }

    private static string Equality(
        DiagnosticVariable actual,
        DiagnosticVariable? expected,
        bool negate)
    {
        if (expected is null)
            return "unknown";
        var bothNumeric = actual.ValueType is "integer" or "number" &&
                          expected.ValueType is "integer" or "number";
        if (actual.ValueType != expected.ValueType && !bothNumeric)
            return "unknown";

        bool equal;
        if (bothNumeric &&
            decimal.TryParse(actual.Value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
            decimal.TryParse(expected.Value.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var right))
        {
            equal = left == right;
        }
        else if (bothNumeric)
        {
            return "unknown";
        }
        else
        {
            equal = JsonElement.DeepEquals(actual.Value, expected.Value);
        }
        return equal != negate ? "pass" : "fail";
    }

    private static string Contains(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind is JsonValueKind.String && expected.ValueKind is JsonValueKind.String)
        {
            return actual.GetString()!.Contains(expected.GetString()!, StringComparison.Ordinal)
                ? "pass"
                : "fail";
        }
        if (actual.ValueKind is JsonValueKind.Array)
        {
            return actual.EnumerateArray().Any(item => JsonElement.DeepEquals(item, expected))
                ? "pass"
                : "fail";
        }
        return "unknown";
    }

    private static string CompareNumbers(JsonElement actual, JsonElement expected, bool lessThan)
    {
        if (actual.ValueKind is not JsonValueKind.Number ||
            expected.ValueKind is not JsonValueKind.Number ||
            !decimal.TryParse(actual.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var left) ||
            !decimal.TryParse(expected.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var right))
        {
            return "unknown";
        }
        return lessThan ? left < right ? "pass" : "fail" : left > right ? "pass" : "fail";
    }

    private static byte[] WritePayload(
        string snapshotId,
        string probeId,
        string phase,
        string outcome,
        IReadOnlyList<DiagnosticVariable> variables,
        IReadOnlyList<DiagnosticCheck> checks,
        string? captureNonce,
        bool redact)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("extension_id", ExtensionId);
        writer.WriteNumber("format_version", FormatVersion);
        if (captureNonce is not null)
            writer.WriteString("capture_nonce", captureNonce);
        writer.WriteString("snapshot_id", snapshotId);
        writer.WriteString("probe_id", probeId);
        writer.WriteString("phase", phase);
        writer.WriteString("outcome", outcome);
        writer.WritePropertyName("variables");
        writer.WriteStartArray();
        foreach (var variable in variables.OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("name", variable.Name);
            writer.WriteString("classification", variable.Classification);
            writer.WriteString("type", variable.ValueType);
            if (!redact || variable.Classification is "public" or "internal")
            {
                writer.WriteString("capture", "value");
                writer.WritePropertyName("value");
                writer.WriteRawValue(variable.CanonicalValue, skipInputValidation: true);
            }
            else
            {
                writer.WriteString(
                    "capture",
                    variable.Classification == "sensitive" ? "redacted" : "omitted");
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WritePropertyName("checks");
        writer.WriteStartArray();
        foreach (var check in checks.OrderBy(static item => item.CheckId, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("check_id", check.CheckId);
            writer.WriteString("operator", check.Operator);
            writer.WriteString("actual", check.Actual);
            if (check.Expected is not null)
                writer.WriteString("expected", check.Expected);
            if (check.ExpectedType is not null)
                writer.WriteString("expected_type", check.ExpectedType);
            writer.WriteString("outcome", check.Outcome);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static byte[] CanonicalJson(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        WriteCanonicalValue(writer, value);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = value.EnumerateObject().ToArray();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in properties.OrderBy(static item => item.Name, StringComparer.Ordinal))
                {
                    if (!names.Add(property.Name))
                        throw new InvalidDataException("Diagnostic JSON values cannot contain duplicate properties.");
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalValue(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonicalValue(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException("Diagnostic values must be closed JSON values.");
        }
    }

    private static int JsonDepth(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => 1 + value.EnumerateObject()
                .Select(static property => JsonDepth(property.Value))
                .DefaultIfEmpty(0)
                .Max(),
            JsonValueKind.Array => 1 + value.EnumerateArray()
                .Select(static item => JsonDepth(item))
                .DefaultIfEmpty(0)
                .Max(),
            _ => 0
        };
    }

    private static string ValueType(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => "null",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Number when value.TryGetInt64(out _) => "integer",
        JsonValueKind.Number => "number",
        JsonValueKind.String => "string",
        JsonValueKind.Object or JsonValueKind.Array => "json",
        _ => throw new InvalidDataException("Diagnostic values must be closed JSON values.")
    };

    private static bool HasOnlyProperties(
        JsonElement element,
        string[] allowed,
        out string invalidProperty)
    {
        invalidProperty = "";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !seen.Add(property.Name))
            {
                invalidProperty = property.Name;
                return false;
            }
        }
        return true;
    }

    private static bool TryIdentifier(JsonElement value, string name, out string result)
    {
        result = "";
        return TryRequiredString(value, name, out var candidate) &&
               IsMachineIdentifier(candidate, out result);
    }

    private static bool IsMachineIdentifier(string? candidate, out string result)
    {
        result = candidate ?? "";
        if (candidate is null || candidate.Length is 0 or > MaxIdentifierLength ||
            !(IsAsciiLetterOrDigit(candidate[0]) || candidate[0] == '_'))
        {
            return false;
        }
        return candidate.All(static character =>
            IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or ':' or '/' or '-' or '[' or ']');
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static bool TryRequiredString(JsonElement value, string name, out string result)
    {
        result = "";
        return value.TryGetProperty(name, out var property) &&
               property.ValueKind is JsonValueKind.String &&
               (result = property.GetString()!) is not null;
    }

    private static bool Fail(
        string code,
        string field,
        out DiagnosticSnapshotValidationError error)
    {
        error = new DiagnosticSnapshotValidationError(code, field);
        return false;
    }

    private sealed record DiagnosticVariable(
        string Name,
        string Classification,
        string ValueType,
        JsonElement Value,
        byte[] CanonicalValue);

    private sealed record DiagnosticCheck(
        string CheckId,
        string Operator,
        string Actual,
        string? Expected,
        string? ExpectedType,
        string Outcome);
}

internal readonly record struct DiagnosticSnapshotValidationError(string Code, string Field);
