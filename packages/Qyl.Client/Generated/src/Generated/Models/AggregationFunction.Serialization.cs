
#nullable disable

using System;

namespace Qyl.OTel.Metrics
{
    internal static partial class AggregationFunctionExtensions
    {
        public static string ToSerialString(this AggregationFunction value) => value switch
        {
            AggregationFunction.Sum => "sum",
            AggregationFunction.Avg => "avg",
            AggregationFunction.Min => "min",
            AggregationFunction.Max => "max",
            AggregationFunction.Count => "count",
            AggregationFunction.Last => "last",
            AggregationFunction.Rate => "rate",
            AggregationFunction.Increase => "increase",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AggregationFunction value.")
        };

        public static AggregationFunction ToAggregationFunction(this string value)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "sum"))
            {
                return AggregationFunction.Sum;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "avg"))
            {
                return AggregationFunction.Avg;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "min"))
            {
                return AggregationFunction.Min;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "max"))
            {
                return AggregationFunction.Max;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "count"))
            {
                return AggregationFunction.Count;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "last"))
            {
                return AggregationFunction.Last;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "rate"))
            {
                return AggregationFunction.Rate;
            }
            if (StringComparer.OrdinalIgnoreCase.Equals(value, "increase"))
            {
                return AggregationFunction.Increase;
            }
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown AggregationFunction value.");
        }
    }
}
