
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Issues
{
    public partial class ErrorIssueEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorIssueEntity(string id, string projectId, string fingerprint, string title, string errorType, string category, IssueLevel level, DateTimeOffset firstSeenAt, DateTimeOffset lastSeenAt, long occurrenceCount, int affectedUsersCount, IssueStatus status, IssuePriority priority, int regressionCount, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            ProjectId = projectId;
            Fingerprint = fingerprint;
            Title = title;
            ErrorType = errorType;
            Category = category;
            Level = level;
            FirstSeenAt = firstSeenAt;
            LastSeenAt = lastSeenAt;
            OccurrenceCount = occurrenceCount;
            AffectedUsersCount = affectedUsersCount;
            Status = status;
            Priority = priority;
            RegressionCount = regressionCount;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal ErrorIssueEntity(string id, string projectId, string fingerprint, string title, string culprit, string errorType, string category, IssueLevel level, string platform, DateTimeOffset firstSeenAt, DateTimeOffset lastSeenAt, long occurrenceCount, int affectedUsersCount, IssueStatus status, string substatus, IssuePriority priority, string assignedTo, DateTimeOffset? resolvedAt, string resolvedBy, int regressionCount, string lastRelease, string tagsJson, string metadataJson, DateTimeOffset createdAt, DateTimeOffset updatedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            ProjectId = projectId;
            Fingerprint = fingerprint;
            Title = title;
            Culprit = culprit;
            ErrorType = errorType;
            Category = category;
            Level = level;
            Platform = platform;
            FirstSeenAt = firstSeenAt;
            LastSeenAt = lastSeenAt;
            OccurrenceCount = occurrenceCount;
            AffectedUsersCount = affectedUsersCount;
            Status = status;
            Substatus = substatus;
            Priority = priority;
            AssignedTo = assignedTo;
            ResolvedAt = resolvedAt;
            ResolvedBy = resolvedBy;
            RegressionCount = regressionCount;
            LastRelease = lastRelease;
            TagsJson = tagsJson;
            MetadataJson = metadataJson;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string ProjectId { get; }

        public string Fingerprint { get; }

        public string Title { get; }

        public string Culprit { get; }

        public string ErrorType { get; }

        public string Category { get; }

        public IssueLevel Level { get; }

        public string Platform { get; }

        public DateTimeOffset FirstSeenAt { get; }

        public DateTimeOffset LastSeenAt { get; }

        public long OccurrenceCount { get; }

        public int AffectedUsersCount { get; }

        public IssueStatus Status { get; }

        public string Substatus { get; }

        public IssuePriority Priority { get; }

        public string AssignedTo { get; }

        public DateTimeOffset? ResolvedAt { get; }

        public string ResolvedBy { get; }

        public int RegressionCount { get; }

        public string LastRelease { get; }

        public string TagsJson { get; }

        public string MetadataJson { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }
    }
}
