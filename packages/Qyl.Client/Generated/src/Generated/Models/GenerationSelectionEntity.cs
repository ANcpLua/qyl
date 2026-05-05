
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Configurator
{
    public partial class GenerationSelectionEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal GenerationSelectionEntity(string id, string workspaceId, string profileId, string selectionType, string selectionKey, bool enabled, DateTimeOffset createdAt, DateTimeOffset updatedAt)
        {
            Id = id;
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            SelectionType = selectionType;
            SelectionKey = selectionKey;
            Enabled = enabled;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        internal GenerationSelectionEntity(string id, string workspaceId, string profileId, string selectionType, string selectionKey, bool enabled, string configJson, DateTimeOffset createdAt, DateTimeOffset updatedAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            SelectionType = selectionType;
            SelectionKey = selectionKey;
            Enabled = enabled;
            ConfigJson = configJson;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string WorkspaceId { get; }

        public string ProfileId { get; }

        public string SelectionType { get; }

        public string SelectionKey { get; }

        public bool Enabled { get; }

        public string ConfigJson { get; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; }
    }
}
