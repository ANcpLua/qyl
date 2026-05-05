#nullable enable

using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api;
using Qyl.Common;
using Qyl.Common.Errors;
using Qyl.Common.Pagination;
using Qyl.Domains.AI.GenAi;
using Qyl.Domains.Agent.Checkpoint;
using Qyl.Domains.Agent.Run;
using Qyl.Domains.Agent.ToolCall;
using Qyl.Domains.Agent.Workflow;
using Qyl.Domains.Alerting;
using Qyl.Domains.Configurator;
using Qyl.Domains.Data.Db;
using Qyl.Domains.Identity;
using Qyl.Domains.Issues;
using Qyl.Domains.Loom.Triage;
using Qyl.Domains.Observe.Error;
using Qyl.Domains.Observe.Log;
using Qyl.Domains.Observe.Otel;
using Qyl.Domains.Observe.Session;
using Qyl.Domains.Observe.Test;
using Qyl.Domains.Ops.Deployment;
using Qyl.Domains.Ops.Retention;
using Qyl.Domains.Runtime.System;
using Qyl.Domains.Search;
using Qyl.Domains.Transport.Http;
using Qyl.Domains.Transport.Messaging;
using Qyl.Domains.Transport.Rpc;
using Qyl.Domains.Workflow;
using Qyl.Domains.Workspace;
using Qyl.Intelligence;
using Qyl.OTel.Enums;
using Qyl.OTel.Logs;
using Qyl.OTel.Metrics;
using Qyl.OTel.Profiles;
using Qyl.OTel.Resource;
using Qyl.OTel.Traces;
using Qyl.Storage;

namespace Qyl.Api.Controllers
{
    [ApiController]
    public partial class OnboardingApiController : ControllerBase
    {

        public OnboardingApiController(IOnboardingApi operations)
        {
            OnboardingApiImpl = operations;
        }
        internal virtual IOnboardingApi OnboardingApiImpl { get; }

        [HttpPost]
        [Route("/api/v1/onboarding/handshake")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(HandshakeSessionEntity))]
        public virtual async Task<IActionResult> StartHandshake(HandshakeStartRequest body)
        {
            var result = await OnboardingApiImpl.StartHandshakeAsync(body);
            return Ok(result);
        }

        [HttpPost]
        [Route("/api/v1/onboarding/handshake/{sessionId}/verify")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(HandshakeVerifyResponse))]
        public virtual async Task<IActionResult> VerifyHandshake(string sessionId, HandshakeVerifyRequest body)
        {
            var result = await OnboardingApiImpl.VerifyHandshakeAsync(sessionId, body);
            return Ok(result);
        }

        [HttpGet]
        [Route("/api/v1/onboarding/handshake/{sessionId}")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(HandshakeSessionEntity))]
        public virtual async Task<IActionResult> GetHandshake(string sessionId)
        {
            var result = await OnboardingApiImpl.GetHandshakeAsync(sessionId);
            return Ok(result);
        }

    }
}
