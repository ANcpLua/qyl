
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HandshakeStartRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public HandshakeStartRequest(string codeChallenge, string clientId)
        {
            Argument.AssertNotNull(codeChallenge, nameof(codeChallenge));
            Argument.AssertNotNull(clientId, nameof(clientId));

            CodeChallenge = codeChallenge;
            ClientId = clientId;
        }

        internal HandshakeStartRequest(string codeChallenge, string clientId, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            CodeChallenge = codeChallenge;
            ClientId = clientId;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string CodeChallenge { get; }

        public string ClientId { get; }
    }
}
