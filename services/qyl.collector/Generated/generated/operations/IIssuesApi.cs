#nullable enable

using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Issues;
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
using IssueStatus = Qyl.Domains.Issues.IssueStatus;

namespace Qyl.Api
{

    public interface IIssuesApi
    {
        Task<CursorPageErrorIssueEntity> ListAsync(string? projectId, IssueStatus? status, IssuePriority? priority, IssueLevel? level, DateTimeOffset? startTime, DateTimeOffset? endTime, int? limit, string? cursor);
        Task<ErrorIssueEntity> GetNameAsync(string issueId);
        Task<ErrorIssueEntity> UpdateAsync(string issueId, IssueUpdateRequest body);
        Task<CursorPageErrorIssueEventEntity> GetEventsAsync(string issueId, int? limit, string? cursor);
        Task<ErrorBreadcrumbEntity[]> GetBreadcrumbsAsync(string issueId, int? limit);

    }
}
