
#nullable disable

using System;
using System.Collections.Generic;

namespace Qyl.Domains.Observe.Error
{
    public partial class ErrorCategoryStats
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal ErrorCategoryStats(ErrorCategory category, long count, double percentage)
        {
            Category = category;
            Count = count;
            Percentage = percentage;
        }

        internal ErrorCategoryStats(ErrorCategory category, long count, double percentage, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            Category = category;
            Count = count;
            Percentage = percentage;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public ErrorCategory Category { get; }

        public long Count { get; }

        public double Percentage { get; }
    }
}
