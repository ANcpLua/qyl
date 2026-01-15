// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Utils.Messaging;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MessageReceiver>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource(nameof(MessageReceiver));
        tracing.AddOtlpExporter();
    });

var app = builder.Build();

app.Run();