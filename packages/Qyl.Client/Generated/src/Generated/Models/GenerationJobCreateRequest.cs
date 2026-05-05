
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;
using Qyl.Domains.Configurator;

namespace Qyl.Api
{
    public partial class GenerationJobCreateRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public GenerationJobCreateRequest(string workspaceId, string profileId, GenerationJobType jobType)
        {
            Argument.AssertNotNull(workspaceId, nameof(workspaceId));
            Argument.AssertNotNull(profileId, nameof(profileId));

            WorkspaceId = workspaceId;
            ProfileId = profileId;
            JobType = jobType;
        }

        internal GenerationJobCreateRequest(string workspaceId, string profileId, GenerationJobType jobType, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            WorkspaceId = workspaceId;
            ProfileId = profileId;
            JobType = jobType;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string WorkspaceId { get; }

        public string ProfileId { get; }

        public GenerationJobType JobType { get; }
    }
}
