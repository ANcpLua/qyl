namespace Qyl.AdGuard.Companion.Diagnostics;

internal sealed class DnsDiagnostics(TextWriter diagnostics)
{
    private static readonly string[] s_adGuardDnsServers =
    [
        "94.140.14.14",
        "94.140.15.15",
        "2a10:50c0::ad1:ff",
        "2a10:50c0::ad2:ff"
    ];

    public async Task<DnsStatusResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new DnsStatusResult(
                Platform: Environment.OSVersion.Platform.ToString(),
                Supported: false,
                UsesAdGuardDns: false,
                EncryptedDnsLikely: false,
                ResolverCount: 0,
                Resolvers: [],
                Notes: ["DNS posture inspection is implemented for macOS first."]);
        }

        var output = await RunScutilAsync(cancellationToken).ConfigureAwait(false);
        var nameservers = ParseNameservers(output);
        var resolverCount = output.Split('\n')
            .Count(static line => line.TrimStart().StartsWith("resolver #", StringComparison.Ordinal));
        var usesAdGuard = nameservers.Any(static address =>
            s_adGuardDnsServers.Contains(address, StringComparer.OrdinalIgnoreCase));
        var encryptedLikely = output.Contains("https", StringComparison.OrdinalIgnoreCase) ||
                              output.Contains("doh", StringComparison.OrdinalIgnoreCase) ||
                              output.Contains("odoh", StringComparison.OrdinalIgnoreCase);

        var notes = new List<string>();
        notes.Add(usesAdGuard
            ? "At least one active resolver matches AdGuard DNS."
            : "No active resolver matched the known AdGuard DNS anycast addresses.");

        if (!encryptedLikely)
            notes.Add("macOS scutil did not expose an obvious encrypted-DNS marker; verify DoH/DoT in System Settings if configured.");

        return new DnsStatusResult(
            Platform: "macOS",
            Supported: true,
            UsesAdGuardDns: usesAdGuard,
            EncryptedDnsLikely: encryptedLikely,
            ResolverCount: resolverCount,
            Resolvers: nameservers.Select(static address => new DnsResolver(
                Address: address,
                IsAdGuard: s_adGuardDnsServers.Contains(address, StringComparer.OrdinalIgnoreCase))).ToArray(),
            Notes: notes.ToArray());
    }

    private async Task<string> RunScutilAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("/usr/sbin/scutil", "--dns")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start /usr/sbin/scutil.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            diagnostics.WriteLine("qyl-adguard-companion dns.status timed out while running scutil --dns.");
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(error))
            diagnostics.WriteLine($"qyl-adguard-companion scutil stderr: {error.Trim()}");

        return output;
    }

    private static string[] ParseNameservers(string output)
    {
        var addresses = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var marker = rawLine.IndexOf("nameserver[", StringComparison.Ordinal);
            if (marker < 0)
                continue;

            var colon = rawLine.IndexOf(':', marker);
            if (colon < 0 || colon == rawLine.Length - 1)
                continue;

            var address = rawLine[(colon + 1)..].Trim();
            if (address.Length > 0)
                addresses.Add(address);
        }

        return addresses.ToArray();
    }
}

internal sealed record DnsStatusResult(
    string Platform,
    bool Supported,
    bool UsesAdGuardDns,
    bool EncryptedDnsLikely,
    int ResolverCount,
    DnsResolver[] Resolvers,
    string[] Notes);

internal sealed record DnsResolver(string Address, bool IsAdGuard);
