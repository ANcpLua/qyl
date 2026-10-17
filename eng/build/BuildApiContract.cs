using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;

namespace Qyl.Build;

interface IApiContract : IHasSourcePaths
{
    private static readonly HttpClient s_client = new() { Timeout = TimeSpan.FromMinutes(1) };

    AbsolutePath OpenApiContractFile => ArtifactsDirectory / "api-contract" / "qyl.openapi.json";

    // The NuGet contracts package contains DTOs, but only the matching npm package ships
    // OpenAPI. Read that artifact directly; backend verification needs no Node installation.
    Target RestoreApiContract => d => d
        .Unlisted()
        .Description("Restore the published OpenAPI matching Qyl.Api.Contracts")
        .Executes(async () =>
        {
            const string PackageName = "@ancplua/qyl-api-schema";
            var version = XDocument.Load(RootDirectory / "Version.props")
                .Descendants("QylApiContractsVersion").Single().Value.Trim();
            var metadataUri = new Uri(
                $"https://registry.npmjs.org/@ancplua%2Fqyl-api-schema/{Uri.EscapeDataString(version)}");
            using var metadata = JsonDocument.Parse(await s_client.GetStringAsync(metadataUri));
            if (metadata.RootElement.GetProperty("name").GetString() != PackageName ||
                metadata.RootElement.GetProperty("version").GetString() != version)
                throw new InvalidOperationException($"Registry returned a different contract than {PackageName}@{version}.");

            var distribution = metadata.RootElement.GetProperty("dist");
            var integrity = distribution.GetProperty("integrity").GetString()
                ?? throw new InvalidOperationException("The contract package has no integrity digest.");
            const string Algorithm = "sha512-";
            if (!integrity.StartsWith(Algorithm, StringComparison.Ordinal))
                throw new InvalidOperationException("The contract package must publish SHA-512 integrity.");
            var digest = Convert.FromBase64String(integrity[Algorithm.Length..]);
            var cache = ArtifactsDirectory / "api-contract" / $"{version}.tgz";
            Directory.CreateDirectory(cache.Parent);
            var bytes = File.Exists(cache) ? await File.ReadAllBytesAsync(cache) : [];
            if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(bytes), digest))
            {
                var tarballUri = new Uri(distribution.GetProperty("tarball").GetString()
                    ?? throw new InvalidOperationException("The contract package has no tarball URL."));
                bytes = await s_client.GetByteArrayAsync(tarballUri);
                if (!CryptographicOperations.FixedTimeEquals(SHA512.HashData(bytes), digest))
                    throw new InvalidOperationException($"Integrity mismatch for {PackageName}@{version}.");
                await File.WriteAllBytesAsync(cache, bytes);
            }

            var export = metadata.RootElement.GetProperty("exports").GetProperty("./openapi")
                .GetProperty("default").GetString()
                ?? throw new InvalidOperationException("The contract package does not export OpenAPI.");
            await using var archive = new MemoryStream(bytes);
            await using var gzip = new GZipStream(archive, CompressionMode.Decompress);
            await using var reader = new TarReader(gzip);
            while (await reader.GetNextEntryAsync() is { } entry)
            {
                if (entry.Name != "package/" + export.TrimStart('.', '/')) continue;
                using var contract = await JsonDocument.ParseAsync(entry.DataStream
                    ?? throw new InvalidOperationException("The OpenAPI archive entry has no content."));
                if (contract.RootElement.GetProperty("info").GetProperty("version").GetString() != version)
                    throw new InvalidOperationException("OpenAPI and DTO package versions disagree.");
                await File.WriteAllTextAsync(OpenApiContractFile, contract.RootElement.GetRawText());
                Log.Information("Restored {Package} {Version} OpenAPI with verified integrity", PackageName, version);
                return;
            }

            throw new InvalidOperationException($"{PackageName}@{version} is missing its OpenAPI export.");
        });
}
