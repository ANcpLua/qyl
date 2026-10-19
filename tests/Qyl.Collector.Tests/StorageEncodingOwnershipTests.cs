using System.Text.Json.Serialization;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;
using ContractValue = Qyl.Api.Contracts.Common.AttributeValue;

namespace Qyl.Collector.Tests;

/// <summary>
/// Persisted bytes and identifiers cannot follow the API contract's serialization: storage's own
/// context is the only serializer on the ingestion and storage write path, it resolves no contract
/// type, and every type it reaches from a persisted value is a storage record.
/// </summary>
[TestClass]
public sealed class StorageEncodingOwnershipTests
{
    [TestMethod]
    public void Storage_context_is_the_only_serializer_on_the_write_path()
    {
        var contexts = new[] { typeof(OtlpAttributeValue).Assembly, typeof(StorageJsonSerializerContext).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.IsSubclassOf(typeof(JsonSerializerContext)));

        Assert.AreSequenceEqual([typeof(StorageJsonSerializerContext)], contexts);
    }

    [TestMethod]
    public void Storage_context_resolves_no_contract_type()
    {
        var contractTypes = typeof(ContractValue).Assembly.GetTypes()
            .Where(static type => type.IsVisible && !type.ContainsGenericParameters).ToArray();

        Assert.IsNotEmpty(contractTypes);
        foreach (var type in contractTypes)
            Assert.IsNull(StorageJsonSerializerContext.Default.GetTypeInfo(type), type.FullName);
    }

    [TestMethod]
    public void Every_type_a_persisted_value_reaches_is_a_storage_record()
    {
        var storage = typeof(StorageJsonSerializerContext).Assembly;
        var reached = Reach(typeof(StoredAttributeValue), typeof(StoredObjectValue), typeof(StoredMetricPointData),
            typeof(IReadOnlyList<StoredExemplar>), typeof(IReadOnlyList<StoredAttribute>));

        Type[] records =
        [
            typeof(StoredAttributeValue), typeof(StoredObjectValue), typeof(StoredNumberValue), typeof(StoredIntValue),
            typeof(StoredDoubleValue), typeof(StoredBytesValue), typeof(StoredKeyValueListValue),
            typeof(StoredMetricPointData), typeof(StoredNumberPointData), typeof(StoredHistogramPointData),
            typeof(StoredExponentialHistogramPointData), typeof(StoredExponentialBuckets), typeof(StoredSummaryPointData),
            typeof(StoredQuantileValue), typeof(StoredExemplar), typeof(StoredAttribute)
        ];
        foreach (var record in records)
            Assert.Contains(record, reached, record.Name);
        foreach (var type in reached)
            Assert.IsTrue(type.Assembly == storage || type.Assembly == typeof(object).Assembly,
                $"{type.FullName} from {type.Assembly.GetName().Name} shapes persisted bytes.");
        Assert.IsInstanceOfType<StoredAttributeValueJsonConverter>(StorageJsonSerializerContext.Default.StoredAttributeValue.Converter);
    }

    // Properties, polymorphic shapes, elements and keys: everything serialization of the roots can touch.
    private static HashSet<Type> Reach(params Type[] roots)
    {
        var reached = new HashSet<Type>();
        var pending = new Queue<Type>(roots);
        while (pending.TryDequeue(out var type))
        {
            if (!reached.Add(type)) continue;
            foreach (var argument in type.GetGenericArguments()) pending.Enqueue(argument);
            if (StorageJsonSerializerContext.Default.GetTypeInfo(type) is not { } info) continue;
            foreach (var property in info.Properties) pending.Enqueue(property.PropertyType);
            foreach (var derived in info.PolymorphismOptions?.DerivedTypes ?? []) pending.Enqueue(derived.DerivedType);
            if (info.ElementType is { } element) pending.Enqueue(element);
            if (info.KeyType is { } key) pending.Enqueue(key);
        }
        return reached;
    }
}
