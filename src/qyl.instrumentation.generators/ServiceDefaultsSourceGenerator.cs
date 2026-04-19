using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Qyl.Instrumentation.Generators.CallSites;
using Qyl.Instrumentation.Generators.Emitters;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators;

/// <summary>
///     Intercepts WebApplicationBuilder.Build() calls to auto-register Qyl service defaults.
/// </summary>
/// <remarks>
///     See: https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md
/// </remarks>
[Generator]
public sealed class ServiceDefaultsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // =====================================================================
        // BUILDER INTERCEPTION PIPELINE
        // Discovers WebApplicationBuilder.Build() calls and intercepts them
        // to inject Qyl service defaults and endpoint registration.
        // =====================================================================

        // Step 1: Detect if Qyl runtime is available (prerequisite gate)
        var qylRuntimeAvailable = context.CompilationProvider
            .Select(IsQylRuntimeReferenced)
            .WithTrackingName(PipelineStage.QylRuntimeCheck);

        // Step 2: Discover Build() call sites eligible for interception
        var builderCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                CouldBeBuildInvocation, // Fast syntactic pre-filter
                ExtractBuilderCallSite) // Semantic analysis
            .WhereNotNull()
            .WithTrackingName(PipelineStage.BuilderCallSitesDiscovered);

        // MSBuild property toggles (default: true when absent)
        var toggles = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => new PipelineToggles(
                IsPipelineEnabled(options, "QylDatabase"),
                IsPipelineEnabled(options, "QylAgent"),
                IsPipelineEnabled(options, "QylTraced"),
                IsPipelineEnabled(options, "QylMeter")))
            .WithTrackingName(PipelineStage.ToggleCheck);

        // Per-pipeline enabled flags: runtime check AND MSBuild toggle
        var dbEnabled = qylRuntimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Database);
        var meterEnabled = qylRuntimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Meter);
        var tracedEnabled = qylRuntimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Traced);
        var agentEnabled = qylRuntimeAvailable.Combine(toggles)
            .Select(static (pair, _) => pair.Left && pair.Right.Agent);

        var meterDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MeterAnalyzer.MeterAttributeMetadataName,
                MeterAnalyzer.CouldBeMeterClass,
                MeterAnalyzer.ExtractDefinitionFromAttribute)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.MeterDefinitionsDiscovered);

        var tracedCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                TracedCallSiteAnalyzer.CouldBeTracedInvocation,
                TracedCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.TracedCallSitesDiscovered);

        // =====================================================================
        // GENAI / DB / AGENT CALL SITE PROVIDERS
        // Exposed as standalone providers so they can feed both
        // their individual emitters AND the capability manifest pipeline.
        // =====================================================================

        var genAiCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                GenAiCallSiteAnalyzer.CouldBeGenAiInvocation,
                GenAiCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.GenAiCallSitesDiscovered);

        var dbCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                DbCallSiteAnalyzer.CouldBeDbInvocation,
                DbCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.DbCallSitesDiscovered);

        var agentCallSites = context.SyntaxProvider
            .CreateSyntaxProvider(
                AgentCallSiteAnalyzer.CouldBeAgentInvocation,
                AgentCallSiteAnalyzer.ExtractCallSite)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.AgentCallSitesDiscovered);

        // =====================================================================
        // SERVICE REGISTRATION (telemetry + capabilities)
        // =====================================================================

        var generatedRegistration = BuildGeneratedServiceRegistration(
            context,
            meterDefinitions, meterEnabled,
            tracedCallSites, tracedEnabled,
            genAiCallSites,
            agentCallSites,
            toggles);

        var builderInterceptors = builderCallSites
            .CollectAsEquatableArray()
            .Combine(qylRuntimeAvailable)
            .Combine(generatedRegistration)
            .Select(static (input, _) => new BuilderInterceptorInput(
                input.Left.Left,
                input.Left.Right,
                input.Right));

        // Step 3: Emit interceptor code when prerequisites are met
        context.RegisterSourceOutput(builderInterceptors, EmitBuilderInterceptors);

        // =====================================================================
        // INDIVIDUAL EMITTER PIPELINES
        // =====================================================================

        // GenAi SDK interception removed: InstrumentedChatClient (runtime DelegatingChatClient)
        // handles all IChatClient instrumentation with provider/model/token enrichment.
        // GenAiCallSiteAnalyzer is retained for compile-time capability discovery only.

        RegisterCollectedEmitterPipeline(context,
            dbCallSites, dbEnabled,
            DbInterceptorEmitter.Emit, GeneratedFile.DbInterceptors, "QSG002");

        RegisterCollectedEmitterPipeline(context,
            meterDefinitions, meterEnabled,
            MeterEmitter.Emit, GeneratedFile.MeterImplementations, "QSG004");

        RegisterCollectedEmitterPipeline(context,
            tracedCallSites, tracedEnabled,
            TracedInterceptorEmitter.Emit, GeneratedFile.TracedInterceptors, "QSG005");

        RegisterCollectedEmitterPipeline(context,
            agentCallSites, agentEnabled,
            AgentInterceptorEmitter.Emit, GeneratedFile.AgentInterceptors, "QSG006");

        // GenAi call sites feed the capability manifest pipeline below (providers, models, operations)
        // but no longer generate interceptor code — InstrumentedChatClient handles runtime instrumentation.

        // =====================================================================
        // TOOL MANIFEST PIPELINE
        // Discovers [McpServerToolType] classes and emits a compile-time
        // Type[] array, replacing the hardcoded list in McpToolRegistry.
        // Not gated by toggles or runtime check — if the MCP SDK attribute
        // isn't referenced, ForAttributeWithMetadataName finds nothing.
        // =====================================================================

        var toolTypeEntries = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ToolManifestAnalyzer.McpServerToolTypeMetadataName,
                ToolManifestAnalyzer.CouldBeToolTypeClass,
                ToolManifestAnalyzer.ExtractToolType)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.ToolTypesDiscovered);

        // =====================================================================
        // HOSTED SERVICE REGISTRATION PIPELINE
        // Discovers [QylHostedService]-tagged classes and emits
        // QylGeneratedRegistry.RegisterQylHostedServices(IServiceCollection)
        // which is called from the intercepted builder.Build() site.
        // =====================================================================

        var hostedServiceDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                HostedServiceAnalyzer.HostedServiceAttributeMetadataName,
                HostedServiceAnalyzer.CouldBeHostedServiceClass,
                HostedServiceAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.HostedServicesDiscovered);

        var mapEndpointsDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MapEndpointsAnalyzer.MapEndpointsAttributeMetadataName,
                MapEndpointsAnalyzer.CouldBeMapEndpointsMethod,
                MapEndpointsAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.MapEndpointsDiscovered);

        var qylServiceDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QylServiceAnalyzer.QylServiceAttributeMetadataName,
                QylServiceAnalyzer.CouldBeQylServiceClass,
                QylServiceAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.QylServicesDiscovered);

        var qylHealthCheckDefinitions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                QylHealthCheckAnalyzer.QylHealthCheckAttributeMetadataName,
                QylHealthCheckAnalyzer.CouldBeHealthCheckClass,
                QylHealthCheckAnalyzer.Extract)
            .WhereNotNull()
            .WithTrackingName(PipelineStage.QylHealthChecksDiscovered);

        // Always emit QylGeneratedRegistry. The intercepted Build() site calls
        // RegisterQylHostedServices / RegisterQylServices / RegisterQylHealthChecks
        // unconditionally, so the class (and its four methods) must exist even when
        // a consumer hasn't tagged anything.
        var generatedRegistryInput = hostedServiceDefinitions
            .CollectAsEquatableArray()
            .Combine(mapEndpointsDefinitions.CollectAsEquatableArray())
            .Combine(qylServiceDefinitions.CollectAsEquatableArray())
            .Combine(qylHealthCheckDefinitions.CollectAsEquatableArray())
            .Combine(qylRuntimeAvailable);

        context.RegisterSourceOutput(
            generatedRegistryInput,
            static (spc, input) =>
            {
                var ((((hostedServices, endpoints), services), healthChecks), runtimeAvailable) = input;
                if (!runtimeAvailable) return;
                var source = HostedServiceEmitter.Emit(
                    hostedServices.IsDefaultOrEmpty ? [] : hostedServices.AsImmutableArray(),
                    endpoints.IsDefaultOrEmpty ? [] : endpoints.AsImmutableArray(),
                    services.IsDefaultOrEmpty ? [] : services.AsImmutableArray(),
                    healthChecks.IsDefaultOrEmpty ? [] : healthChecks.AsImmutableArray());
                spc.AddSource(GeneratedFile.HostedServiceRegistry, SourceText.From(source, Encoding.UTF8));
            });

        context.RegisterSourceOutput(
            toolTypeEntries.CollectAsEquatableArray(),
            static (spc, toolTypes) =>
            {
                if (toolTypes.IsDefaultOrEmpty) return;
                var source = ToolManifestEmitter.Emit(toolTypes.AsImmutableArray());
                if (!string.IsNullOrEmpty(source))
                    spc.AddSource(GeneratedFile.ToolManifest, SourceText.From(source, Encoding.UTF8));
            });

        // =====================================================================
        // CAPABILITY MANIFEST PIPELINE
        // Emits [assembly: GeneratedCapabilityAttribute] for cross-assembly
        // discovery. Capabilities are always emitted (not gated by toggles)
        // because they reflect inherent service topology, not active telemetry.
        // =====================================================================

        var capabilityInput = genAiCallSites
            .CollectAsEquatableArray()
            .Combine(agentCallSites.CollectAsEquatableArray())
            .Combine(qylRuntimeAvailable);

        context.RegisterSourceOutput(capabilityInput, static (spc, input) =>
        {
            var ((genAi, agents), runtimeAvailable) = input;
            if (!runtimeAvailable) return;

            var source = CapabilityEmitter.Emit(
                genAi.AsImmutableArray(),
                agents.AsImmutableArray());

            if (!string.IsNullOrEmpty(source))
                spc.AddSource(GeneratedFile.Capabilities, SourceText.From(source, Encoding.UTF8));
        });
    }

    // =========================================================================
    // PIPELINE REGISTRATION HELPER
    // =========================================================================

    private static void RegisterCollectedEmitterPipeline<T>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<T> values,
        IncrementalValueProvider<bool> enabledFlag,
        Func<ImmutableArray<T>, string> emitter,
        string generatedFileName,
        string diagnosticId)
        where T : class, IEquatable<T> =>
        values
            .CollectAsEquatableArray()
            .Combine(enabledFlag)
            .SelectAndReportExceptions((input, _) =>
            {
                if (!input.Right || input.Left.IsDefaultOrEmpty) return FileWithName.Empty;
                var sourceCode = emitter(input.Left.AsImmutableArray());
                return string.IsNullOrEmpty(sourceCode)
                    ? FileWithName.Empty
                    : new FileWithName(generatedFileName, sourceCode);
            }, context, diagnosticId)
            .AddSource(context);

    // =========================================================================
    // RUNTIME AVAILABILITY CHECKS
    // These gate code generation based on whether required libraries are referenced.
    // =========================================================================

    /// <summary>
    ///     Checks if the Qyl.Instrumentation runtime is referenced.
    ///     This is the prerequisite for most codegen operations.
    /// </summary>
    private static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(WellKnownType.QylServiceDefaults) is not null;

    /// <summary>
    ///     Reads an MSBuild toggle property. Returns true if absent or "true".
    /// </summary>
    private static bool IsPipelineEnabled(
        AnalyzerConfigOptionsProvider options,
        string propertyName) =>
        !options.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value)
        || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    // =========================================================================
    // SYNTACTIC PRE-FILTER
    // Fast check that runs on every syntax node. Must be cheap - no semantic model!
    // =========================================================================

    /// <summary>
    ///     Fast syntactic check for candidate <c>Build()</c> invocations.
    /// </summary>
    private static bool CouldBeBuildInvocation(SyntaxNode node, CancellationToken _) =>
        string.Equals(IncrementalPipelineHelpers.GetInvokedMethodName(node), MethodName.Build, StringComparison.Ordinal);

    // =========================================================================
    // BUILDER CALL SITE EXTRACTION
    // Semantic analysis to identify and validate WebApplicationBuilder.Build() calls.
    // =========================================================================

    /// <summary>
    ///     Analyzes an invocation to determine if it's a WebApplicationBuilder.Build()
    ///     call that should be intercepted.
    /// </summary>
    /// <returns>A validated call site descriptor, or null if not applicable.</returns>
    private static BuilderCallSite? ExtractBuilderCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (!IncrementalPipelineHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!IsWebApplicationBuilderBuildCall(invocation, context.SemanticModel.Compilation))
            return null;

        // Avoid conflicts: skip if another generator has already intercepted this call
        if (IncrementalPipelineHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var location = ExtractInterceptableLocation(context, cancellationToken);
        return location is null
            ? null
            : new BuilderCallSite(
                IncrementalPipelineHelpers.FormatSortKey(context.Node),
                BuilderCallKind.Build,
                location);
    }


    private static bool IsWebApplicationBuilderBuildCall(
        IInvocationOperation invocation,
        Compilation compilation)
    {
        var webAppBuilderType = compilation.GetTypeByMetadataName(WellKnownType.WebApplicationBuilder);
        return invocation.IsMethodNamed(webAppBuilderType, MethodName.Build);
    }


    private static InterceptableLocation? ExtractInterceptableLocation(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken) =>
        context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);


    // =========================================================================
    // CODE EMITTERS
    // Transform discovered call sites into generated source code.
    // =========================================================================

    private static void EmitBuilderInterceptors(
        SourceProductionContext context,
        BuilderInterceptorInput input)
    {
        if (!input.QylRuntimeAvailable)
            return;

        var sourceCode = GenerateBuilderInterceptorSource(
            input.CallSites.AsImmutableArray(),
            input.GeneratedTelemetry);
        context.AddSource(GeneratedFile.BuilderInterceptors, SourceText.From(sourceCode, Encoding.UTF8));
    }

    // =========================================================================
    // SOURCE CODE GENERATION
    // =========================================================================

    private static string GenerateBuilderInterceptorSource(
        ImmutableArray<BuilderCallSite> callSites,
        GeneratedServiceRegistration generatedTelemetry)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CodeTemplate.AutoGeneratedHeader);
        sb.AppendLine(CodeTemplate.PragmaDisableAll);
        sb.AppendLine();
        sb.AppendLine(CodeTemplate.InterceptsLocationAttribute);
        sb.AppendLine(CodeTemplate.InterceptorsNamespaceOpen);

        var orderedCallSites = callSites.OrderBy(static c => c.SortKey, StringComparer.Ordinal);
        var index = 0;
        foreach (var callSite in orderedCallSites)
        {
            if (callSite.Kind is BuilderCallKind.Build)
                AppendBuildInterceptorMethod(sb, callSite, index, generatedTelemetry);
            index++;
        }

        sb.AppendLine(CodeTemplate.InterceptorsNamespaceClose);
        return sb.ToString();
    }

    private static void AppendBuildInterceptorMethod(
        StringBuilder sb,
        BuilderCallSite callSite,
        int index,
        GeneratedServiceRegistration generatedTelemetry)
    {
        var displayLocation = callSite.Location.GetDisplayLocation();
        var interceptAttribute = callSite.Location.GetInterceptsLocationAttributeSyntax();
        var configure = BuildGeneratedServiceRegistrationLambda(generatedTelemetry);
        var useQylCall = configure is null
            ? $"builder.{MethodName.TryUseQylConventions}();"
            : $"builder.{MethodName.TryUseQylConventions}({configure});";

        sb.AppendLine($$"""
                                // Intercepted call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static global::{{WellKnownType.WebApplication}} {{MethodName.InterceptBuildPrefix}}{{index}}(
                                    this global::{{WellKnownType.WebApplicationBuilder}} builder)
                                {
                                    {{useQylCall}}
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHostedServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylServices(builder.Services);
                                    global::Qyl.Instrumentation.Generators.QylGeneratedRegistry.RegisterQylHealthChecks(builder.Services);
                                    var app = builder.{{MethodName.Build}}();
                                    app.{{MethodName.MapQylDefaultEndpoints}}();
                                    return app;
                                }
                        """);
    }

    private static IncrementalValueProvider<GeneratedServiceRegistration> BuildGeneratedServiceRegistration(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MeterDefinition> meterDefinitions,
        IncrementalValueProvider<bool> meterEnabled,
        IncrementalValuesProvider<TracedCallSite> tracedCallSites,
        IncrementalValueProvider<bool> tracedEnabled,
        IncrementalValuesProvider<GenAiCallSite> genAiCallSites,
        IncrementalValuesProvider<AgentCallSite> agentCallSites,
        IncrementalValueProvider<PipelineToggles> toggles)
    {
        // Telemetry sources (existing: meters + traced activity sources)
        var currentTelemetry = meterDefinitions
            .CollectAsEquatableArray()
            .Combine(meterEnabled)
            .Combine(tracedCallSites.CollectAsEquatableArray().Combine(tracedEnabled))
            .Select(static (input, _) =>
            {
                var currentMeters = input.Left.Right ? input.Left.Left : default;
                var currentTraced = input.Right.Right ? input.Right.Left : default;
                return BuildCurrentTelemetryRegistration(currentMeters, currentTraced);
            })
            .WithTrackingName(PipelineStage.GeneratedTelemetryCurrentDiscovered);

        // Capabilities (new: GenAI + Agent topology data)
        var currentCapabilities = genAiCallSites
            .CollectAsEquatableArray()
            .Combine(agentCallSites.CollectAsEquatableArray())
            .Select(static (input, _) => BuildCurrentCapabilities(input.Left, input.Right))
            .WithTrackingName(PipelineStage.CapabilitiesCurrentDiscovered);

        // Referenced assemblies (telemetry + capabilities)
        var referencedRegistration = context.CompilationProvider
            .Select(CollectReferencedServiceRegistration)
            .WithTrackingName(PipelineStage.GeneratedTelemetryReferencedDiscovered);

        return currentTelemetry
            .Combine(currentCapabilities)
            .Combine(referencedRegistration)
            .Combine(toggles)
            .Select(static (input, _) =>
            {
                var (((telemetry, capabilities), referenced), toggles) = input;
                return FilterServiceRegistration(
                    MergeServiceRegistrations(
                        MergeTelemetryWithCapabilities(telemetry, capabilities),
                        referenced),
                    toggles);
            })
            .WithTrackingName(PipelineStage.GeneratedTelemetryCombined);
    }

    // --- Current assembly extraction ---

    private static TelemetryRegistration BuildCurrentTelemetryRegistration(
        EquatableArray<MeterDefinition> meters,
        EquatableArray<TracedCallSite> tracedCallSites) =>
        new(
            tracedCallSites.IsDefaultOrEmpty
                ? default
                : tracedCallSites
                    .Select(static callSite => callSite.ActivitySourceName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray(),
            meters.IsDefaultOrEmpty
                ? default
                : meters
                    .Select(static meter => meter.MeterName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static name => name, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray());

    private static CapabilityRegistration BuildCurrentCapabilities(
        EquatableArray<GenAiCallSite> genAi,
        EquatableArray<AgentCallSite> agents) =>
        new(
            agents.IsDefaultOrEmpty
                ? default
                : agents
                    .Where(static c => c.AgentName is not null)
                    .Select(static c => c.AgentName!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray(),
            genAi.IsDefaultOrEmpty
                ? default
                : genAi
                    .Select(static c => c.Provider)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray(),
            genAi.IsDefaultOrEmpty
                ? default
                : genAi
                    .Where(static c => c.Model is not null)
                    .Select(static c => c.Model!)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray(),
            genAi.IsDefaultOrEmpty
                ? default
                : genAi
                    .Select(static c => c.Operation)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(static n => n, StringComparer.Ordinal)
                    .ToArray()
                    .ToEquatableArray());

    // --- Referenced assembly scanning ---

    private static GeneratedServiceRegistration CollectReferencedServiceRegistration(
        Compilation compilation,
        CancellationToken _)
    {
        var activitySources = new SortedSet<string>(StringComparer.Ordinal);
        var meterNames = new SortedSet<string>(StringComparer.Ordinal);
        var agents = new SortedSet<string>(StringComparer.Ordinal);
        var genAiProviders = new SortedSet<string>(StringComparer.Ordinal);
        var genAiModels = new SortedSet<string>(StringComparer.Ordinal);
        var genAiOperations = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
                continue;

            CollectGeneratedAttributes(
                assembly.GetAttributes(),
                activitySources, meterNames,
                agents, genAiProviders, genAiModels, genAiOperations);
        }

        return new GeneratedServiceRegistration(
            activitySources.Count is 0 ? default : activitySources.ToArray().ToEquatableArray(),
            meterNames.Count is 0 ? default : meterNames.ToArray().ToEquatableArray(),
            agents.Count is 0 ? default : agents.ToArray().ToEquatableArray(),
            genAiProviders.Count is 0 ? default : genAiProviders.ToArray().ToEquatableArray(),
            genAiModels.Count is 0 ? default : genAiModels.ToArray().ToEquatableArray(),
            genAiOperations.Count is 0 ? default : genAiOperations.ToArray().ToEquatableArray());
    }

    private static void CollectGeneratedAttributes(
        ImmutableArray<AttributeData> attributes,
        SortedSet<string> activitySources,
        SortedSet<string> meterNames,
        SortedSet<string> agents,
        SortedSet<string> genAiProviders,
        SortedSet<string> genAiModels,
        SortedSet<string> genAiOperations)
    {
        foreach (var attribute in attributes)
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();

            // Telemetry attributes: [GeneratedActivitySource("name")] / [GeneratedMeter("name")]
            if (attribute.ConstructorArguments.Length is 1 &&
                attribute.GetConstructorArgument<string>(0) is { Length: > 0 } name)
            {
                if (string.Equals(attributeName, WellKnownType.GeneratedActivitySourceAttribute,
                        StringComparison.Ordinal))
                    activitySources.Add(name);
                else if (string.Equals(attributeName, WellKnownType.GeneratedMeterAttribute, StringComparison.Ordinal))
                    meterNames.Add(name);
            }

            // Capability attributes: [GeneratedCapability("kind", "value")]
            if (attribute.ConstructorArguments.Length is 2 &&
                string.Equals(attributeName, WellKnownType.GeneratedCapabilityAttribute, StringComparison.Ordinal) &&
                attribute.GetConstructorArgument<string>(0) is { Length: > 0 } kind &&
                attribute.GetConstructorArgument<string>(1) is { Length: > 0 } value)
            {
                switch (kind)
                {
                    case CapabilityKind.Agent: agents.Add(value); break;
                    case CapabilityKind.GenAiProvider: genAiProviders.Add(value); break;
                    case CapabilityKind.GenAiModel: genAiModels.Add(value); break;
                    case CapabilityKind.GenAiOperation: genAiOperations.Add(value); break;
                }
            }
        }
    }

    // --- Merge & filter ---

    private static GeneratedServiceRegistration MergeTelemetryWithCapabilities(
        TelemetryRegistration telemetry,
        CapabilityRegistration capabilities) =>
        new(
            telemetry.ActivitySources,
            telemetry.MeterNames,
            capabilities.AgentNames,
            capabilities.GenAiProviders,
            capabilities.GenAiModels,
            capabilities.GenAiOperations);

    private static GeneratedServiceRegistration MergeServiceRegistrations(
        GeneratedServiceRegistration current,
        GeneratedServiceRegistration referenced) =>
        new(
            MergeNames(current.ActivitySources, referenced.ActivitySources),
            MergeNames(current.MeterNames, referenced.MeterNames),
            MergeNames(current.CapabilityAgents, referenced.CapabilityAgents),
            MergeNames(current.CapabilityGenAiProviders, referenced.CapabilityGenAiProviders),
            MergeNames(current.CapabilityGenAiModels, referenced.CapabilityGenAiModels),
            MergeNames(current.CapabilityGenAiOperations, referenced.CapabilityGenAiOperations));

    private static EquatableArray<string> MergeNames(
        EquatableArray<string> left,
        EquatableArray<string> right)
    {
        if (left.IsDefaultOrEmpty && right.IsDefaultOrEmpty)
            return default;

        return left
            .Concat(right)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray()
            .ToEquatableArray();
    }

    private static GeneratedServiceRegistration FilterServiceRegistration(
        GeneratedServiceRegistration registration,
        PipelineToggles toggles) =>
        new(
            toggles.Traced ? registration.ActivitySources : default,
            toggles.Meter ? registration.MeterNames : default,
            // Capabilities are not gated by toggles — they reflect inherent topology
            registration.CapabilityAgents,
            registration.CapabilityGenAiProviders,
            registration.CapabilityGenAiModels,
            registration.CapabilityGenAiOperations);

    private static string? BuildGeneratedServiceRegistrationLambda(
        GeneratedServiceRegistration registration)
    {
        var hastelemetry = !registration.ActivitySources.IsDefaultOrEmpty ||
                           !registration.MeterNames.IsDefaultOrEmpty;
        var hasCapabilities = !registration.CapabilityAgents.IsDefaultOrEmpty ||
                              !registration.CapabilityGenAiProviders.IsDefaultOrEmpty ||
                              !registration.CapabilityGenAiModels.IsDefaultOrEmpty ||
                              !registration.CapabilityGenAiOperations.IsDefaultOrEmpty;

        if (!hastelemetry && !hasCapabilities)
            return null;

        var sb = new StringBuilder();
        sb.AppendLine("static options =>");
        sb.AppendLine("        {");

        foreach (var activitySource in registration.ActivitySources)
            sb.AppendLine($"            options.AdditionalActivitySources.Add({Literal(activitySource)});");

        foreach (var meterName in registration.MeterNames)
            sb.AppendLine($"            options.AdditionalMeterNames.Add({Literal(meterName)});");

        AppendCapabilityAttribute(sb, "qyl.capability.agents", registration.CapabilityAgents);
        AppendCapabilityAttribute(sb, "qyl.capability.genai.providers", registration.CapabilityGenAiProviders);
        AppendCapabilityAttribute(sb, "qyl.capability.genai.models", registration.CapabilityGenAiModels);
        AppendCapabilityAttribute(sb, "qyl.capability.genai.operations", registration.CapabilityGenAiOperations);

        sb.Append("        }");
        return sb.ToString();
    }

    private static void AppendCapabilityAttribute(
        StringBuilder sb, string key, EquatableArray<string> values)
    {
        if (values.IsDefaultOrEmpty) return;
        var items = string.Join(", ", values.Select(static v => Literal(v)));
        sb.AppendLine(
            $"            options.CapabilityAttributes.Add(new({Literal(key)}, new string[] {{ {items} }}));");
    }

    private static string Literal(string s) => SymbolDisplay.FormatLiteral(s, true);

    // =========================================================================
    // CONSTANTS - Organized by semantic category
    // =========================================================================

    /// <summary>
    ///     Fully-qualified type names for semantic analysis and code generation.
    /// </summary>
    private static class WellKnownType
    {
        // ASP.NET Core types we intercept
        public const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
        public const string WebApplication = "Microsoft.AspNetCore.Builder.WebApplication";

        // Qyl runtime types that enable codegen
        public const string QylServiceDefaults = "Qyl.Instrumentation.QylServiceDefaults";
        public const string GeneratedActivitySourceAttribute = "Qyl.Instrumentation.GeneratedActivitySourceAttribute";
        public const string GeneratedMeterAttribute = "Qyl.Instrumentation.GeneratedMeterAttribute";
        public const string GeneratedCapabilityAttribute = "Qyl.Instrumentation.GeneratedCapabilityAttribute";
    }

    private static class CapabilityKind
    {
        public const string Agent = "agent";
        public const string GenAiProvider = "genai.provider";
        public const string GenAiModel = "genai.model";
        public const string GenAiOperation = "genai.operation";
    }

    /// <summary>
    ///     Method names used in interception detection and code generation.
    /// </summary>
    private static class MethodName
    {
        // Methods we intercept
        public const string Build = "Build";

        // Methods injected into generated code
        public const string TryUseQylConventions = "TryUseQylConventions";
        public const string MapQylDefaultEndpoints = "MapQylDefaultEndpoints";
        public const string InterceptBuildPrefix = "Intercept_Build";
    }

    /// <summary>
    ///     Pipeline stage names for incremental generator debugging.
    ///     These appear in Roslyn's generator driver diagnostics.
    /// </summary>
    private static class PipelineStage
    {
        // Runtime availability checks
        public const string QylRuntimeCheck = nameof(QylRuntimeCheck);

        // Builder interception pipeline
        public const string BuilderCallSitesDiscovered = nameof(BuilderCallSitesDiscovered);

        // GenAI interception pipeline
        public const string GenAiCallSitesDiscovered = nameof(GenAiCallSitesDiscovered);

        // Database interception pipeline
        public const string DbCallSitesDiscovered = nameof(DbCallSitesDiscovered);

        // Meter definition pipeline
        public const string MeterDefinitionsDiscovered = nameof(MeterDefinitionsDiscovered);

        // Traced method pipeline
        public const string TracedCallSitesDiscovered = nameof(TracedCallSitesDiscovered);

        // Agent interception pipeline
        public const string AgentCallSitesDiscovered = nameof(AgentCallSitesDiscovered);

        // Tool manifest pipeline
        public const string ToolTypesDiscovered = nameof(ToolTypesDiscovered);

        // Hosted service auto-registration pipeline
        public const string HostedServicesDiscovered = nameof(HostedServicesDiscovered);

        // Endpoint aggregator pipeline
        public const string MapEndpointsDiscovered = nameof(MapEndpointsDiscovered);

        // DI / health-check auto-registration pipelines
        public const string QylServicesDiscovered = nameof(QylServicesDiscovered);
        public const string QylHealthChecksDiscovered = nameof(QylHealthChecksDiscovered);

        // Capability manifest pipeline
        public const string CapabilitiesCurrentDiscovered = nameof(CapabilitiesCurrentDiscovered);

        // MSBuild property toggle check
        public const string ToggleCheck = nameof(ToggleCheck);

        // Generated telemetry registration discovery
        public const string GeneratedTelemetryCurrentDiscovered = nameof(GeneratedTelemetryCurrentDiscovered);
        public const string GeneratedTelemetryReferencedDiscovered = nameof(GeneratedTelemetryReferencedDiscovered);
        public const string GeneratedTelemetryCombined = nameof(GeneratedTelemetryCombined);
    }

    /// <summary>
    ///     Output file names for generated source files.
    /// </summary>
    private static class GeneratedFile
    {
        public const string BuilderInterceptors = "Intercepts.g.cs";
        public const string DbInterceptors = "DbIntercepts.g.cs";
        public const string MeterImplementations = "MeterImplementations.g.cs";
        public const string TracedInterceptors = "TracedIntercepts.g.cs";
        public const string AgentInterceptors = "AgentIntercepts.g.cs";
        public const string Capabilities = "QylCapabilities.g.cs";
        public const string ToolManifest = "QylToolManifest.g.cs";
        public const string HostedServiceRegistry = "QylGeneratedRegistry.g.cs";
    }

    /// <summary>
    ///     MSBuild property toggles for each generator pipeline.
    /// </summary>
    private sealed record PipelineToggles(
        bool Database,
        bool Agent,
        bool Traced,
        bool Meter);

    private readonly record struct TelemetryRegistration(
        EquatableArray<string> ActivitySources,
        EquatableArray<string> MeterNames);

    private readonly record struct CapabilityRegistration(
        EquatableArray<string> AgentNames,
        EquatableArray<string> GenAiProviders,
        EquatableArray<string> GenAiModels,
        EquatableArray<string> GenAiOperations);

    private readonly record struct GeneratedServiceRegistration(
        EquatableArray<string> ActivitySources,
        EquatableArray<string> MeterNames,
        EquatableArray<string> CapabilityAgents,
        EquatableArray<string> CapabilityGenAiProviders,
        EquatableArray<string> CapabilityGenAiModels,
        EquatableArray<string> CapabilityGenAiOperations);

    private readonly record struct BuilderInterceptorInput(
        EquatableArray<BuilderCallSite> CallSites,
        bool QylRuntimeAvailable,
        GeneratedServiceRegistration GeneratedTelemetry);

    /// <summary>
    ///     Source code templates for generated output.
    /// </summary>
    private static class CodeTemplate
    {
        public const string AutoGeneratedHeader = "// <auto-generated/>";
        public const string PragmaDisableAll = "#pragma warning disable";

        public const string InterceptsLocationAttribute = """
                                                          namespace System.Runtime.CompilerServices
                                                          {
                                                              [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                              file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute;
                                                          }
                                                          """;

        public const string InterceptorsNamespaceOpen = """
                                                        namespace Qyl.Instrumentation.Generators
                                                        {
                                                            using Qyl.Instrumentation;

                                                            file static partial class Interceptors
                                                            {
                                                        """;

        public const string InterceptorsNamespaceClose = """
                                                             }
                                                         }
                                                         """;
    }
}
