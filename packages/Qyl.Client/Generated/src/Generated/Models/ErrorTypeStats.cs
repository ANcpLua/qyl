
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorTypeStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorTypeStats(string errorType, long count, double percentage, ErrorStatus status)
        {
            ErrorType = errorType;
            Count = count;
            Percentage = percentage;
            Status = status;
        }

        internal ErrorTypeStats(string errorType, long count, double percentage, long? affectedUsers, ErrorStatus status, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            ErrorType = errorType;
            Count = count;
            Percentage = percentage;
            AffectedUsers = affectedUsers;
            Status = status;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public string ErrorType { get; }

        public long Count { get; }

        public double Percentage { get; }

        public long? AffectedUsers { get; }

        public ErrorStatus Status { get; }
    }
}
