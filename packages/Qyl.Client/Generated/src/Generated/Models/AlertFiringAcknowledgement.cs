
#nullable disable

using System;
using System.Collections.Generic;
using Qyl.Client;

namespace Qyl.Api
{
    public partial class AlertFiringAcknowledgement
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        public AlertFiringAcknowledgement(string acknowledgedBy)
        {
            Argument.AssertNotNull(acknowledgedBy, nameof(acknowledgedBy));

            AcknowledgedBy = acknowledgedBy;
        }

        internal AlertFiringAcknowledgement(string acknowledgedBy, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            AcknowledgedBy = acknowledgedBy;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string AcknowledgedBy { get; }
    }
}
