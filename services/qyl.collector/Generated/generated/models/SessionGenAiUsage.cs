
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Domains.Observe.Session
{

    public partial class SessionGenAiUsage
    {
        [NumericConstraint<int>(MinValue = 0)]
        [JsonPropertyName("request_count")]
        public int RequestCount { get; set; }

        [JsonPropertyName("total_input_tokens")]
        public long TotalInputTokens { get; set; }

        [JsonPropertyName("total_output_tokens")]
        public long TotalOutputTokens { get; set; }

        [JsonPropertyName("models_used")]
        public string[] ModelsUsed { get; set; }

        [JsonPropertyName("providers_used")]
        public string[] ProvidersUsed { get; set; }

        [JsonPropertyName("estimated_cost_usd")]
        public double? EstimatedCostUsd { get; set; }


    }
}
