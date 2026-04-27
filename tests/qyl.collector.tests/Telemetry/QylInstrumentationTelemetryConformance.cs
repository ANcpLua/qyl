// Copyright (c) 2025-2026 ancplua

using ANcpLua.Agents.Testing.Conformance.Telemetry;

namespace Qyl.Collector.Tests.Telemetry;

/// <summary>
///     Provider-agnostic telemetry conformance for qyl's
///     <c>UseQylTelemetry</c> + <c>UseQylAgentTelemetry</c> pipeline. Inherits
///     <see cref="TelemetryConformanceTests{TFixture}" /> with
///     <see cref="QylInstrumentationTelemetryFixture" /> bound, exercising the
///     full chat-client + agent decorator chain through a real
///     <c>AIAgent.RunAsync</c> invocation.
/// </summary>
public sealed class QylInstrumentationTelemetryConformance()
    : TelemetryConformanceTests<QylInstrumentationTelemetryFixture>(static () => new());
