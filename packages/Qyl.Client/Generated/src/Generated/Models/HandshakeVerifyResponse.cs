
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Api
{
    public partial class HandshakeVerifyResponse
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal HandshakeVerifyResponse(string accessToken, DateTimeOffset expiresAt, string workspaceId)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            WorkspaceId = workspaceId;
        }

        internal HandshakeVerifyResponse(string accessToken, DateTimeOffset expiresAt, string workspaceId, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            WorkspaceId = workspaceId;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string AccessToken { get; }

        public DateTimeOffset ExpiresAt { get; }

        public string WorkspaceId { get; }
    }
}
