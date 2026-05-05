using Qyl.Contracts.Intelligence;

namespace Qyl.Collector.Intelligence;

internal static class IntelligenceEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapIntelligenceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/intelligence");

        group.MapGet("/evaluate", EvaluateAsync);
        group.MapGet("/causal-chain", CausalChainAsync);
        group.MapGet("/strategy", StrategyAsync);
        group.MapGet("/execute-step", ExecuteStepAsync);

        return app;
    }

    private static async Task<IResult> EvaluateAsync(
        IPatternEngine engine,
        SessionQueryService queryService,
        DuckDbStore store,
        string? traceId,
        string? issueId,
        CancellationToken ct)
    {
        if (traceId is null && issueId is null)
            return TypedResults.BadRequest(new { error = "Provide traceId or issueId." });

        IReadOnlyList<SpanStorageRow> spans;

        if (traceId is not null)
        {
            spans = await queryService.GetSpansByTraceAsync(traceId, ct).ConfigureAwait(false);
            if (spans.Count is 0)
                return TypedResults.NotFound();
        }
        else
        {
            var issue = await store.GetIssueByIdAsync(issueId!, ct).ConfigureAwait(false);
            if (issue is null)
                return TypedResults.NotFound();

            spans = await GetSpansForIssueAsync(store, issue.ErrorType, ct).ConfigureAwait(false);
        }

        var signals = ExtractSignals(spans);
        var matches = engine.Evaluate(signals);

        return TypedResults.Ok(new
        {
            matches = matches.Select(static m => new
            {
                pattern_id = m.Pattern.Id,
                category = m.Pattern.Category.ToString().ToLowerInvariant(),
                score = m.Score,
                hypothesis = m.Pattern.Hypothesis,
                matched_signals = m.MatchedSignals.Select(static s => new
                {
                    attribute = s.Attribute,
                    @operator = s.Operator.ToString().ToLowerInvariant(),
                    expected = s.Value,
                    observed = s.Value
                })
            })
        });
    }

    private static ValueTask<IResult> CausalChainAsync(
        IPatternEngine engine,
        string patternIds,
        CancellationToken ct)
    {
        _ = ct;
        var ids = patternIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var allPatterns = DiagnosticPatterns.All;
        var matches = new List<PatternMatch>();
        foreach (var id in ids)
        {
            var pattern = allPatterns.FirstOrDefault(p => p.Id.EqualsOrdinal(id));
            if (pattern is not null)
                matches.Add(new PatternMatch(pattern, pattern.Confidence, pattern.Signals));
        }

        var graph = engine.BuildCausalGraph(matches);

        return ValueTask.FromResult<IResult>(TypedResults.Ok(new
        {
            root_causes = graph.RootCauses,
            edges = graph.Edges.Select(static e => new
            {
                cause = e.CausePatternId, effect = e.EffectPatternId, strength = e.Strength
            })
        }));
    }

    private static ValueTask<IResult> StrategyAsync(
        IPatternEngine engine,
        string patternId,
        CancellationToken ct)
    {
        _ = ct;
        var pattern = DiagnosticPatterns.All.FirstOrDefault(p => p.Id.EqualsOrdinal(patternId));
        if (pattern is null)
            return ValueTask.FromResult<IResult>(TypedResults.NotFound());

        var match = new PatternMatch(pattern, pattern.Confidence, pattern.Signals);
        var strategy = engine.SelectStrategy(match);

        if (strategy is null)
            return ValueTask.FromResult<IResult>(TypedResults.NotFound());

        return ValueTask.FromResult<IResult>(TypedResults.Ok(new
        {
            id = strategy.Id,
            trigger_pattern = strategy.TriggerPattern,
            steps = strategy.Steps.Select(static s => new
            {
                action = s.Action, query = s.Query, description = s.Description
            })
        }));
    }

    private static async Task<IResult> ExecuteStepAsync(
        DuckDbStore store,
        string strategyId,
        int stepIndex,
        string? traceId,
        string? service,
        CancellationToken ct)
    {
        var strategy = InvestigationStrategies.All.FirstOrDefault(s => s.Id.EqualsOrdinal(strategyId));

        if (strategy is null || stepIndex < 0 || stepIndex >= strategy.Steps.Count)
            return TypedResults.NotFound();

        var step = strategy.Steps[stepIndex];
        var query = step.Query;

        if (traceId is not null)
            query = query.ReplaceOrdinal("${trace_id}", traceId);
        if (service is not null)
            query = query.ReplaceOrdinal("${service_name}", service);

        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = query;

        var rows = new List<Dictionary<string, object?>>();
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = await reader.IsDBNullAsync(i, ct).ConfigureAwait(false)
                        ? null
                        : reader.GetValue(i);
                }

                rows.Add(row);
            }
        }
        catch (DuckDBException ex)
        {
            return TypedResults.Ok(new
            {
                query_results = Array.Empty<object>(),
                step_description = step.Description,
                has_next_step = stepIndex + 1 < strategy.Steps.Count,
                error = ex.Message
            });
        }

        return TypedResults.Ok(new
        {
            query_results = rows,
            step_description = step.Description,
            has_next_step = stepIndex + 1 < strategy.Steps.Count
        });
    }

    private static List<Signal> ExtractSignals(IReadOnlyList<SpanStorageRow> spans)
    {
        var signals = new List<Signal>();

        foreach (var span in spans)
        {
            AddSignalIfPresent(signals, "status_code", span.StatusCode.ToString(CultureInfo.InvariantCulture));
            AddSignalIfPresent(signals, "status_message", span.StatusMessage);
            AddSignalIfPresent(signals, "service_name", span.ServiceName);
            AddSignalIfPresent(signals, "gen_ai_provider_name", span.GenAiProviderName);
            AddSignalIfPresent(signals, "gen_ai_request_model", span.GenAiRequestModel);
            AddSignalIfPresent(signals, "gen_ai_response_model", span.GenAiResponseModel);
            AddSignalIfPresent(signals, "gen_ai_stop_reason", span.GenAiStopReason);
            AddSignalIfPresent(signals, "gen_ai_tool_name", span.GenAiToolName);
            AddSignalIfPresent(signals, "gen_ai_cost_usd", span.GenAiCostUsd?.ToString(CultureInfo.InvariantCulture));
            AddSignalIfPresent(signals, "gen_ai_input_tokens",
                span.GenAiInputTokens?.ToString(CultureInfo.InvariantCulture));
            AddSignalIfPresent(signals, "gen_ai_output_tokens",
                span.GenAiOutputTokens?.ToString(CultureInfo.InvariantCulture));
            AddSignalIfPresent(signals, "span_name", span.Name);
            AddSignalIfPresent(signals, "duration_ns", span.DurationNs.ToString(CultureInfo.InvariantCulture));

            if (span.StatusCode is 2)
                AddSignalIfPresent(signals, "error_type", span.StatusMessage);

            if (span.AttributesJson is not null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(span.AttributesJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var value = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.GetRawText(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => null
                        };
                        AddSignalIfPresent(signals, prop.Name, value);
                    }
                }
                catch (JsonException ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        return signals;
    }

    private static void AddSignalIfPresent(List<Signal> signals, string attribute, string? value)
    {
        if (value is null) return;
        signals.Add(new Signal { Attribute = attribute, Operator = SignalOperator.Eq, Value = value });
    }

    private static async Task<IReadOnlyList<SpanStorageRow>> GetSpansForIssueAsync(
        DuckDbStore store,
        string errorType,
        CancellationToken ct)
    {
        await using var lease = await store.GetReadConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = """
                          SELECT *
                          FROM spans
                          WHERE status_code = 2
                            AND status_message ILIKE $1
                          ORDER BY start_time_unix_nano DESC
                          LIMIT 50
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = $"%{errorType}%" });

        var spans = new List<SpanStorageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            spans.Add(SpanStorageRow.MapFromReader(reader));

        return spans;
    }
}
