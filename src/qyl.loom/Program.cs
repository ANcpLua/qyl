using Qyl.Loom;
using Qyl.Loom.Agents;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<CollectorClient>(client =>
{
    var baseUrl = builder.Configuration["QYL_COLLECTOR_URL"] ?? "http://localhost:5100";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddSingleton<AutofixOrchestrator>();
builder.Services.AddHostedService<TriagePipelineService>();
builder.Services.AddHostedService<AutofixAgentService>();
builder.Services.AddHostedService<RegressionDetectionService>();
builder.Services.AddSingleton<LoomGodAnalyzerServer>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "qyl.loom" }));
var loomServer = app.Services.GetRequiredService<LoomGodAnalyzerServer>();
app.MapLoomGodAnalyzerServer(loomServer);
app.Run();
