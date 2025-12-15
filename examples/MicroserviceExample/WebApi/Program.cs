// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<MessageSender>();

// OpenTelemetry is auto-configured by ANcpLua.NET.Sdk ServiceDefaults
// Set OTEL_EXPORTER_OTLP_ENDPOINT env var for OTLP export, otherwise console

builder.WebHost.UseUrls("http://*:5000");

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();

app.UseRouting();

app.MapControllers();

app.Run();