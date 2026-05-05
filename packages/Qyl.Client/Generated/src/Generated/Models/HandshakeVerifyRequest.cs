
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class HandshakeVerifyRequest
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public HandshakeVerifyRequest(string codeVerifier, string code)
        {
            Argument.AssertNotNull(codeVerifier, nameof(codeVerifier));
            Argument.AssertNotNull(code, nameof(code));

            CodeVerifier = codeVerifier;
            Code = code;
        }

        internal HandshakeVerifyRequest(string codeVerifier, string code, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            CodeVerifier = codeVerifier;
            Code = code;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string CodeVerifier { get; }

        public string Code { get; }
    }
}
