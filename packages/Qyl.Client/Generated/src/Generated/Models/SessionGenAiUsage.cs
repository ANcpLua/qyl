
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Qyl.Domains.Observe.Session
{
    public partial class SessionGenAiUsage
    {
        private protected readonly IDictionary<string, BinaryData> _additionalBinaryDataProperties;

        internal SessionGenAiUsage(int requestCount, long totalInputTokens, long totalOutputTokens, IEnumerable<string> modelsUsed, IEnumerable<string> providersUsed)
        {
            RequestCount = requestCount;
            TotalInputTokens = totalInputTokens;
            TotalOutputTokens = totalOutputTokens;
            ModelsUsed = modelsUsed.ToList();
            ProvidersUsed = providersUsed.ToList();
        }

        internal SessionGenAiUsage(int requestCount, long totalInputTokens, long totalOutputTokens, IList<string> modelsUsed, IList<string> providersUsed, double? estimatedCostUsd, IDictionary<string, BinaryData> additionalBinaryDataProperties)
        {
            RequestCount = requestCount;
            TotalInputTokens = totalInputTokens;
            TotalOutputTokens = totalOutputTokens;
            ModelsUsed = modelsUsed;
            ProvidersUsed = providersUsed;
            EstimatedCostUsd = estimatedCostUsd;
            _additionalBinaryDataProperties = additionalBinaryDataProperties;
        }

        public int RequestCount { get; }

        public long TotalInputTokens { get; }

        public long TotalOutputTokens { get; }

        public IList<string> ModelsUsed { get; }

        public IList<string> ProvidersUsed { get; }

        public double? EstimatedCostUsd { get; }
    }
}
