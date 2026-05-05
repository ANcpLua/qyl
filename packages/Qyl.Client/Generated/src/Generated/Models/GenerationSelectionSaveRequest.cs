
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class GenerationSelectionSaveRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public GenerationSelectionSaveRequest(string workspaceId, string profileId, string selectedKeysJson)
        {
            Argument.AssertNotNull(workspaceId, nameof(workspaceId));
            Argument.AssertNotNull(profileId, nameof(profileId));
            Argument.AssertNotNull(selectedKeysJson, nameof(selectedKeysJson));

            WorkspaceId = workspaceId;
            ProfileId = profileId;
            SelectedKeysJson = selectedKeysJson;
        }

        internal GenerationSelectionSaveRequest(string workspaceId, string profileId, string selectedKeysJson, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            SelectedKeysJson = selectedKeysJson;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string WorkspaceId { get; }

        public string ProfileId { get; }

        public string SelectedKeysJson { get; }
    }
}
