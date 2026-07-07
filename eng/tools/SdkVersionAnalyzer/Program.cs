// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
// qyl adaptation: expected versions are read from global.json; GitHub workflow
// definitions and dockerfiles are then checked for drift against it.

namespace SdkVersionAnalyzer;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("At least 2 arguments required - operation mode (--verify|--modify) and directory root.");
            return 1;
        }

        var operationMode = args[0];
        var directoryRoot = args[1];
        switch (operationMode)
        {
            case "--verify" when args.Length == 2:
                {
                    return VerifyDotnetSdkVersions(directoryRoot);
                }

            case "--modify" when args.Length == 5:
                {
                    var requestedSdkVersions = new DotnetSdkVersion(
                        NullIfEmpty(args[2]),
                        NullIfEmpty(args[3]),
                        NullIfEmpty(args[4]));
                    ModifyDotnetSdkVersions(directoryRoot, requestedSdkVersions);
                    return 0;
                }

            default:
                {
                    Console.WriteLine("Invalid arguments. Usage: --verify <root> | --modify <root> <net8|-> <net9|-> <net10|->");
                    return 1;
                }
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) || value == "-" ? null : value;

    private static void ModifyDotnetSdkVersions(string directoryRoot, DotnetSdkVersion requestedSdkVersions)
    {
        ActionWorkflowAnalyzer.ModifyVersions(directoryRoot, requestedSdkVersions);
        DockerfileAnalyzer.ModifyVersions(directoryRoot, requestedSdkVersions);
    }

    private static int VerifyDotnetSdkVersions(string directoryRoot)
    {
        // global.json owns the toolchain version. Workflows and dockerfiles that
        // pin an explicit SDK version must agree with it.
        var expectedVersion = GlobalJsonAnalyzer.GetExpectedSdkVersion(directoryRoot);
        if (expectedVersion is null)
        {
            Console.WriteLine("Unable to extract expected SDK version from global.json.");
            return 1;
        }

        Console.WriteLine($"Expected SDK versions: {expectedVersion}");
        if (!ActionWorkflowAnalyzer.VerifyVersions(directoryRoot, expectedVersion))
        {
            Console.WriteLine("Invalid SDK versions in GitHub workflow or action definitions.");
            return 1;
        }

        if (!DockerfileAnalyzer.VerifyVersions(directoryRoot, expectedVersion))
        {
            Console.WriteLine("Invalid SDK versions in dockerfiles.");
            return 1;
        }

        Console.WriteLine("SDK versions are consistent.");
        return 0;
    }
}
