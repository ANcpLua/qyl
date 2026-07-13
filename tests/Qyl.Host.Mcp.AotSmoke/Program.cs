using Qyl.Host;
using Qyl.Host.Mcp;

// AOT smoke: compose every Qyl.Host.Mcp resource kind so the trimmer/AOT compiler
// walks the full package surface (handshake probe, registry, passthrough handler,
// official-SDK OTLP telemetry wiring), then Build() to exercise DI graph creation.
var app = QylAppBuilder.Create(args);
app.AddMcpStdio("smoke-stdio", "cat");
app.AddMcpHttp("smoke-http", new Uri("http://127.0.0.1:9/mcp"));
var built = app.Build();
Console.WriteLine($"composed: {built.GetType().FullName}");
return 0;
