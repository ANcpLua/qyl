namespace Qyl.Conformance;

/// <summary>
/// Pure diff engine: conformance plan (declared) vs observed snapshot (emitted).
/// The graph declares, the collector must conform — and the verifier is the only
/// component allowed to say so.
///
/// Severity law:
///   declared signal never observed            → error  (declared_missing)
///   observed signal/service never declared    → error  (undeclared_emitted; never sneak in a fact)
///   required attribute absent on an occurrence → error  (attribute_drift)
///   recommended attribute never observed       → warning(attribute_drift)
///   observed attribute key never declared      → warning(attribute_drift)
/// </summary>
public static class ConformanceVerifier
{
    public static ConformanceReport Verify(ConformancePlan plan, IReadOnlyList<ObservedSignal> observed)
    {
        var findings = new List<ConformanceFinding>();
        var observedByService = observed.ToLookup(o => o.ServiceName, StringComparer.Ordinal);
        var plannedServices = plan.Services.ToDictionary(s => s.ServiceName, StringComparer.Ordinal);

        foreach (var service in plan.Services)
            VerifyService(service, observedByService[service.ServiceName].ToList(), findings);

        foreach (var orphanService in observedByService.Where(g => !plannedServices.ContainsKey(g.Key)))
        {
            findings.Add(new ConformanceFinding
            {
                Kind = ConformanceFindingKind.UndeclaredEmitted,
                Severity = ConformanceSeverity.Error,
                ServiceName = orphanService.Key,
                Detail = $"Service '{orphanService.Key}' emitted telemetry but is not a node of the control graph.",
            });
        }

        return new ConformanceReport
        {
            GraphSchemaVersion = plan.GraphSchemaVersion,
            Findings = findings,
            Conformant = findings.All(f => f.Severity != ConformanceSeverity.Error),
        };
    }

    private static void VerifyService(PlannedService service, IReadOnlyList<ObservedSignal> observed, List<ConformanceFinding> findings)
    {
        var expectedByIdentity = service.ExpectedSignals.ToDictionary(e => (e.Kind, e.Name));
        var observedByIdentity = observed.ToLookup(o => (o.Kind, o.Name));

        foreach (var expected in service.ExpectedSignals)
        {
            var occurrences = observedByIdentity[(expected.Kind, expected.Name)].ToList();
            if (occurrences.Count == 0)
            {
                findings.Add(SignalFinding(ConformanceFindingKind.DeclaredMissing, ConformanceSeverity.Error,
                    service.ServiceName, expected,
                    $"Declared signal '{expected.Kind}:{expected.Name}' was never observed."));
                continue;
            }

            VerifyAttributes(service.ServiceName, expected, occurrences, findings);
        }

        foreach (var orphan in observedByIdentity.Where(g => !expectedByIdentity.ContainsKey(g.Key)))
        {
            findings.Add(new ConformanceFinding
            {
                Kind = ConformanceFindingKind.UndeclaredEmitted,
                Severity = ConformanceSeverity.Error,
                ServiceName = service.ServiceName,
                SignalKind = orphan.Key.Kind,
                SignalName = orphan.Key.Name,
                Detail = $"Observed signal '{orphan.Key.Kind}:{orphan.Key.Name}' is not declared by service '{service.ServiceName}'.",
            });
        }
    }

    private static void VerifyAttributes(string serviceName, ExpectedSignal expected, IReadOnlyList<ObservedSignal> occurrences, List<ConformanceFinding> findings)
    {
        foreach (var required in expected.RequiredAttributes)
        {
            var missingOn = occurrences.Count(o => !o.AttributeKeys.Contains(required, StringComparer.Ordinal));
            if (missingOn > 0)
            {
                findings.Add(AttributeFinding(ConformanceSeverity.Error, serviceName, expected, required,
                    $"Required attribute '{required}' missing on {missingOn}/{occurrences.Count} occurrences of '{expected.Kind}:{expected.Name}'."));
            }
        }

        var everObserved = occurrences.SelectMany(o => o.AttributeKeys).ToHashSet(StringComparer.Ordinal);

        foreach (var recommended in expected.RecommendedAttributes.Where(r => !everObserved.Contains(r)))
        {
            findings.Add(AttributeFinding(ConformanceSeverity.Warning, serviceName, expected, recommended,
                $"Recommended attribute '{recommended}' was never observed on '{expected.Kind}:{expected.Name}'."));
        }

        var declared = expected.RequiredAttributes
            .Concat(expected.RecommendedAttributes)
            .Concat(expected.OptInAttributes)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var unknown in everObserved.Where(k => !declared.Contains(k)).Order(StringComparer.Ordinal))
        {
            findings.Add(AttributeFinding(ConformanceSeverity.Warning, serviceName, expected, unknown,
                $"Observed attribute '{unknown}' is not declared on '{expected.Kind}:{expected.Name}'."));
        }
    }

    private static ConformanceFinding SignalFinding(ConformanceFindingKind kind, ConformanceSeverity severity, string serviceName, ExpectedSignal signal, string detail) =>
        new()
        {
            Kind = kind,
            Severity = severity,
            ServiceName = serviceName,
            SignalKind = signal.Kind,
            SignalName = signal.Name,
            Detail = detail,
        };

    private static ConformanceFinding AttributeFinding(ConformanceSeverity severity, string serviceName, ExpectedSignal signal, string attributeKey, string detail) =>
        new()
        {
            Kind = ConformanceFindingKind.AttributeDrift,
            Severity = severity,
            ServiceName = serviceName,
            SignalKind = signal.Kind,
            SignalName = signal.Name,
            AttributeKey = attributeKey,
            Detail = detail,
        };
}
