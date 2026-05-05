
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workspace
{
    public partial class ProjectEnvironmentEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ProjectEnvironmentEntity(string id, string projectId, string name, string displayName, int sortOrder, DateTimeOffset createdAt)
        {
            Id = id;
            ProjectId = projectId;
            Name = name;
            DisplayName = displayName;
            SortOrder = sortOrder;
            CreatedAt = createdAt;
        }

        internal ProjectEnvironmentEntity(string id, string projectId, string name, string displayName, string color, int sortOrder, DateTimeOffset createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            ProjectId = projectId;
            Name = name;
            DisplayName = displayName;
            Color = color;
            SortOrder = sortOrder;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string ProjectId { get; }

        public string Name { get; }

        public string DisplayName { get; }

        public string Color { get; }

        public int SortOrder { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
