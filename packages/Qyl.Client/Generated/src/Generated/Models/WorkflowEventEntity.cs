
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workflow
{
    public partial class WorkflowEventEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal WorkflowEventEntity(string id, string runId, string eventType, string eventName, long sequenceNumber, DateTimeOffset timestamp)
        {
            Id = id;
            RunId = runId;
            EventType = eventType;
            EventName = eventName;
            SequenceNumber = sequenceNumber;
            Timestamp = timestamp;
        }

        internal WorkflowEventEntity(string id, string runId, string nodeId, string eventType, string eventName, string payloadJson, long sequenceNumber, string source, string correlationId, DateTimeOffset timestamp, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            RunId = runId;
            NodeId = nodeId;
            EventType = eventType;
            EventName = eventName;
            PayloadJson = payloadJson;
            SequenceNumber = sequenceNumber;
            Source = source;
            CorrelationId = correlationId;
            Timestamp = timestamp;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string RunId { get; }

        public string NodeId { get; }

        public string EventType { get; }

        public string EventName { get; }

        public string PayloadJson { get; }

        public long SequenceNumber { get; }

        public string Source { get; }

        public string CorrelationId { get; }

        public DateTimeOffset Timestamp { get; }
    }
}
