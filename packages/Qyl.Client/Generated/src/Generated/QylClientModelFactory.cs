
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Qyl.Api;
using Qyl.Api._Streaming;
using Qyl.Common;
using Qyl.Common.Pagination;
using Qyl.Domains.Alerting;
using Qyl.Domains.Configurator;
using Qyl.Domains.Identity;
using Qyl.Domains.Issues;
using Qyl.Domains.Observe.Error;
using Qyl.Domains.Observe.Log;
using Qyl.Domains.Observe.Session;
using Qyl.Domains.Ops.Deployment;
using Qyl.Domains.Search;
using Qyl.Domains.Workflow;
using Qyl.Domains.Workspace;
using Qyl.OTel.Enums;
using Qyl.OTel.Logs;
using Qyl.OTel.Metrics;
using Qyl.OTel.Resource;
using Qyl.OTel.Traces;
using Qyl.Storage;
using Trace = Qyl.OTel.Traces.Trace;

namespace Qyl.Client
{
    public static partial class QylClientModelFactory
    {
        public static CursorPageTrace CursorPageTrace(IEnumerable<Trace> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<Trace>();

            return new CursorPageTrace(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static Trace Trace(string traceId = default, IEnumerable<Span> spans = default, Span rootSpan = default, int spanCount = default, long durationNs = default, DateTimeOffset startTime = default, DateTimeOffset endTime = default, IEnumerable<string> services = default, bool hasError = default)
        {
            spans ??= new ChangeTrackingList<Span>();
            services ??= new ChangeTrackingList<string>();

            return new Trace(
                traceId,
                spans.ToList(),
                rootSpan,
                spanCount,
                durationNs,
                startTime,
                endTime,
                services.ToList(),
                hasError,
                additionalBinaryDataProperties: null);
        }

        public static Span Span(string spanId = default, string traceId = default, string parentSpanId = default, string traceState = default, string name = default, SpanKind kind = default, long startTimeUnixNano = default, long endTimeUnixNano = default, IEnumerable<Common.Attribute> attributes = default, long? droppedAttributesCount = default, IEnumerable<SpanEvent> events = default, long? droppedEventsCount = default, IEnumerable<SpanLink> links = default, long? droppedLinksCount = default, SpanStatus status = default, int? flags = default, Resource resource = default, InstrumentationScope instrumentationScope = default)
        {
            attributes ??= new ChangeTrackingList<Common.Attribute>();
            events ??= new ChangeTrackingList<SpanEvent>();
            links ??= new ChangeTrackingList<SpanLink>();

            return new Span(
                spanId,
                traceId,
                parentSpanId,
                traceState,
                name,
                kind,
                startTimeUnixNano,
                endTimeUnixNano,
                attributes.ToList(),
                droppedAttributesCount,
                events.ToList(),
                droppedEventsCount,
                links.ToList(),
                droppedLinksCount,
                status,
                flags,
                resource,
                instrumentationScope,
                additionalBinaryDataProperties: null);
        }

        public static Common.Attribute Attribute(string key = default, BinaryData value = default)
        {
            return new Common.Attribute(key, value, additionalBinaryDataProperties: null);
        }

        public static SpanEvent SpanEvent(string name = default, long timeUnixNano = default, IEnumerable<Common.Attribute> attributes = default, long? droppedAttributesCount = default)
        {
            attributes ??= new ChangeTrackingList<Common.Attribute>();

            return new SpanEvent(name, timeUnixNano, attributes.ToList(), droppedAttributesCount, additionalBinaryDataProperties: null);
        }

        public static SpanLink SpanLink(string traceId = default, string spanId = default, string traceState = default, IEnumerable<Common.Attribute> attributes = default, long? droppedAttributesCount = default, int? flags = default)
        {
            attributes ??= new ChangeTrackingList<Common.Attribute>();

            return new SpanLink(
                traceId,
                spanId,
                traceState,
                attributes.ToList(),
                droppedAttributesCount,
                flags,
                additionalBinaryDataProperties: null);
        }

        public static SpanStatus SpanStatus(SpanStatusCode code = default, string message = default)
        {
            return new SpanStatus(code, message, additionalBinaryDataProperties: null);
        }

        public static Resource Resource(string serviceName = default, string serviceNamespace = default, string serviceInstanceId = default, string serviceVersion = default, string telemetrySdkName = default, TelemetrySdkLanguage? telemetrySdkLanguage = default, string telemetrySdkVersion = default, string telemetryAutoVersion = default, string deploymentEnvironment = default, CloudProvider? cloudProvider = default, string cloudRegion = default, string cloudAvailabilityZone = default, string cloudAccountId = default, string cloudPlatform = default, string hostName = default, string hostId = default, string hostType = default, HostArch? hostArch = default, OsType? osType = default, string osDescription = default, string osVersion = default, long? processPid = default, string processExecutableName = default, string processCommandLine = default, string processRuntimeName = default, string processRuntimeVersion = default, string containerId = default, string containerName = default, string containerImageName = default, string containerImageTag = default, string k8sClusterName = default, string k8sNamespaceName = default, string k8sPodName = default, string k8sPodUid = default, string k8sDeploymentName = default, IEnumerable<Common.Attribute> attributes = default, long? droppedAttributesCount = default)
        {
            attributes ??= new ChangeTrackingList<Common.Attribute>();

            return new Resource(
                serviceName,
                serviceNamespace,
                serviceInstanceId,
                serviceVersion,
                telemetrySdkName,
                telemetrySdkLanguage,
                telemetrySdkVersion,
                telemetryAutoVersion,
                deploymentEnvironment,
                cloudProvider,
                cloudRegion,
                cloudAvailabilityZone,
                cloudAccountId,
                cloudPlatform,
                hostName,
                hostId,
                hostType,
                hostArch,
                osType,
                osDescription,
                osVersion,
                processPid,
                processExecutableName,
                processCommandLine,
                processRuntimeName,
                processRuntimeVersion,
                containerId,
                containerName,
                containerImageName,
                containerImageTag,
                k8sClusterName,
                k8sNamespaceName,
                k8sPodName,
                k8sPodUid,
                k8sDeploymentName,
                attributes.ToList(),
                droppedAttributesCount,
                additionalBinaryDataProperties: null);
        }

        public static InstrumentationScope InstrumentationScope(string scopeName = default, string scopeVersion = default, IEnumerable<Common.Attribute> scopeAttributes = default, long? droppedAttributesCount = default)
        {
            scopeAttributes ??= new ChangeTrackingList<Common.Attribute>();

            return new InstrumentationScope(scopeName, scopeVersion, scopeAttributes.ToList(), droppedAttributesCount, additionalBinaryDataProperties: null);
        }

        public static CursorPageSpanRecord CursorPageSpanRecord(IEnumerable<SpanRecord> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<SpanRecord>();

            return new CursorPageSpanRecord(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static SpanRecord SpanRecord(string spanId = default, string traceId = default, string parentSpanId = default, string sessionId = default, string name = default, SpanKind kind = default, long startTimeUnixNano = default, long endTimeUnixNano = default, long durationNs = default, SpanStatusCode statusCode = default, string statusMessage = default, string serviceName = default, string genAiProviderName = default, string genAiRequestModel = default, string genAiResponseModel = default, long? genAiInputTokens = default, long? genAiOutputTokens = default, double? genAiTemperature = default, string genAiStopReason = default, string genAiToolName = default, string genAiToolCallId = default, double? genAiCostUsd = default, string attributesJson = default, string resourceJson = default, string baggageJson = default, string schemaUrl = default, DateTimeOffset? createdAt = default)
        {
            return new SpanRecord(
                spanId,
                traceId,
                parentSpanId,
                sessionId,
                name,
                kind,
                startTimeUnixNano,
                endTimeUnixNano,
                durationNs,
                statusCode,
                statusMessage,
                serviceName,
                genAiProviderName,
                genAiRequestModel,
                genAiResponseModel,
                genAiInputTokens,
                genAiOutputTokens,
                genAiTemperature,
                genAiStopReason,
                genAiToolName,
                genAiToolCallId,
                genAiCostUsd,
                attributesJson,
                resourceJson,
                baggageJson,
                schemaUrl,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static TraceQuery TraceQuery(string query = default, string serviceName = default, string operationName = default, long? minDurationMs = default, long? maxDurationMs = default, SpanStatusCode? status = default, DateTimeOffset? startTime = default, DateTimeOffset? endTime = default, IDictionary<string, string> tags = default, int? limit = default, string cursor = default)
        {
            tags ??= new ChangeTrackingDictionary<string, string>();

            return new TraceQuery(
                query,
                serviceName,
                operationName,
                minDurationMs,
                maxDurationMs,
                status,
                startTime,
                endTime,
                tags,
                limit,
                cursor,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageLogRecord CursorPageLogRecord(IEnumerable<LogRecord> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<LogRecord>();

            return new CursorPageLogRecord(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static LogRecord LogRecord(long timeUnixNano = default, long observedTimeUnixNano = default, SeverityNumber severityNumber = default, SeverityText? severityText = default, BinaryData body = default, IEnumerable<Common.Attribute> attributes = default, long? droppedAttributesCount = default, int? flags = default, string traceId = default, string spanId = default, Resource resource = default, InstrumentationScope instrumentationScope = default)
        {
            attributes ??= new ChangeTrackingList<Common.Attribute>();

            return new LogRecord(
                timeUnixNano,
                observedTimeUnixNano,
                severityNumber,
                severityText,
                body,
                attributes.ToList(),
                droppedAttributesCount,
                flags,
                traceId,
                spanId,
                resource,
                instrumentationScope,
                additionalBinaryDataProperties: null);
        }

        public static LogBodyString LogBodyString(string stringValue = default)
        {
            return new LogBodyString(stringValue, additionalBinaryDataProperties: null);
        }

        public static LogBodyKvList LogBodyKvList(IEnumerable<Common.Attribute> kvListValue = default)
        {
            kvListValue ??= new ChangeTrackingList<Common.Attribute>();

            return new LogBodyKvList(kvListValue.ToList(), additionalBinaryDataProperties: null);
        }

        public static LogBodyArray LogBodyArray(IEnumerable<BinaryData> arrayValue = default)
        {
            arrayValue ??= new ChangeTrackingList<BinaryData>();

            return new LogBodyArray(arrayValue.ToList(), additionalBinaryDataProperties: null);
        }

        public static LogBodyBytes LogBodyBytes(BinaryData bytesValue = default)
        {
            return new LogBodyBytes(bytesValue, additionalBinaryDataProperties: null);
        }

        public static LogQuery LogQuery(string query = default, SeverityNumber? severityMin = default, string serviceName = default, string traceId = default, string spanId = default, DateTimeOffset? timeStart = default, DateTimeOffset? timeEnd = default, IEnumerable<AttributeFilter> attributeFilters = default, int? limit = default, LogOrderBy? orderBy = default)
        {
            attributeFilters ??= new ChangeTrackingList<AttributeFilter>();

            return new LogQuery(
                query,
                severityMin,
                serviceName,
                traceId,
                spanId,
                timeStart,
                timeEnd,
                attributeFilters.ToList(),
                limit,
                orderBy,
                additionalBinaryDataProperties: null);
        }

        public static AttributeFilter AttributeFilter(string key = default, FilterOperator @operator = default, string value = default)
        {
            return new AttributeFilter(key, @operator, value, additionalBinaryDataProperties: null);
        }

        public static LogAggregationRequest LogAggregationRequest(LogQuery query = default, LogAggregation aggregation = default)
        {
            return new LogAggregationRequest(query, aggregation, additionalBinaryDataProperties: null);
        }

        public static LogAggregation LogAggregation(IEnumerable<string> groupBy = default, AggregationFunction function = default, string @field = default, TimeBucket? timeBucket = default, int? topN = default)
        {
            groupBy ??= new ChangeTrackingList<string>();

            return new LogAggregation(
                groupBy.ToList(),
                function,
                @field,
                timeBucket,
                topN,
                additionalBinaryDataProperties: null);
        }

        public static LogAggregationResponse LogAggregationResponse(IEnumerable<LogAggregationBucket> results = default, long totalCount = default)
        {
            results ??= new ChangeTrackingList<LogAggregationBucket>();

            return new LogAggregationResponse(results.ToList(), totalCount, additionalBinaryDataProperties: null);
        }

        public static LogAggregationBucket LogAggregationBucket(string key = default, double value = default, long count = default, DateTimeOffset? timestamp = default)
        {
            return new LogAggregationBucket(key, value, count, timestamp, additionalBinaryDataProperties: null);
        }

        public static LogPattern LogPattern(string patternId = default, string template = default, string sample = default, long count = default, DateTimeOffset firstSeen = default, DateTimeOffset lastSeen = default, LogPatternTrend trend = default, IEnumerable<LogSeverityStats> severityDistribution = default)
        {
            severityDistribution ??= new ChangeTrackingList<LogSeverityStats>();

            return new LogPattern(
                patternId,
                template,
                sample,
                count,
                firstSeen,
                lastSeen,
                trend,
                severityDistribution.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static LogSeverityStats LogSeverityStats(SeverityNumber severity = default, string severityText = default, long count = default, double percentage = default)
        {
            return new LogSeverityStats(severity, severityText, count, percentage, additionalBinaryDataProperties: null);
        }

        public static LogStats LogStats(long totalCount = default, IEnumerable<LogCountBySeverity> bySeverity = default, IEnumerable<LogCountByDimension> byService = default, double logsPerSecond = default, double errorRate = default)
        {
            bySeverity ??= new ChangeTrackingList<LogCountBySeverity>();
            byService ??= new ChangeTrackingList<LogCountByDimension>();

            return new LogStats(
                totalCount,
                bySeverity.ToList(),
                byService.ToList(),
                logsPerSecond,
                errorRate,
                additionalBinaryDataProperties: null);
        }

        public static LogCountBySeverity LogCountBySeverity(SeverityText severity = default, long count = default, double percentage = default)
        {
            return new LogCountBySeverity(severity, count, percentage, additionalBinaryDataProperties: null);
        }

        public static LogCountByDimension LogCountByDimension(string dimension = default, long count = default, long errorCount = default)
        {
            return new LogCountByDimension(dimension, count, errorCount, additionalBinaryDataProperties: null);
        }

        public static CursorPageMetricMetadata CursorPageMetricMetadata(IEnumerable<MetricMetadata> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<MetricMetadata>();

            return new CursorPageMetricMetadata(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static MetricMetadata MetricMetadata(string name = default, string description = default, string unit = default, MetricType @type = default, IEnumerable<string> labelKeys = default, IEnumerable<string> services = default)
        {
            labelKeys ??= new ChangeTrackingList<string>();
            services ??= new ChangeTrackingList<string>();

            return new MetricMetadata(
                name,
                description,
                unit,
                @type,
                labelKeys.ToList(),
                services.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static MetricQueryRequest MetricQueryRequest(string metricName = default, IDictionary<string, string> filters = default, DateTimeOffset startTime = default, DateTimeOffset endTime = default, Common.Pagination.TimeBucket? step = default, OTel.Metrics.AggregationFunction? aggregation = default, IEnumerable<string> groupBy = default)
        {
            filters ??= new ChangeTrackingDictionary<string, string>();
            groupBy ??= new ChangeTrackingList<string>();

            return new MetricQueryRequest(
                metricName,
                filters,
                startTime,
                endTime,
                step,
                aggregation,
                groupBy.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static MetricQueryResponse MetricQueryResponse(string metricName = default, IEnumerable<MetricTimeSeries> series = default)
        {
            series ??= new ChangeTrackingList<MetricTimeSeries>();

            return new MetricQueryResponse(metricName, series.ToList(), additionalBinaryDataProperties: null);
        }

        public static MetricTimeSeries MetricTimeSeries(IDictionary<string, string> labels = default, IEnumerable<MetricDataPoint> points = default)
        {
            labels ??= new ChangeTrackingDictionary<string, string>();
            points ??= new ChangeTrackingList<MetricDataPoint>();

            return new MetricTimeSeries(labels, points.ToList(), additionalBinaryDataProperties: null);
        }

        public static MetricDataPoint MetricDataPoint(DateTimeOffset timestamp = default, double value = default)
        {
            return new MetricDataPoint(timestamp, value, additionalBinaryDataProperties: null);
        }

        public static ProfileRecord ProfileRecord(string profileId = default, string traceId = default, string spanId = default, string sessionId = default, long timeUnixNano = default, long durationNano = default, int sampleCount = default, string sampleType = default, string sampleUnit = default, string originalPayloadFormat = default, string serviceName = default, string profileFrameType = default, string attributesJson = default, string resourceJson = default, string profileDataJson = default, string schemaUrl = default, DateTimeOffset? createdAt = default)
        {
            return new ProfileRecord(
                profileId,
                traceId,
                spanId,
                sessionId,
                timeUnixNano,
                durationNano,
                sampleCount,
                sampleType,
                sampleUnit,
                originalPayloadFormat,
                serviceName,
                profileFrameType,
                attributesJson,
                resourceJson,
                profileDataJson,
                schemaUrl,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageSessionEntity CursorPageSessionEntity(IEnumerable<SessionEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<SessionEntity>();

            return new CursorPageSessionEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static SessionEntity SessionEntity(string sessionId = default, string userId = default, DateTimeOffset startTime = default, DateTimeOffset? endTime = default, double? durationMs = default, int traceCount = default, int spanCount = default, int errorCount = default, IEnumerable<string> services = default, SessionState state = default, SessionClientInfo client = default, SessionGeoInfo geo = default, SessionGenAiUsage genaiUsage = default)
        {
            services ??= new ChangeTrackingList<string>();

            return new SessionEntity(
                sessionId,
                userId,
                startTime,
                endTime,
                durationMs,
                traceCount,
                spanCount,
                errorCount,
                services.ToList(),
                state,
                client,
                geo,
                genaiUsage,
                additionalBinaryDataProperties: null);
        }

        public static SessionClientInfo SessionClientInfo(string ip = default, string userAgent = default, DeviceType? deviceType = default, string os = default, string browser = default, string browserVersion = default)
        {
            return new SessionClientInfo(
                ip,
                userAgent,
                deviceType,
                os,
                browser,
                browserVersion,
                additionalBinaryDataProperties: null);
        }

        public static SessionGeoInfo SessionGeoInfo(string countryCode = default, string countryName = default, string region = default, string city = default, string postalCode = default, string timezone = default)
        {
            return new SessionGeoInfo(
                countryCode,
                countryName,
                region,
                city,
                postalCode,
                timezone,
                additionalBinaryDataProperties: null);
        }

        public static SessionGenAiUsage SessionGenAiUsage(int requestCount = default, long totalInputTokens = default, long totalOutputTokens = default, IEnumerable<string> modelsUsed = default, IEnumerable<string> providersUsed = default, double? estimatedCostUsd = default)
        {
            modelsUsed ??= new ChangeTrackingList<string>();
            providersUsed ??= new ChangeTrackingList<string>();

            return new SessionGenAiUsage(
                requestCount,
                totalInputTokens,
                totalOutputTokens,
                modelsUsed.ToList(),
                providersUsed.ToList(),
                estimatedCostUsd,
                additionalBinaryDataProperties: null);
        }

        public static SessionStats SessionStats(long activeSessions = default, long totalSessions = default, long uniqueUsers = default, double avgDurationMs = default, long sessionsWithErrors = default, long sessionsWithGenAi = default, double bounceRate = default, IEnumerable<SessionDeviceStats> byDeviceType = default, IEnumerable<SessionCountryStats> byCountry = default)
        {
            byDeviceType ??= new ChangeTrackingList<SessionDeviceStats>();
            byCountry ??= new ChangeTrackingList<SessionCountryStats>();

            return new SessionStats(
                activeSessions,
                totalSessions,
                uniqueUsers,
                avgDurationMs,
                sessionsWithErrors,
                sessionsWithGenAi,
                bounceRate,
                byDeviceType.ToList(),
                byCountry.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static SessionDeviceStats SessionDeviceStats(DeviceType deviceType = default, long count = default, double percentage = default)
        {
            return new SessionDeviceStats(deviceType, count, percentage, additionalBinaryDataProperties: null);
        }

        public static SessionCountryStats SessionCountryStats(string countryCode = default, string countryName = default, long count = default, double percentage = default)
        {
            return new SessionCountryStats(countryCode, countryName, count, percentage, additionalBinaryDataProperties: null);
        }

        public static CursorPageErrorEntity CursorPageErrorEntity(IEnumerable<ErrorEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<ErrorEntity>();

            return new CursorPageErrorEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static ErrorEntity ErrorEntity(string errorId = default, string errorType = default, string message = default, ErrorCategory category = default, string fingerprint = default, DateTimeOffset firstSeen = default, DateTimeOffset lastSeen = default, long occurrenceCount = default, long? affectedUsers = default, IEnumerable<string> affectedServices = default, ErrorStatus status = default, string assignedTo = default, string issueUrl = default, IEnumerable<string> sampleTraces = default)
        {
            affectedServices ??= new ChangeTrackingList<string>();
            sampleTraces ??= new ChangeTrackingList<string>();

            return new ErrorEntity(
                errorId,
                errorType,
                message,
                category,
                fingerprint,
                firstSeen,
                lastSeen,
                occurrenceCount,
                affectedUsers,
                affectedServices.ToList(),
                status,
                assignedTo,
                issueUrl,
                sampleTraces.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static ErrorStats ErrorStats(long totalCount = default, int uniqueTypes = default, double errorRate = default, IEnumerable<ErrorCategoryStats> byCategory = default, IEnumerable<ErrorServiceStats> byService = default, IEnumerable<ErrorTypeStats> topErrors = default, ErrorTrend trend = default)
        {
            byCategory ??= new ChangeTrackingList<ErrorCategoryStats>();
            byService ??= new ChangeTrackingList<ErrorServiceStats>();
            topErrors ??= new ChangeTrackingList<ErrorTypeStats>();

            return new ErrorStats(
                totalCount,
                uniqueTypes,
                errorRate,
                byCategory.ToList(),
                byService.ToList(),
                topErrors.ToList(),
                trend,
                additionalBinaryDataProperties: null);
        }

        public static ErrorCategoryStats ErrorCategoryStats(ErrorCategory category = default, long count = default, double percentage = default)
        {
            return new ErrorCategoryStats(category, count, percentage, additionalBinaryDataProperties: null);
        }

        public static ErrorServiceStats ErrorServiceStats(string serviceName = default, long count = default, double errorRate = default, string topErrorType = default)
        {
            return new ErrorServiceStats(serviceName, count, errorRate, topErrorType, additionalBinaryDataProperties: null);
        }

        public static ErrorTypeStats ErrorTypeStats(string errorType = default, long count = default, double percentage = default, long? affectedUsers = default, ErrorStatus status = default)
        {
            return new ErrorTypeStats(
                errorType,
                count,
                percentage,
                affectedUsers,
                status,
                additionalBinaryDataProperties: null);
        }

        public static ErrorCorrelation ErrorCorrelation(string errorId = default, IEnumerable<CorrelatedError> correlatedErrors = default, string rootCause = default, IEnumerable<Common.Attribute> commonAttributes = default)
        {
            correlatedErrors ??= new ChangeTrackingList<CorrelatedError>();
            commonAttributes ??= new ChangeTrackingList<Common.Attribute>();

            return new ErrorCorrelation(errorId, correlatedErrors.ToList(), rootCause, commonAttributes.ToList(), additionalBinaryDataProperties: null);
        }

        public static CorrelatedError CorrelatedError(string errorId = default, string errorType = default, double correlationStrength = default, TemporalRelationship temporalRelationship = default)
        {
            return new CorrelatedError(errorId, errorType, correlationStrength, temporalRelationship, additionalBinaryDataProperties: null);
        }

        public static CursorPageDeploymentEntity CursorPageDeploymentEntity(IEnumerable<DeploymentEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<DeploymentEntity>();

            return new CursorPageDeploymentEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static DeploymentEntity DeploymentEntity(string deploymentId = default, string serviceName = default, string serviceVersion = default, DeploymentEnvironment environment = default, DeploymentStatus status = default, DeploymentStrategy strategy = default, DateTimeOffset startTime = default, DateTimeOffset? endTime = default, double? durationS = default, string deployedBy = default, string gitCommit = default, string gitBranch = default, string previousVersion = default, string rollbackTarget = default, int? replicaCount = default, int? healthyReplicas = default, string errorMessage = default)
        {
            return new DeploymentEntity(
                deploymentId,
                serviceName,
                serviceVersion,
                environment,
                status,
                strategy,
                startTime,
                endTime,
                durationS,
                deployedBy,
                gitCommit,
                gitBranch,
                previousVersion,
                rollbackTarget,
                replicaCount,
                healthyReplicas,
                errorMessage,
                additionalBinaryDataProperties: null);
        }

        public static DeploymentCreate DeploymentCreate(string serviceName = default, string serviceVersion = default, DeploymentEnvironment environment = default, DeploymentStrategy strategy = default, string deployedBy = default, string gitCommit = default, string gitBranch = default)
        {
            return new DeploymentCreate(
                serviceName,
                serviceVersion,
                environment,
                strategy,
                deployedBy,
                gitCommit,
                gitBranch,
                additionalBinaryDataProperties: null);
        }

        public static DoraMetrics DoraMetrics(double deploymentFrequency = default, double leadTimeHours = default, double changeFailureRate = default, double mttrHours = default, DoraPerformanceLevel performanceLevel = default)
        {
            return new DoraMetrics(
                deploymentFrequency,
                leadTimeHours,
                changeFailureRate,
                mttrHours,
                performanceLevel,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageServiceInfo CursorPageServiceInfo(IEnumerable<ServiceInfo> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<ServiceInfo>();

            return new CursorPageServiceInfo(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static ServiceInfo ServiceInfo(string name = default, string namespaceName = default, string version = default, int instanceCount = default, DateTimeOffset lastSeen = default)
        {
            return new ServiceInfo(
                name,
                namespaceName,
                version,
                instanceCount,
                lastSeen,
                additionalBinaryDataProperties: null);
        }

        public static ServiceDetails ServiceDetails(string name = default, string namespaceName = default, string version = default, int instanceCount = default, DateTimeOffset lastSeen = default, IEnumerable<Common.Attribute> resourceAttributes = default, IEnumerable<InstrumentationScope> instrumentationLibraries = default, double requestRate = default, double errorRate = default, double avgLatencyMs = default, double p99LatencyMs = default)
        {
            resourceAttributes ??= new ChangeTrackingList<Common.Attribute>();
            instrumentationLibraries ??= new ChangeTrackingList<InstrumentationScope>();

            return new ServiceDetails(
                name,
                namespaceName,
                version,
                instanceCount,
                lastSeen,
                resourceAttributes.ToList(),
                instrumentationLibraries.ToList(),
                requestRate,
                errorRate,
                avgLatencyMs,
                p99LatencyMs,
                additionalBinaryDataProperties: null);
        }

        public static ServiceDependency ServiceDependency(string sourceService = default, string targetService = default, long requestCount = default, double errorRate = default, double avgLatencyMs = default)
        {
            return new ServiceDependency(
                sourceService,
                targetService,
                requestCount,
                errorRate,
                avgLatencyMs,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageOperationInfo CursorPageOperationInfo(IEnumerable<OperationInfo> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<OperationInfo>();

            return new CursorPageOperationInfo(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static OperationInfo OperationInfo(string name = default, SpanKind spanKind = default, long requestCount = default, long errorCount = default, double avgDurationMs = default, double p99DurationMs = default)
        {
            return new OperationInfo(
                name,
                spanKind,
                requestCount,
                errorCount,
                avgDurationMs,
                p99DurationMs,
                additionalBinaryDataProperties: null);
        }

        public static WorkspaceEnvelopeEntity WorkspaceEnvelopeEntity(string id = default, string projectId = default, string environmentId = default, string nodeId = default, string name = default, string rootPath = default, DateTimeOffset? heartbeatAt = default, int heartbeatIntervalSeconds = default, WorkspaceStatus status = default, string configJson = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
        {
            return new WorkspaceEnvelopeEntity(
                id,
                projectId,
                environmentId,
                nodeId,
                name,
                rootPath,
                heartbeatAt,
                heartbeatIntervalSeconds,
                status,
                configJson,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageProjectEntity CursorPageProjectEntity(IEnumerable<ProjectEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<ProjectEntity>();

            return new CursorPageProjectEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static ProjectEntity ProjectEntity(string id = default, string name = default, string slug = default, string description = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default, DateTimeOffset? archivedAt = default)
        {
            return new ProjectEntity(
                id,
                name,
                slug,
                description,
                createdAt,
                updatedAt,
                archivedAt,
                additionalBinaryDataProperties: null);
        }

        public static ProjectCreateRequest ProjectCreateRequest(string name = default, string slug = default, string description = default)
        {
            return new ProjectCreateRequest(name, slug, description, additionalBinaryDataProperties: null);
        }

        public static ProjectEnvironmentEntity ProjectEnvironmentEntity(string id = default, string projectId = default, string name = default, string displayName = default, string color = default, int sortOrder = default, DateTimeOffset createdAt = default)
        {
            return new ProjectEnvironmentEntity(
                id,
                projectId,
                name,
                displayName,
                color,
                sortOrder,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static HandshakeStartRequest HandshakeStartRequest(string codeChallenge = default, string clientId = default)
        {
            return new HandshakeStartRequest(codeChallenge, clientId, additionalBinaryDataProperties: null);
        }

        public static HandshakeSessionEntity HandshakeSessionEntity(string id = default, string workspaceId = default, string challenge = default, string challengeMethod = default, string browserFingerprint = default, string originUrl = default, HandshakeState state = default, DateTimeOffset? verifiedAt = default, DateTimeOffset expiresAt = default, DateTimeOffset createdAt = default)
        {
            return new HandshakeSessionEntity(
                id,
                workspaceId,
                challenge,
                challengeMethod,
                browserFingerprint,
                originUrl,
                state,
                verifiedAt,
                expiresAt,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static HandshakeVerifyRequest HandshakeVerifyRequest(string codeVerifier = default, string code = default)
        {
            return new HandshakeVerifyRequest(codeVerifier, code, additionalBinaryDataProperties: null);
        }

        public static HandshakeVerifyResponse HandshakeVerifyResponse(string accessToken = default, DateTimeOffset expiresAt = default, string workspaceId = default)
        {
            return new HandshakeVerifyResponse(accessToken, expiresAt, workspaceId, additionalBinaryDataProperties: null);
        }

        public static CursorPageGenerationProfileEntity CursorPageGenerationProfileEntity(IEnumerable<GenerationProfileEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<GenerationProfileEntity>();

            return new CursorPageGenerationProfileEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static GenerationProfileEntity GenerationProfileEntity(string id = default, string projectId = default, string name = default, string description = default, string targetFramework = default, string targetLanguage = default, string semconvVersion = default, string featuresJson = default, string templateOverridesJson = default, bool isDefault = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
        {
            return new GenerationProfileEntity(
                id,
                projectId,
                name,
                description,
                targetFramework,
                targetLanguage,
                semconvVersion,
                featuresJson,
                templateOverridesJson,
                isDefault,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties: null);
        }

        public static GenerationProfileCreateRequest GenerationProfileCreateRequest(string name = default, string targetFramework = default, string description = default, string featuresJson = default)
        {
            return new GenerationProfileCreateRequest(name, targetFramework, description, featuresJson, additionalBinaryDataProperties: null);
        }

        public static GenerationSelectionEntity GenerationSelectionEntity(string id = default, string workspaceId = default, string profileId = default, string selectionType = default, string selectionKey = default, bool enabled = default, string configJson = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
        {
            return new GenerationSelectionEntity(
                id,
                workspaceId,
                profileId,
                selectionType,
                selectionKey,
                enabled,
                configJson,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties: null);
        }

        public static GenerationSelectionSaveRequest GenerationSelectionSaveRequest(string workspaceId = default, string profileId = default, string selectedKeysJson = default)
        {
            return new GenerationSelectionSaveRequest(workspaceId, profileId, selectedKeysJson, additionalBinaryDataProperties: null);
        }

        public static GenerationJobCreateRequest GenerationJobCreateRequest(string workspaceId = default, string profileId = default, GenerationJobType jobType = default)
        {
            return new GenerationJobCreateRequest(workspaceId, profileId, jobType, additionalBinaryDataProperties: null);
        }

        public static GenerationJobEntity GenerationJobEntity(string id = default, string workspaceId = default, string profileId = default, GenerationJobType jobType = default, JobStatus status = default, int priority = default, string inputHash = default, string outputPath = default, string outputHash = default, string errorMessage = default, DateTimeOffset queuedAt = default, DateTimeOffset? startedAt = default, DateTimeOffset? completedAt = default, int? durationMs = default)
        {
            return new GenerationJobEntity(
                id,
                workspaceId,
                profileId,
                jobType,
                status,
                priority,
                inputHash,
                outputPath,
                outputHash,
                errorMessage,
                queuedAt,
                startedAt,
                completedAt,
                durationMs,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageErrorIssueEntity CursorPageErrorIssueEntity(IEnumerable<ErrorIssueEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<ErrorIssueEntity>();

            return new CursorPageErrorIssueEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static ErrorIssueEntity ErrorIssueEntity(string id = default, string projectId = default, string fingerprint = default, string title = default, string culprit = default, string errorType = default, string category = default, IssueLevel level = default, string platform = default, DateTimeOffset firstSeenAt = default, DateTimeOffset lastSeenAt = default, long occurrenceCount = default, int affectedUsersCount = default, IssueStatus status = default, string substatus = default, IssuePriority priority = default, string assignedTo = default, DateTimeOffset? resolvedAt = default, string resolvedBy = default, int regressionCount = default, string lastRelease = default, string tagsJson = default, string metadataJson = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
        {
            return new ErrorIssueEntity(
                id,
                projectId,
                fingerprint,
                title,
                culprit,
                errorType,
                category,
                level,
                platform,
                firstSeenAt,
                lastSeenAt,
                occurrenceCount,
                affectedUsersCount,
                status,
                substatus,
                priority,
                assignedTo,
                resolvedAt,
                resolvedBy,
                regressionCount,
                lastRelease,
                tagsJson,
                metadataJson,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageErrorIssueEventEntity CursorPageErrorIssueEventEntity(IEnumerable<ErrorIssueEventEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<ErrorIssueEventEntity>();

            return new CursorPageErrorIssueEventEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static ErrorIssueEventEntity ErrorIssueEventEntity(string id = default, string issueId = default, string traceId = default, string spanId = default, string message = default, string stackTrace = default, string stackFramesJson = default, string environment = default, string releaseVersion = default, string userId = default, string userIp = default, string requestUrl = default, string requestMethod = default, string browser = default, string os = default, string device = default, string runtime = default, string runtimeVersion = default, string contextJson = default, string tagsJson = default, DateTimeOffset timestamp = default)
        {
            return new ErrorIssueEventEntity(
                id,
                issueId,
                traceId,
                spanId,
                message,
                stackTrace,
                stackFramesJson,
                environment,
                releaseVersion,
                userId,
                userIp,
                requestUrl,
                requestMethod,
                browser,
                os,
                device,
                runtime,
                runtimeVersion,
                contextJson,
                tagsJson,
                timestamp,
                additionalBinaryDataProperties: null);
        }

        public static ErrorBreadcrumbEntity ErrorBreadcrumbEntity(string id = default, string eventId = default, BreadcrumbType breadcrumbType = default, string category = default, string message = default, string level = default, string dataJson = default, DateTimeOffset timestamp = default)
        {
            return new ErrorBreadcrumbEntity(
                id,
                eventId,
                breadcrumbType,
                category,
                message,
                level,
                dataJson,
                timestamp,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageWorkflowRunEntity CursorPageWorkflowRunEntity(IEnumerable<WorkflowRunEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<WorkflowRunEntity>();

            return new CursorPageWorkflowRunEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static WorkflowRunEntity WorkflowRunEntity(string id = default, string workflowId = default, int workflowVersion = default, string projectId = default, WorkflowTriggerType triggerType = default, string triggerSource = default, string inputJson = default, string outputJson = default, WorkflowRunStatus status = default, string errorMessage = default, string parentRunId = default, string correlationId = default, DateTimeOffset? startedAt = default, DateTimeOffset? completedAt = default, int? durationMs = default, DateTimeOffset createdAt = default)
        {
            return new WorkflowRunEntity(
                id,
                workflowId,
                workflowVersion,
                projectId,
                triggerType,
                triggerSource,
                inputJson,
                outputJson,
                status,
                errorMessage,
                parentRunId,
                correlationId,
                startedAt,
                completedAt,
                durationMs,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageWorkflowNodeEntity CursorPageWorkflowNodeEntity(IEnumerable<WorkflowNodeEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<WorkflowNodeEntity>();

            return new CursorPageWorkflowNodeEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static WorkflowNodeEntity WorkflowNodeEntity(string id = default, string runId = default, string nodeId = default, WorkflowNodeType nodeType = default, string nodeName = default, int attempt = default, string inputJson = default, string outputJson = default, WorkflowRunStatus status = default, string errorMessage = default, int retryCount = default, int maxRetries = default, int? timeoutMs = default, DateTimeOffset? startedAt = default, DateTimeOffset? completedAt = default, int? durationMs = default, DateTimeOffset createdAt = default)
        {
            return new WorkflowNodeEntity(
                id,
                runId,
                nodeId,
                nodeType,
                nodeName,
                attempt,
                inputJson,
                outputJson,
                status,
                errorMessage,
                retryCount,
                maxRetries,
                timeoutMs,
                startedAt,
                completedAt,
                durationMs,
                createdAt,
                additionalBinaryDataProperties: null);
        }

        public static WorkflowEventEntity WorkflowEventEntity(string id = default, string runId = default, string nodeId = default, string eventType = default, string eventName = default, string payloadJson = default, long sequenceNumber = default, string source = default, string correlationId = default, DateTimeOffset timestamp = default)
        {
            return new WorkflowEventEntity(
                id,
                runId,
                nodeId,
                eventType,
                eventName,
                payloadJson,
                sequenceNumber,
                source,
                correlationId,
                timestamp,
                additionalBinaryDataProperties: null);
        }

        public static SearchRequest SearchRequest(string query = default, IEnumerable<SearchEntityType> entityTypes = default, string projectId = default, int? limit = default, string cursor = default)
        {
            entityTypes ??= new ChangeTrackingList<SearchEntityType>();

            return new SearchRequest(
                query,
                entityTypes.ToList(),
                projectId,
                limit,
                cursor,
                additionalBinaryDataProperties: null);
        }

        public static SearchResponse SearchResponse(IEnumerable<SearchResult> results = default, long totalCount = default, int durationMs = default, string nextCursor = default, IEnumerable<string> suggestions = default)
        {
            results ??= new ChangeTrackingList<SearchResult>();
            suggestions ??= new ChangeTrackingList<string>();

            return new SearchResponse(
                results.ToList(),
                totalCount,
                durationMs,
                nextCursor,
                suggestions.ToList(),
                additionalBinaryDataProperties: null);
        }

        public static SearchResult SearchResult(string documentId = default, SearchEntityType entityType = default, string entityId = default, string title = default, string snippet = default, double score = default, string url = default)
        {
            return new SearchResult(
                documentId,
                entityType,
                entityId,
                title,
                snippet,
                score,
                url,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageAlertRuleEntity CursorPageAlertRuleEntity(IEnumerable<AlertRuleEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<AlertRuleEntity>();

            return new CursorPageAlertRuleEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static AlertRuleEntity AlertRuleEntity(string id = default, string projectId = default, string name = default, string description = default, AlertRuleType ruleType = default, string conditionJson = default, string thresholdJson = default, string targetType = default, string targetFilterJson = default, AlertSeverity severity = default, int cooldownSeconds = default, string notificationChannelsJson = default, bool enabled = default, DateTimeOffset? lastTriggeredAt = default, long triggerCount = default, DateTimeOffset createdAt = default, DateTimeOffset updatedAt = default)
        {
            return new AlertRuleEntity(
                id,
                projectId,
                name,
                description,
                ruleType,
                conditionJson,
                thresholdJson,
                targetType,
                targetFilterJson,
                severity,
                cooldownSeconds,
                notificationChannelsJson,
                enabled,
                lastTriggeredAt,
                triggerCount,
                createdAt,
                updatedAt,
                additionalBinaryDataProperties: null);
        }

        public static CursorPageAlertFiringEntity CursorPageAlertFiringEntity(IEnumerable<AlertFiringEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<AlertFiringEntity>();

            return new CursorPageAlertFiringEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static AlertFiringEntity AlertFiringEntity(string id = default, string ruleId = default, string fingerprint = default, AlertSeverity severity = default, string title = default, string message = default, double? triggerValue = default, double? thresholdValue = default, string contextJson = default, AlertFiringStatus status = default, DateTimeOffset? acknowledgedAt = default, string acknowledgedBy = default, DateTimeOffset? resolvedAt = default, DateTimeOffset firedAt = default, string dedupKey = default)
        {
            return new AlertFiringEntity(
                id,
                ruleId,
                fingerprint,
                severity,
                title,
                message,
                triggerValue,
                thresholdValue,
                contextJson,
                status,
                acknowledgedAt,
                acknowledgedBy,
                resolvedAt,
                firedAt,
                dedupKey,
                additionalBinaryDataProperties: null);
        }

        public static AlertFiringAcknowledgement AlertFiringAcknowledgement(string acknowledgedBy = default)
        {
            return new AlertFiringAcknowledgement(acknowledgedBy, additionalBinaryDataProperties: null);
        }

        public static CursorPageFixRunEntity CursorPageFixRunEntity(IEnumerable<FixRunEntity> items = default, string nextCursor = default, string prevCursor = default, bool hasMore = default)
        {
            items ??= new ChangeTrackingList<FixRunEntity>();

            return new CursorPageFixRunEntity(items.ToList(), nextCursor, prevCursor, hasMore, additionalBinaryDataProperties: null);
        }

        public static FixRunEntity FixRunEntity(string id = default, string issueId = default, string alertFiringId = default, FixTriggerType triggerType = default, string strategy = default, string modelName = default, string modelProvider = default, FixRunStatus status = default, string errorMessage = default, int? tokensUsed = default, int? durationMs = default, DateTimeOffset createdAt = default, DateTimeOffset? startedAt = default, DateTimeOffset? completedAt = default)
        {
            return new FixRunEntity(
                id,
                issueId,
                alertFiringId,
                triggerType,
                strategy,
                modelName,
                modelProvider,
                status,
                errorMessage,
                tokensUsed,
                durationMs,
                createdAt,
                startedAt,
                completedAt,
                additionalBinaryDataProperties: null);
        }
    }
}
