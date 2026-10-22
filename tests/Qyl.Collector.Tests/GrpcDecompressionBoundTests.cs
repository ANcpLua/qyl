using System.IO.Compression;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Grpc.Net.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qyl.Collector.Grpc;
using Qyl.Collector.Hosting;

namespace Qyl.Collector.Tests;

// grpc-dotnet decompresses a request message into an unbounded buffer before any interceptor runs,
// so the API-key interceptor cannot stop a small compressed payload that expands without limit.
// These tests pin the collector's bound: every decompression provider stops at the receive limit.
[TestClass]
public sealed class GrpcDecompressionBoundTests
{
    private static GrpcServiceOptions CollectorGrpcOptions()
    {
        var services = new ServiceCollection();
        services.AddQylCollectorCore(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    public void The_collector_registers_a_bounded_decompression_provider_for(string encoding)
    {
        var registered = Assert.ContainsSingle(p => p.EncodingName == encoding,CollectorGrpcOptions().CompressionProviders);
        Assert.IsExactInstanceOfType<BoundedDecompressionProvider>(registered);
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    public void A_compression_bomb_fails_with_resource_exhausted(string encoding)
    {
        var options = CollectorGrpcOptions();
        var provider = options.CompressionProviders.Single(p => p.EncodingName == encoding);
        var limit = options.MaxReceiveMessageSize ?? BoundedDecompressionProvider.DefaultMaxReceiveMessageSize;

        // Zeros compress about a thousandfold: 16 times the limit arrives as a small message.
        var bomb = Compress(provider, new byte[limit * 16]);
        Assert.IsLessThan(limit / 16, bomb.Length, $"the bomb should be small on the wire, was {bomb.Length} bytes");

        using var stream = provider.CreateDecompressionStream(new MemoryStream(bomb));
        var ex = Assert.ThrowsExactly<RpcException>(() => stream.CopyTo(Stream.Null));
        Assert.AreEqual(StatusCode.ResourceExhausted, ex.StatusCode);
    }

    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    public void A_message_at_the_limit_decompresses_unchanged(string encoding)
    {
        var options = CollectorGrpcOptions();
        var provider = options.CompressionProviders.Single(p => p.EncodingName == encoding);
        var limit = options.MaxReceiveMessageSize ?? BoundedDecompressionProvider.DefaultMaxReceiveMessageSize;

        var payload = new byte[limit];
        new Random(42).NextBytes(payload);

        using var stream = provider.CreateDecompressionStream(new MemoryStream(Compress(provider, payload)));
        using var output = new MemoryStream();
        stream.CopyTo(output);
        Assert.AreSequenceEqual(payload, output.ToArray());
    }

    private static byte[] Compress(ICompressionProvider provider, byte[] payload)
    {
        using var output = new MemoryStream();
        using (var compression = provider.CreateCompressionStream(output, CompressionLevel.Optimal))
            compression.Write(payload);
        return output.ToArray();
    }
}
