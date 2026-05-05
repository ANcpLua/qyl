
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workspace
{
    public partial class ProjectEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ProjectEntity(string id, string name, string slug, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            Name = name;
            Slug = slug;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal ProjectEntity(string id, string name, string slug, string description, DateTimeOffset createdAt, DateTimeOffset updatedAt, DateTimeOffset? archivedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            Name = name;
            Slug = slug;
            Description = description;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            ArchivedAt = archivedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string Name { get; }

        public string Slug { get; }

        public string Description { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }

        public DateTimeOffset? ArchivedAt { get; }
    }
}
