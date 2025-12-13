// Example Web API with OpenTelemetry
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello from qyl example!");

app.Run();
