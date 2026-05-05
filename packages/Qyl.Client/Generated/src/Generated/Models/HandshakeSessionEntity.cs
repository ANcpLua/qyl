
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Workspace
{
    public partial class HandshakeSessionEntity
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal HandshakeSessionEntity(string id, string workspaceId, string challenge, string challengeMethod, HandshakeState state, DateTimeOffset expiresAt, DateTimeOffset createdAt)
        {
            Id = id;
            WorkspaceId = workspaceId;
            Challenge = challenge;
            ChallengeMethod = challengeMethod;
            State = state;
            ExpiresAt = expiresAt;
            CreatedAt = createdAt;
        }

        internal HandshakeSessionEntity(string id, string workspaceId, string challenge, string challengeMethod, string browserFingerprint, string originUrl, HandshakeState state, DateTimeOffset? verifiedAt, DateTimeOffset expiresAt, DateTimeOffset createdAt, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Id = id;
            WorkspaceId = workspaceId;
            Challenge = challenge;
            ChallengeMethod = challengeMethod;
            BrowserFingerprint = browserFingerprint;
            OriginUrl = originUrl;
            State = state;
            VerifiedAt = verifiedAt;
            ExpiresAt = expiresAt;
            CreatedAt = createdAt;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string Id { get; }

        public string WorkspaceId { get; }

        public string Challenge { get; }

        public string ChallengeMethod { get; }

        public string BrowserFingerprint { get; }

        public string OriginUrl { get; }

        public HandshakeState State { get; }

        public DateTimeOffset? VerifiedAt { get; }

        public DateTimeOffset ExpiresAt { get; }

        public DateTimeOffset CreatedAt { get; }
    }
}
