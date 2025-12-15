// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WorkerService;

internal sealed class Worker(MessageReceiver messageReceiver) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        await messageReceiver.StartConsumerAsync().ConfigureAwait(false);
    }
}