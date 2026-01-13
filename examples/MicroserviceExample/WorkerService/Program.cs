// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;
using WorkerService;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<MessageReceiver>();

builder.Services.AddOpenTelemetry()
    .WithTracing(static b => b.AddSource(nameof(MessageReceiver)));

var app = builder.Build();

app.Run();