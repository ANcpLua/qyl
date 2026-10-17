using System.Diagnostics.Metrics;
using Qyl.Telemetry.SemanticConventions.Incubating.Mapping;
using Qyl.Telemetry.SemanticConventions.Incubating.Metrics;
using Qyl.Telemetry.SemanticConventions.Names;

namespace Qyl.Collector.Ingestion;

/// <summary>
/// The collector's own instruments. One Meter, named by the same scope constant as the
/// ActivitySource, and every instrument built from its registry definition rather than from a
/// name typed here — the name, the unit and the attribute the definition declares are the ones
/// the collector's own ingest catalog recognises.
/// </summary>
internal static class QylCollectorMetrics
{
    private static readonly Meter s_meter = new(QylTelemetryNames.Scopes.QylCollector, BuildVersion.InformationalVersion);

    private static readonly Counter<long> s_attributesDropped = s_meter.CreateCounter<long>(
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Name,
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Unit,
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Brief);

    private static readonly Counter<long> s_attributesCoalesced = s_meter.CreateCounter<long>(
        QylIncubatingMetricDefinitions.QylCollectorAttributesCoalesced.Name,
        QylIncubatingMetricDefinitions.QylCollectorAttributesCoalesced.Unit,
        QylIncubatingMetricDefinitions.QylCollectorAttributesCoalesced.Brief);

    private static readonly Counter<long> s_sanitizationFailed = s_meter.CreateCounter<long>(
        QylIncubatingMetricDefinitions.QylCollectorAttributesSanitizationFailed.Name,
        QylIncubatingMetricDefinitions.QylCollectorAttributesSanitizationFailed.Unit,
        QylIncubatingMetricDefinitions.QylCollectorAttributesSanitizationFailed.Brief);

    /// <summary>
    /// Records an explicit persistence-policy denial by a bounded attribute namespace.
    /// </summary>
    internal static void AttributeDropped(string key) =>
        s_attributesDropped.Add(
            1,
            new KeyValuePair<string, object?>(
                CollectorSemanticAttributeCatalog.QylAttributeNamespace,
                AttributeMapping.NamespaceOf(key)));

    internal static void AttributeCoalesced(string key) =>
        s_attributesCoalesced.Add(1, new KeyValuePair<string, object?>(
            CollectorSemanticAttributeCatalog.QylAttributeNamespace, AttributeMapping.NamespaceOf(key)));

    internal static void AttributeSanitizationFailed(string canonicalKey, string category) =>
        s_sanitizationFailed.Add(1,
            new KeyValuePair<string, object?>(CollectorSemanticAttributeCatalog.QylAttributeKey, canonicalKey),
            new KeyValuePair<string, object?>(CollectorSemanticAttributeCatalog.ErrorType, category));
}
