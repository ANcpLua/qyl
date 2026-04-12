// ROOT CAUSE: gen_ai.client.token.usage histogram is manually written in both
// GenAiInstrumentation.cs:142-144 AND InstrumentedChatClient.cs:47-49.
// Duplicated, not generated. Violates "fix the generator input, never the output."
//
// The metric IS defined in qyl-extensions.json as a MetricSpec:
//   new MetricSpec("gen_ai.client.token.usage", "histogram", "token")
// ContractGenerator emits it into DomainContracts.g.cs.
// But no Roslyn generator reads DomainContracts and emits the Histogram<long>.
//
// Fix: MeterEmitter or a new DomainMetricEmitter that reads DomainContracts.GenAi
// and emits the histogram creation + recording code, replacing the hand-written
// static fields in GenAiInstrumentation and InstrumentedChatClient.

namespace Qyl.Instrumentation.Generators.Emitters;

// When implemented: reads DomainContracts.g.cs MetricDef[] at compile time,
// emits Histogram/Counter creation for each defined metric instrument.
// Consumers get generated Record* methods instead of hand-wiring histograms.
