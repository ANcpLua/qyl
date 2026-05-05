
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Alerting;

namespace Qyl.Common.Pagination
{

    public partial class CursorPageAlertFiringEntity
    {
        public AlertFiringEntity[] Items { get; set; }

        [JsonPropertyName("next_cursor")]
        public string NextCursor { get; set; }

        [JsonPropertyName("prev_cursor")]
        public string PrevCursor { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }


    }
}
