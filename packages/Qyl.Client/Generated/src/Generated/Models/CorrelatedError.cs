
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Error
{
    public partial class CorrelatedError
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal CorrelatedError(string errorId, string errorType, double correlationStrength, TemporalRelationship temporalRelationship)
        {
            ErrorId = errorId;
            ErrorType = errorType;
            CorrelationStrength = correlationStrength;
            TemporalRelationship = temporalRelationship;
        }

        internal CorrelatedError(string errorId, string errorType, double correlationStrength, TemporalRelationship temporalRelationship, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ErrorId = errorId;
            ErrorType = errorType;
            CorrelationStrength = correlationStrength;
            TemporalRelationship = temporalRelationship;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ErrorId { get; }

        public string ErrorType { get; }

        public double CorrelationStrength { get; }

        public TemporalRelationship TemporalRelationship { get; }
    }
}
