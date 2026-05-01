// =============================================================================
// qyl Build System - GenAI Pricing Mirror
// =============================================================================
// nuke UpdatePricing — pull LiteLLM model_prices_and_context_window.json,
// hash it, log provenance, write the snapshot consumed by QylPricingTable.
// =============================================================================

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Serilog;

namespace Qyl.Build;

interface IPricing : IHazSourcePaths
{
    AbsolutePath PricingDirectory => RootDirectory / "eng" / "pricing";
    AbsolutePath PricingModelsFile => PricingDirectory / "models.json";
    AbsolutePath PricingProvenanceFile => PricingDirectory / "provenance.jsonl";

    string PricingSourceUrl =>
        "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json";

    Target UpdatePricing => d => d
        .Description("Mirror LiteLLM pricing into eng/pricing/models.json with SHA256 provenance log")
        .Executes(async () =>
        {
            Directory.CreateDirectory(PricingDirectory);

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("qyl-pricing-mirror/1.0");

            Log.Information("UpdatePricing: fetching {Url}", PricingSourceUrl);
            var bytes = await http.GetByteArrayAsync(PricingSourceUrl).ConfigureAwait(false);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            var fetchedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var line = "{\"source_url\":\""
                       + PricingSourceUrl
                       + "\",\"sha256\":\"" + sha
                       + "\",\"fetched_at_utc\":\"" + fetchedAt
                       + "\",\"bytes\":" + bytes.Length.ToString(CultureInfo.InvariantCulture)
                       + "}\n";

            File.WriteAllBytes(PricingModelsFile, bytes);
            File.AppendAllText(PricingProvenanceFile, line, Encoding.UTF8);

            Log.Information("UpdatePricing: wrote {Bytes} bytes (sha256 {Sha}) → {File}",
                bytes.Length, sha, PricingModelsFile);
            Log.Information("UpdatePricing: provenance appended to {File}", PricingProvenanceFile);
        });
}
