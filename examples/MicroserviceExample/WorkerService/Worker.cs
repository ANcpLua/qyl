// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WorkerService;

internal sealed class Worker(MessageReceiver messageReceiver) : BackgroundService
{
    private readonly MessageReceiver _messageReceiver = messageReceiver;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        await _messageReceiver.StartConsumerAsync().ConfigureAwait(false);
    }
}