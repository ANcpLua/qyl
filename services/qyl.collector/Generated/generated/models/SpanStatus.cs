
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.OTel.Enums;

namespace Qyl.OTel.Traces
{

    public partial class SpanStatus
    {
        public SpanStatusCode Code { get; set; }

        public string Message { get; set; }


    }
}
