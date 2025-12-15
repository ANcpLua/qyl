// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Examples.GrpcService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

// OpenTelemetry is auto-configured by ANcpLua.NET.Sdk ServiceDefaults
// No manual setup required!

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseRouting();

app.MapGrpcService<GreeterService>();
app.MapGet("/", () =>
{
    return Results.Text("Communication with gRPC endpoints must be made through a gRPC client. " +
                      "To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
});

app.Run();
