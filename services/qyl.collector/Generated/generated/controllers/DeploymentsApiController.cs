#nullable enable

using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Qyl.Api;
using Qyl.Domains.Ops.Deployment;
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
    public partial class DeploymentsApiController : ControllerBase
    {

        public DeploymentsApiController(IDeploymentsApi operations)
        {
            DeploymentsApiImpl = operations;
        }
        internal virtual IDeploymentsApi DeploymentsApiImpl { get; }

        [HttpGet]
        [Route("/api/v1/deployments")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(CursorPageDeploymentEntity))]
        public virtual async Task<IActionResult> List([FromQuery(Name = "serviceName")] string? serviceName, [FromQuery(Name = "environment")] DeploymentEnvironment? environment, [FromQuery(Name = "status")] DeploymentStatus? status, [FromQuery(Name = "startTime")] DateTimeOffset? startTime, [FromQuery(Name = "endTime")] DateTimeOffset? endTime, [FromQuery(Name = "limit")] int? limit, [FromQuery(Name = "cursor")] string? cursor)
        {
            var result = await DeploymentsApiImpl.ListAsync(serviceName, environment, status, startTime, endTime, limit, cursor);
            return Ok(result);
        }

        [HttpGet]
        [Route("/api/v1/deployments/{deploymentId}")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeploymentEntity))]
        public virtual async Task<IActionResult> GetName(string deploymentId)
        {
            var result = await DeploymentsApiImpl.GetNameAsync(deploymentId);
            return Ok(result);
        }

        [HttpPost]
        [Route("/api/v1/deployments")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeploymentEntity))]
        public virtual async Task<IActionResult> Create(DeploymentCreate body)
        {
            var result = await DeploymentsApiImpl.CreateAsync(body);
            return Ok(result);
        }

        [HttpPatch]
        [Route("/api/v1/deployments/{deploymentId}")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DeploymentEntity))]
        public virtual async Task<IActionResult> Update(string deploymentId, DeploymentUpdate body)
        {
            var result = await DeploymentsApiImpl.UpdateAsync(deploymentId, body);
            return Ok(result);
        }

        [HttpGet]
        [Route("/api/v1/deployments/metrics/dora")]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DoraMetrics))]
        public virtual async Task<IActionResult> GetDoraMetrics([FromQuery(Name = "serviceName")] string? serviceName, [FromQuery(Name = "environment")] DeploymentEnvironment? environment, [FromQuery(Name = "startTime")] DateTimeOffset? startTime, [FromQuery(Name = "endTime")] DateTimeOffset? endTime)
        {
            var result = await DeploymentsApiImpl.GetDoraMetricsAsync(serviceName, environment, startTime, endTime);
            return Ok(result);
        }

    }
}
