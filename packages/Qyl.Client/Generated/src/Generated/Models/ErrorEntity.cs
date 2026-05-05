
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorEntity(string errorId, string errorType, string message, ErrorCategory category, string fingerprint, DateTimeOffset firstSeen, DateTimeOffset lastSeen, long occurrenceCount, ErrorStatus status)
        {
            ErrorId = errorId;
            ErrorType = errorType;
            Message = message;
            Category = category;
            Fingerprint = fingerprint;
            FirstSeen = firstSeen;
            LastSeen = lastSeen;
            OccurrenceCount = occurrenceCount;
            AffectedServices = new ChangeTrackingList<string>();
            Status = status;
            SampleTraces = new ChangeTrackingList<string>();
        }

        internal ErrorEntity(string errorId, string errorType, string message, ErrorCategory category, string fingerprint, DateTimeOffset firstSeen, DateTimeOffset lastSeen, long occurrenceCount, long? affectedUsers, IList<string> affectedServices, ErrorStatus status, string assignedTo, string issueUrl, IList<string> sampleTraces, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ErrorId = errorId;
            ErrorType = errorType;
            Message = message;
            Category = category;
            Fingerprint = fingerprint;
            FirstSeen = firstSeen;
            LastSeen = lastSeen;
            OccurrenceCount = occurrenceCount;
            AffectedUsers = affectedUsers;
            AffectedServices = affectedServices;
            Status = status;
            AssignedTo = assignedTo;
            IssueUrl = issueUrl;
            SampleTraces = sampleTraces;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ErrorId { get; }

        public string ErrorType { get; }

        public string Message { get; }

        public ErrorCategory Category { get; }

        public string Fingerprint { get; }

        public DateTimeOffset FirstSeen { get; }

        public DateTimeOffset LastSeen { get; }

        public long OccurrenceCount { get; }

        public long? AffectedUsers { get; }

        public IList<string> AffectedServices { get; }

        public ErrorStatus Status { get; }

        public string AssignedTo { get; }

        public string IssueUrl { get; }

        public IList<string> SampleTraces { get; }
    }
}
