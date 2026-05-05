#nullable enable

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSpec.Helpers.JsonConverters
{
    public class NumericConstraintAttribute<T> : JsonConverterAttribute where T : struct, INumber<T>
    {

        T? _minValue = null, _maxValue = null;
        public NumericConstraintAttribute()
        {
        }

        public T MinValue { get { return _minValue.HasValue ? _minValue.Value : default(T); } set { _minValue = value; } }
        public T MaxValue { get { return _maxValue.HasValue ? _maxValue.Value : default(T); } set { _maxValue = value; } }
        public bool MinValueExclusive { get; set; }
        public bool MaxValueExclusive { get; set; }

        public override JsonConverter? CreateConverter(Type typeToConvert)
        {
            return new NumericJsonConverter<T>(_minValue, _maxValue, MinValueExclusive, MaxValueExclusive);
        }
    }

    public class NumericJsonConverter<T> : JsonConverter<T> where T : struct, INumber<T>
    {
        string _rangeString;
        public NumericJsonConverter(T? minValue = null, T? maxValue = null, bool? minValueExclusive = false, bool? maxValueExclusive = false, JsonSerializerOptions? options = null)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            MinValueExclusive = minValueExclusive.HasValue ? minValueExclusive.Value : false;
            MaxValueExclusive = maxValueExclusive.HasValue ? maxValueExclusive.Value : false;
            _rangeString = $"{(MinValue.HasValue ? (MinValueExclusive ? $"({MinValue}" : $"[{MinValue}") : $"[{typeof(T).Name}.Min")}, {(MaxValue.HasValue ? (MaxValueExclusive ? $"{MaxValue})" : $"{MaxValue}]") : $"{typeof(T).Name}.Max]")}";
            if (options != null)
            {
                InnerConverter = options.GetConverter(typeof(T)) as JsonConverter<T>;
            }
        }

        protected T? MinValue { get; }
        protected bool MinValueExclusive { get; }
        protected T? MaxValue { get; }

        protected bool MaxValueExclusive { get; }

        private JsonConverter<T>? InnerConverter { get; set; }

        private JsonConverter<T> GetInnerConverter(JsonSerializerOptions options)
        {
            if (InnerConverter == null)
            {
                InnerConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
            }

            return InnerConverter;
        }
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var inner = GetInnerConverter(options);
            T candidate = inner.Read(ref reader, typeToConvert, options);
            ValidateRange(candidate);
            return candidate;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            ValidateRange(value);
            GetInnerConverter(options).Write(writer, value, options);
        }

        protected virtual void ValidateRange(T value)
        {
            if ((MinValue.HasValue && (value < MinValue.Value || (value == MinValue.Value && MinValueExclusive)))
                || (MaxValue.HasValue && (value > MaxValue.Value || (value == MaxValue.Value && MaxValueExclusive))))
                throw new JsonException($"{value} is outside the allowed range of {_rangeString}");
        }
    }
}
