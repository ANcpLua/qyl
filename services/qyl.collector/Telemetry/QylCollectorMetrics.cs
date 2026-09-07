using System.Diagnostics.Metrics;
using Qyl.Collector.Ingestion;
using Qyl.Telemetry.SemanticConventions.Incubating.Mapping;
using Qyl.Telemetry.SemanticConventions.Incubating.Metrics;

namespace Qyl.Collector.Telemetry;

/// <summary>
/// The collector's own instruments. One Meter, named by the same scope constant as the
/// ActivitySource, and every instrument built from its registry definition rather than from a
/// name typed here — the name, the unit and the attribute the definition declares are the ones
/// the collector's own ingest catalog recognises.
/// </summary>
internal static class QylCollectorMetrics
{
    private static readonly Meter s_meter = new(QylTelemetry.ServiceName, BuildVersion.InformationalVersion);

    private static readonly Counter<long> s_attributesDropped = s_meter.CreateCounter<long>(
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Name,
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Unit,
        QylIncubatingMetricDefinitions.QylCollectorAttributesDropped.Brief);

    /// <summary>
    /// Records one attribute the ingest allowlist refused, by the namespace its key belongs to.
    /// Without it an undeclared key is indistinguishable from a key nobody ever sent.
    /// </summary>
    internal static void AttributeDropped(string key) =>
        s_attributesDropped.Add(
            1,
            new KeyValuePair<string, object?>(
                CollectorSemanticAttributeCatalog.QylAttributeNamespace,
                AttributeMapping.NamespaceOf(key)));
}
