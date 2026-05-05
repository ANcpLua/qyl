
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Issues
{
    public partial class ErrorIssueEventEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorIssueEventEntity(string id, string issueId, DateTimeOffset timestamp)
        {
            Id = id;
            IssueId = issueId;
            Timestamp = timestamp;
        }

        internal ErrorIssueEventEntity(string id, string issueId, string traceId, string spanId, string message, string stackTrace, string stackFramesJson, string environment, string releaseVersion, string userId, string userIp, string requestUrl, string requestMethod, string browser, string os, string device, string runtime, string runtimeVersion, string contextJson, string tagsJson, DateTimeOffset timestamp, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            IssueId = issueId;
            TraceId = traceId;
            SpanId = spanId;
            Message = message;
            StackTrace = stackTrace;
            StackFramesJson = stackFramesJson;
            Environment = environment;
            ReleaseVersion = releaseVersion;
            UserId = userId;
            UserIp = userIp;
            RequestUrl = requestUrl;
            RequestMethod = requestMethod;
            Browser = browser;
            Os = os;
            Device = device;
            Runtime = runtime;
            RuntimeVersion = runtimeVersion;
            ContextJson = contextJson;
            TagsJson = tagsJson;
            Timestamp = timestamp;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string IssueId { get; }

        public string TraceId { get; }

        public string SpanId { get; }

        public string Message { get; }

        public string StackTrace { get; }

        public string StackFramesJson { get; }

        public string Environment { get; }

        public string ReleaseVersion { get; }

        public string UserId { get; }

        public string UserIp { get; }

        public string RequestUrl { get; }

        public string RequestMethod { get; }

        public string Browser { get; }

        public string Os { get; }

        public string Device { get; }

        public string Runtime { get; }

        public string RuntimeVersion { get; }

        public string ContextJson { get; }

        public string TagsJson { get; }

        public DateTimeOffset Timestamp { get; }
    }
}
