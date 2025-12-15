// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Mvc;
using Utils.Messaging;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class SendMessageController(MessageSender messageSender) : ControllerBase
{
    [HttpGet]
    public async Task<string> Get()
    {
        return await messageSender.SendMessageAsync().ConfigureAwait(false);
    }
}