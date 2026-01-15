// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Utils.Messaging;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class SendMessageController(MessageSender messageSender) : ControllerBase
{
    private readonly MessageSender _messageSender = messageSender;

    [HttpGet]
    public async Task<string> Get() => await _messageSender.SendMessageAsync().ConfigureAwait(false);
}
