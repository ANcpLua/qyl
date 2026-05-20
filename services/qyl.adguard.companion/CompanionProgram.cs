using Qyl.AdGuard.Companion.Diagnostics;
using Qyl.AdGuard.Companion.Installation;
using Qyl.AdGuard.Companion.Messaging;
using Qyl.AdGuard.Companion.Telemetry;

namespace Qyl.AdGuard.Companion;

internal static class CompanionProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args is [var command, ..] && command.Equals("install", StringComparison.OrdinalIgnoreCase))
            return await NativeHostInstaller.InstallAsync(args[1..], CancellationToken.None).ConfigureAwait(false);

        if (args is [var doctorCommand, ..] && doctorCommand.Equals("doctor", StringComparison.OrdinalIgnoreCase))
            return await NativeHostDoctor.RunCliAsync(args[1..], CancellationToken.None).ConfigureAwait(false);

        var callerOrigin = args.FirstOrDefault(static arg => arg.StartsWith("chrome-extension://", StringComparison.Ordinal));
        await using var telemetry = await CompanionTelemetry.CreateAsync(Console.Error)
            .ConfigureAwait(false);

        var stats = new CompanionStats(TimeProvider.System);

        var dispatcher = new NativeMessageDispatcher(
            new DnsDiagnostics(Console.Error),
            new NetworkBatchAnalyzer(telemetry, stats),
            new NativeHostDoctor(callerOrigin, telemetry),
            telemetry,
            stats);

        await using var standardInput = Console.OpenStandardInput();
        await using var standardOutput = Console.OpenStandardOutput();

        var protocol = new NativeMessagingProtocol(standardInput, standardOutput, Console.Error);
        await protocol.RunAsync(dispatcher, CancellationToken.None).ConfigureAwait(false);

        return 0;
    }
}
