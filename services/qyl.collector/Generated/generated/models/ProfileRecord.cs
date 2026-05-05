
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;

namespace Qyl.Storage
{

    public partial class ProfileRecord
    {
        public string ProfileId { get; set; }

        [StringConstraint(MinLength = 32, MaxLength = 32, Pattern = "^[a-f0-9]{32}$")]
        public string TraceId { get; set; }

        [StringConstraint(MinLength = 16, MaxLength = 16, Pattern = "^[a-f0-9]{16}$")]
        public string SpanId { get; set; }

        [StringConstraint(MinLength = 1, MaxLength = 128)]
        public string SessionId { get; set; }

        public long TimeUnixNano { get; set; }

        public long DurationNano { get; set; }

        public int SampleCount { get; set; }

        public string SampleType { get; set; }

        public string SampleUnit { get; set; }

        public string OriginalPayloadFormat { get; set; }

        public string ServiceName { get; set; }

        public string ProfileFrameType { get; set; }

        public string AttributesJson { get; set; }

        public string ResourceJson { get; set; }

        public string ProfileDataJson { get; set; }

        public string SchemaUrl { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }


    }
}
