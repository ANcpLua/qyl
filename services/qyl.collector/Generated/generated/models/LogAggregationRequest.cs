
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TypeSpec.Helpers.JsonConverters;
using TypeSpec.Helpers;
using Qyl.Domains.Observe.Log;

namespace Qyl.Api
{

    public partial class LogAggregationRequest
    {
        public LogQuery Query { get; set; }

        public LogAggregation Aggregation { get; set; }


    }
}
