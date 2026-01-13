// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Utils.Messaging;

public sealed class MessageSender(ILogger<MessageSender> logger) : IDisposable
{
    private static readonly ActivitySource ActivitySource = new(nameof(MessageSender));
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;
    private readonly ILogger<MessageSender> _logger = logger;
    private IChannel? _channel;

    private IConnection? _connection;

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }

    public async Task<string> SendMessageAsync()
    {
        try
        {
            if (_channel is null)
            {
                _connection = await RabbitMqHelper.CreateConnectionAsync().ConfigureAwait(false);
                _channel = await RabbitMqHelper.CreateModelAndDeclareTestQueueAsync(_connection).ConfigureAwait(false);
            }

            // Start an activity with a name following the semantic convention of the OpenTelemetry messaging specification.
            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/messaging/messaging-spans.md#span-name
            const string activityName = $"{RabbitMqHelper.TestQueueName} send";

            using var activity = ActivitySource.StartActivity(activityName, ActivityKind.Producer);
            var props = new BasicProperties();

            // Depending on Sampling (and whether a listener is registered or not), the
            // activity above may not be created.
            // If it is created, then propagate its context.
            // If it is not created, the propagate the Current context,
            // if any.
            ActivityContext contextToInject = default;
            if (activity != null)
                contextToInject = activity.Context;
            else if (Activity.Current != null) contextToInject = Activity.Current.Context;

            // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
            Propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), props,
                InjectTraceContextIntoBasicProperties);

            // The OpenTelemetry messaging specification defines a number of attributes. These attributes are added here.
            RabbitMqHelper.AddMessagingTags(activity);
            var body = $"Published message: DateTime.Now = {DateTime.Now}.";

            await _channel.BasicPublishAsync(
                RabbitMqHelper.DefaultExchangeName,
                RabbitMqHelper.TestQueueName,
                false,
                props,
                Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);

            _logger.MessageSent(body);

            return body;
        }
        catch (Exception ex)
        {
            _logger.MessagePublishingFailed(ex);
            throw;
        }
    }

    private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object?>();

            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            _logger.FailedToInjectTraceContext(ex);
        }
    }
}