
using ANcpLua.Agents.Testing.Conformance.Telemetry;

namespace Qyl.Collector.Tests.Telemetry;

public sealed class QylInstrumentationTelemetryConformance()
    : TelemetryConformanceTests<QylInstrumentationTelemetryFixture>(static () => new());
