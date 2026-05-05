
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Issues
{
    public partial class ErrorBreadcrumbEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorBreadcrumbEntity(string id, string eventId, BreadcrumbType breadcrumbType, string level, DateTimeOffset timestamp)
        {
            Id = id;
            EventId = eventId;
            BreadcrumbType = breadcrumbType;
            Level = level;
            Timestamp = timestamp;
        }

        internal ErrorBreadcrumbEntity(string id, string eventId, BreadcrumbType breadcrumbType, string category, string message, string level, string dataJson, DateTimeOffset timestamp, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            EventId = eventId;
            BreadcrumbType = breadcrumbType;
            Category = category;
            Message = message;
            Level = level;
            DataJson = dataJson;
            Timestamp = timestamp;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string EventId { get; }

        public BreadcrumbType BreadcrumbType { get; }

        public string Category { get; }

        public string Message { get; }

        public string Level { get; }

        public string DataJson { get; }

        public DateTimeOffset Timestamp { get; }
    }
}
