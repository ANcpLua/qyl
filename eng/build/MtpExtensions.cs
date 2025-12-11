using System;
using System.Collections.Generic;
using System.Text;
using Nuke.Common.IO;

public static class MtpExtensions
{
    public static MtpArgumentsBuilder Mtp() => new();
}

public sealed class MtpArgumentsBuilder
{
    private readonly List<string> _args = [];

    private MtpArgumentsBuilder AddFilter(string option, params ReadOnlySpan<string> patterns)
    {
        foreach (var p in patterns)
        {
            if (string.IsNullOrEmpty(p)) continue;

            _args.Add(option);
            _args.Add(p);
        }

        return this;
    }

    private MtpArgumentsBuilder AddOption(string option, string value)
    {
        _args.Add(option);
        _args.Add(value);
        return this;
    }

    private MtpArgumentsBuilder AddFlag(string option)
    {
        _args.Add(option);
        _args.Add("on");
        return this;
    }

    public MtpArgumentsBuilder FilterNamespace(params ReadOnlySpan<string> patterns)
        => AddFilter("--filter-namespace", patterns);

    public MtpArgumentsBuilder FilterNotNamespace(params ReadOnlySpan<string> patterns)
        => AddFilter("--filter-not-namespace", patterns);

    public MtpArgumentsBuilder FilterClass(params ReadOnlySpan<string> patterns)
        => AddFilter("--filter-class", patterns);

    public MtpArgumentsBuilder FilterNotClass(params ReadOnlySpan<string> patterns)
        => AddFilter("--filter-not-class", patterns);

    public MtpArgumentsBuilder FilterMethod(params ReadOnlySpan<string> patterns)
        => AddFilter("--filter-method", patterns);

    public MtpArgumentsBuilder FilterTrait(string name, string value)
        => AddOption("--filter-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterNotTrait(string name, string value)
        => AddOption("--filter-not-trait", $"{name}={value}");

    public MtpArgumentsBuilder FilterQuery(string? expression)
        => string.IsNullOrEmpty(expression) ? this : AddOption("--filter-query", expression);

    public MtpArgumentsBuilder ReportTrx(string filename)
    {
        _args.Add("--report-trx");
        return AddOption("--report-trx-filename", filename);
    }

    public MtpArgumentsBuilder ReportXunit(string filename)
    {
        _args.Add("--report-xunit");
        return AddOption("--report-xunit-filename", filename);
    }

    public MtpArgumentsBuilder ReportJunit(string filename)
    {
        _args.Add("--report-junit");
        return AddOption("--report-junit-filename", filename);
    }

    public MtpArgumentsBuilder ResultsDirectory(AbsolutePath path)
        => AddOption("--results-directory", path.ToString());

    public MtpArgumentsBuilder StopOnFail() => AddFlag("--stop-on-fail");

    public MtpArgumentsBuilder MaxThreads(int count)
        => AddOption("--max-threads", count.ToString());

    public MtpArgumentsBuilder Timeout(TimeSpan duration)
        => AddOption("--timeout", $"{(int)duration.TotalSeconds}s");

    public MtpArgumentsBuilder IgnoreExitCode(int code)
        => AddOption("--ignore-exit-code", code.ToString());

    public MtpArgumentsBuilder MinimumExpectedtests(int count)
        => AddOption("--minimum-expected-tests", count.ToString());

    public MtpArgumentsBuilder Seed(int seed)
        => AddOption("--seed", seed.ToString());

    public MtpArgumentsBuilder ShowLiveOutput() => AddFlag("--show-live-output");

    public MtpArgumentsBuilder Diagnostics() => AddFlag("--xunit-diagnostics");

    public MtpArgumentsBuilder CoverageCobertura(AbsolutePath outputPath)
    {
        _args.Add("--coverage");
        _args.Add("--coverage-output-format");
        _args.Add("cobertura");
        return AddOption("--coverage-output", outputPath.ToString());
    }

    public string Build()
    {
        if (_args is []) return string.Empty;

        StringBuilder sb = new();
        foreach (var arg in _args)
        {
            if (sb.Length > 0) sb.Append(' ');

            sb.Append(arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg);
        }

        return sb.ToString();
    }

    public IReadOnlyList<string> BuildArgs() => _args;
}
