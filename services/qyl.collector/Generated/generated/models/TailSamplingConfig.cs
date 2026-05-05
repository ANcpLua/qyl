
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Api.Streaming
{

    public partial class TailSamplingConfig
    {
        public bool Enabled { get; set; }

        [JsonPropertyName("sample_errors")]
        public bool SampleErrors { get; set; }

        [JsonPropertyName("sample_slow")]
        public bool SampleSlow { get; set; }

        [JsonPropertyName("slow_threshold_ms")]
        public long? SlowThresholdMs { get; set; }

        [NumericConstraint<double>(MinValue = 0, MaxValue = 1)]
        [JsonPropertyName("random_rate")]
        public double RandomRate { get; set; }


    }
}
