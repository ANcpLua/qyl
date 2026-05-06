import type { AlertFiringAcknowledgement, AlertFiringEntity, AlertRuleEntity, Attribute, AttributeFilter, AttributeValue, CorrelatedError, CreateDeploymentEntity, CreateGenerationJobEntity, CreateGenerationProfileEntity, CreateProjectEntity, CursorPage, CursorPage_10, CursorPage_11, CursorPage_12, CursorPage_13, CursorPage_14, CursorPage_15, CursorPage_16, CursorPage_17, CursorPage_18, CursorPage_2, CursorPage_3, CursorPage_4, CursorPage_5, CursorPage_6, CursorPage_7, CursorPage_8, CursorPage_9, DeploymentEntity, DeploymentEntityMergePatchUpdate, DoraMetrics, ErrorBreadcrumbEntity, ErrorCategoryStats, ErrorCorrelation, ErrorEntity, ErrorEntityMergePatchUpdate, ErrorIssueEntity, ErrorIssueEntityMergePatchUpdate, ErrorIssueEventEntity, ErrorServiceStats, ErrorStats, ErrorTypeStats, FixRunEntity, GenerationJobEntity, GenerationProfileEntity, GenerationSelectionEntity, GenerationSelectionSaveRequest, HandshakeSessionEntity, HandshakeStartRequest, HandshakeVerifyRequest, HandshakeVerifyResponse, InstrumentationScope, LogAggregation, LogAggregationBucket, LogAggregationRequest, LogAggregationResponse, LogBody, LogBodyArray, LogBodyBytes, LogBodyKvList, LogBodyString, LogCountByDimension, LogCountBySeverity, LogPattern, LogQuery, LogRecord, LogSeverityStats, LogStats, MetricDataPoint, MetricMetadata, MetricQueryRequest, MetricQueryResponse, MetricTimeSeries, OperationInfo, ProfileRecord, ProjectEntity, ProjectEnvironmentEntity, Resource, SearchEntityType, SearchRequest, SearchResponse, SearchResult, ServiceDependency, ServiceDetails, ServiceInfo, SessionClientInfo, SessionCountryStats, SessionDeviceStats, SessionEntity, SessionGenAiUsage, SessionGeoInfo, SessionStats, Span, SpanEvent, SpanLink, SpanRecord, SpanStatus, StreamEventType, Trace, TraceQuery, WorkflowEventEntity, WorkflowNodeEntity, WorkflowRunEntity, WorkspaceEnvelopeEntity } from "../models.js";

export function decodeBase64(value: string): Uint8Array | undefined {
  if(!value) {
    return value as any;
  }
  // Normalize Base64URL to Base64
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
    .padEnd(value.length + (4 - (value.length % 4)) % 4, '=');

  return new Uint8Array(Buffer.from(base64, 'base64'));
}export function encodeUint8Array(
  value: Uint8Array | undefined | null,
  encoding: BufferEncoding,
): string | undefined {
  if (!value) {
    return value as any;
  }
  return Buffer.from(value).toString(encoding);
}export function dateDeserializer(date?: string | null): Date {
  if (!date) {
    return date as any;
  }

  return new Date(date);
}export function dateRfc7231Deserializer(date?: string | null): Date {
  if (!date) {
    return date as any;
  }

  return new Date(date);
}export function dateRfc3339Serializer(date?: Date | null): string {
  if (!date) {
    return date as any
  }

  return date.toISOString();
}export function dateRfc7231Serializer(date?: Date | null): string {
  if (!date) {
    return date as any;
  }

  return date.toUTCString();
}export function dateUnixTimestampSerializer(date?: Date | null): number {
  if (!date) {
    return date as any;
  }

  return Math.floor(date.getTime() / 1000);
}export function dateUnixTimestampDeserializer(date?: number | null): Date {
  if (!date) {
    return date as any;
  }

  return new Date(date * 1000);
}export function createRulePayloadToTransport(payload: AlertRuleEntity) {
  return jsonAlertRuleEntityToTransportTransform(payload)!;
}export function updateRulePayloadToTransport(payload: AlertRuleEntity) {
  return jsonAlertRuleEntityToTransportTransform(payload)!;
}export function acknowledgeFiringPayloadToTransport(
  payload: AlertFiringAcknowledgement,
) {
  return jsonAlertFiringAcknowledgementToTransportTransform(payload)!;
}export function searchPayloadToTransport(payload: SearchRequest) {
  return jsonSearchRequestToTransportTransform(payload)!;
}export function updatePayloadToTransport(
  payload: ErrorIssueEntityMergePatchUpdate,
) {
  return jsonErrorIssueEntityMergePatchUpdateToTransportTransform(payload)!;
}export function createProfilePayloadToTransport(
  payload: CreateGenerationProfileEntity,
) {
  return jsonCreateGenerationProfileEntityToTransportTransform(payload)!;
}export function saveSelectionsPayloadToTransport(
  payload: GenerationSelectionSaveRequest,
) {
  return jsonGenerationSelectionSaveRequestToTransportTransform(payload)!;
}export function createJobPayloadToTransport(
  payload: CreateGenerationJobEntity,
) {
  return jsonCreateGenerationJobEntityToTransportTransform(payload)!;
}export function startHandshakePayloadToTransport(
  payload: HandshakeStartRequest,
) {
  return jsonHandshakeStartRequestToTransportTransform(payload)!;
}export function verifyHandshakePayloadToTransport(
  payload: HandshakeVerifyRequest,
) {
  return jsonHandshakeVerifyRequestToTransportTransform(payload)!;
}export function createProjectPayloadToTransport(payload: CreateProjectEntity) {
  return jsonCreateProjectEntityToTransportTransform(payload)!;
}export function createPayloadToTransport(payload: CreateDeploymentEntity) {
  return jsonCreateDeploymentEntityToTransportTransform(payload)!;
}export function updatePayloadToTransport_2(
  payload: DeploymentEntityMergePatchUpdate,
) {
  return jsonDeploymentEntityMergePatchUpdateToTransportTransform(payload)!;
}export function updatePayloadToTransport_3(
  payload: ErrorEntityMergePatchUpdate,
) {
  return jsonErrorEntityMergePatchUpdateToTransportTransform(payload)!;
}export function queryPayloadToTransport(payload: MetricQueryRequest) {
  return jsonMetricQueryRequestToTransportTransform(payload)!;
}export function searchPayloadToTransport_2(payload: LogQuery) {
  return jsonLogQueryToTransportTransform(payload)!;
}export function aggregatePayloadToTransport(payload: LogAggregationRequest) {
  return jsonLogAggregationRequestToTransportTransform(payload)!;
}export function searchPayloadToTransport_3(payload: TraceQuery) {
  return jsonTraceQueryToTransportTransform(payload)!;
}export function jsonArrayStreamEventTypeToTransportTransform(
  items_?: Array<StreamEventType> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayStreamEventTypeToApplicationTransform(
  items_?: any,
): Array<StreamEventType> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonCursorPageToTransportTransform(
  input_?: CursorPage | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayTraceToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform(
  input_?: any,
): CursorPage {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayTraceToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayTraceToTransportTransform(
  items_?: Array<Trace> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonTraceToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayTraceToApplicationTransform(
  items_?: any,
): Array<Trace> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonTraceToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonTraceToTransportTransform(input_?: Trace | null): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    trace_id: input_.traceId,spans: jsonArraySpanToTransportTransform(input_.spans),root_span: jsonSpanToTransportTransform(input_.rootSpan),span_count: input_.spanCount,duration_ns: input_.durationNs,start_time: dateRfc3339Serializer(input_.startTime),end_time: dateRfc3339Serializer(input_.endTime),services: jsonArrayStringToTransportTransform(input_.services),has_error: input_.hasError
  }!;
}export function jsonTraceToApplicationTransform(input_?: any): Trace {
  if(!input_) {
    return input_ as any;
  }
    return {
    traceId: input_.trace_id,spans: jsonArraySpanToApplicationTransform(input_.spans),rootSpan: jsonSpanToApplicationTransform(input_.root_span),spanCount: input_.span_count,durationNs: input_.duration_ns,startTime: dateDeserializer(input_.start_time)!,endTime: dateDeserializer(input_.end_time)!,services: jsonArrayStringToApplicationTransform(input_.services),hasError: input_.has_error
  }!;
}export function jsonArraySpanToTransportTransform(
  items_?: Array<Span> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySpanToApplicationTransform(
  items_?: any,
): Array<Span> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSpanToTransportTransform(input_?: Span | null): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    span_id: input_.spanId,trace_id: input_.traceId,parent_span_id: input_.parentSpanId,trace_state: input_.traceState,name: input_.name,kind: input_.kind,start_time_unix_nano: input_.startTimeUnixNano,end_time_unix_nano: input_.endTimeUnixNano,attributes: jsonArrayAttributeToTransportTransform(input_.attributes),dropped_attributes_count: input_.droppedAttributesCount,events: jsonArraySpanEventToTransportTransform(input_.events),dropped_events_count: input_.droppedEventsCount,links: jsonArraySpanLinkToTransportTransform(input_.links),dropped_links_count: input_.droppedLinksCount,status: jsonSpanStatusToTransportTransform(input_.status),flags: input_.flags,resource: jsonResourceToTransportTransform(input_.resource),instrumentation_scope: jsonInstrumentationScopeToTransportTransform(input_.instrumentationScope)
  }!;
}export function jsonSpanToApplicationTransform(input_?: any): Span {
  if(!input_) {
    return input_ as any;
  }
    return {
    spanId: input_.span_id,traceId: input_.trace_id,parentSpanId: input_.parent_span_id,traceState: input_.trace_state,name: input_.name,kind: input_.kind,startTimeUnixNano: input_.start_time_unix_nano,endTimeUnixNano: input_.end_time_unix_nano,attributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count,events: jsonArraySpanEventToApplicationTransform(input_.events),droppedEventsCount: input_.dropped_events_count,links: jsonArraySpanLinkToApplicationTransform(input_.links),droppedLinksCount: input_.dropped_links_count,status: jsonSpanStatusToApplicationTransform(input_.status),flags: input_.flags,resource: jsonResourceToApplicationTransform(input_.resource),instrumentationScope: jsonInstrumentationScopeToApplicationTransform(input_.instrumentation_scope)
  }!;
}export function jsonArrayAttributeToTransportTransform(
  items_?: Array<Attribute> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayAttributeToApplicationTransform(
  items_?: any,
): Array<Attribute> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonAttributeToTransportTransform(
  input_?: Attribute | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,value: jsonAttributeValueToTransportTransform(input_.value)
  }!;
}export function jsonAttributeToApplicationTransform(input_?: any): Attribute {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,value: jsonAttributeValueToApplicationTransform(input_.value)
  }!;
}export function jsonAttributeValueToTransportTransform(
  input_?: AttributeValue | null,
): any {
  if(!input_) {
    return input_ as any;
  }return input_
}export function jsonAttributeValueToApplicationTransform(
  input_?: any,
): AttributeValue {
  if(!input_) {
    return input_ as any;
  }return input_
}export function jsonArrayStringToTransportTransform(
  items_?: Array<string> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayStringToApplicationTransform(
  items_?: any,
): Array<string> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayBooleanToTransportTransform(
  items_?: Array<boolean> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayBooleanToApplicationTransform(
  items_?: any,
): Array<boolean> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayInt64ToTransportTransform(
  items_?: Array<bigint> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayInt64ToApplicationTransform(
  items_?: any,
): Array<bigint> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayFloat64ToTransportTransform(
  items_?: Array<number> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayFloat64ToApplicationTransform(
  items_?: any,
): Array<number> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySpanEventToTransportTransform(
  items_?: Array<SpanEvent> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanEventToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySpanEventToApplicationTransform(
  items_?: any,
): Array<SpanEvent> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanEventToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSpanEventToTransportTransform(
  input_?: SpanEvent | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,time_unix_nano: input_.timeUnixNano,attributes: jsonArrayAttributeToTransportTransform(input_.attributes),dropped_attributes_count: input_.droppedAttributesCount
  }!;
}export function jsonSpanEventToApplicationTransform(input_?: any): SpanEvent {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,timeUnixNano: input_.time_unix_nano,attributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count
  }!;
}export function jsonArraySpanLinkToTransportTransform(
  items_?: Array<SpanLink> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanLinkToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySpanLinkToApplicationTransform(
  items_?: any,
): Array<SpanLink> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanLinkToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSpanLinkToTransportTransform(
  input_?: SpanLink | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    trace_id: input_.traceId,span_id: input_.spanId,trace_state: input_.traceState,attributes: jsonArrayAttributeToTransportTransform(input_.attributes),dropped_attributes_count: input_.droppedAttributesCount,flags: input_.flags
  }!;
}export function jsonSpanLinkToApplicationTransform(input_?: any): SpanLink {
  if(!input_) {
    return input_ as any;
  }
    return {
    traceId: input_.trace_id,spanId: input_.span_id,traceState: input_.trace_state,attributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count,flags: input_.flags
  }!;
}export function jsonSpanStatusToTransportTransform(
  input_?: SpanStatus | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    code: input_.code,message: input_.message
  }!;
}export function jsonSpanStatusToApplicationTransform(
  input_?: any,
): SpanStatus {
  if(!input_) {
    return input_ as any;
  }
    return {
    code: input_.code,message: input_.message
  }!;
}export function jsonResourceToTransportTransform(
  input_?: Resource | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    "service.name": input_.serviceName,"service.namespace": input_.serviceNamespace,"service.instance.id": input_.serviceInstanceId,"service.version": input_.serviceVersion,"telemetry.sdk.name": input_.telemetrySdkName,"telemetry.sdk.language": input_.telemetrySdkLanguage,"telemetry.sdk.version": input_.telemetrySdkVersion,"telemetry.auto.version": input_.telemetryAutoVersion,"deployment.environment.name": input_.deploymentEnvironment,"cloud.provider": input_.cloudProvider,"cloud.region": input_.cloudRegion,"cloud.availability_zone": input_.cloudAvailabilityZone,"cloud.account.id": input_.cloudAccountId,"cloud.platform": input_.cloudPlatform,"host.name": input_.hostName,"host.id": input_.hostId,"host.type": input_.hostType,"host.arch": input_.hostArch,"os.type": input_.osType,"os.description": input_.osDescription,"os.version": input_.osVersion,"process.pid": input_.processPid,"process.executable.name": input_.processExecutableName,"process.command_line": input_.processCommandLine,"process.runtime.name": input_.processRuntimeName,"process.runtime.version": input_.processRuntimeVersion,"container.id": input_.containerId,"container.name": input_.containerName,"container.image.name": input_.containerImageName,"container.image.tag": input_.containerImageTag,"k8s.cluster.name": input_.k8sClusterName,"k8s.namespace.name": input_.k8sNamespaceName,"k8s.pod.name": input_.k8sPodName,"k8s.pod.uid": input_.k8sPodUid,"k8s.deployment.name": input_.k8sDeploymentName,attributes: jsonArrayAttributeToTransportTransform(input_.attributes),dropped_attributes_count: input_.droppedAttributesCount
  }!;
}export function jsonResourceToApplicationTransform(input_?: any): Resource {
  if(!input_) {
    return input_ as any;
  }
    return {
    serviceName: input_.service.name,serviceNamespace: input_.service.namespace,serviceInstanceId: input_.service.instance.id,serviceVersion: input_.service.version,telemetrySdkName: input_.telemetry.sdk.name,telemetrySdkLanguage: input_.telemetry.sdk.language,telemetrySdkVersion: input_.telemetry.sdk.version,telemetryAutoVersion: input_.telemetry.auto.version,deploymentEnvironment: input_.deployment.environment.name,cloudProvider: input_.cloud.provider,cloudRegion: input_.cloud.region,cloudAvailabilityZone: input_.cloud.availability_zone,cloudAccountId: input_.cloud.account.id,cloudPlatform: input_.cloud.platform,hostName: input_.host.name,hostId: input_.host.id,hostType: input_.host.type,hostArch: input_.host.arch,osType: input_.os.type,osDescription: input_.os.description,osVersion: input_.os.version,processPid: input_.process.pid,processExecutableName: input_.process.executable.name,processCommandLine: input_.process.command_line,processRuntimeName: input_.process.runtime.name,processRuntimeVersion: input_.process.runtime.version,containerId: input_.container.id,containerName: input_.container.name,containerImageName: input_.container.image.name,containerImageTag: input_.container.image.tag,k8sClusterName: input_.k8s.cluster.name,k8sNamespaceName: input_.k8s.namespace.name,k8sPodName: input_.k8s.pod.name,k8sPodUid: input_.k8s.pod.uid,k8sDeploymentName: input_.k8s.deployment.name,attributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count
  }!;
}export function jsonInstrumentationScopeToTransportTransform(
  input_?: InstrumentationScope | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.scopeName,version: input_.scopeVersion,attributes: jsonArrayAttributeToTransportTransform(input_.scopeAttributes),dropped_attributes_count: input_.droppedAttributesCount
  }!;
}export function jsonInstrumentationScopeToApplicationTransform(
  input_?: any,
): InstrumentationScope {
  if(!input_) {
    return input_ as any;
  }
    return {
    scopeName: input_.name,scopeVersion: input_.version,scopeAttributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count
  }!;
}export function jsonCursorPageToTransportTransform_2(
  input_?: CursorPage_2 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArraySpanRecordToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_2(
  input_?: any,
): CursorPage_2 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArraySpanRecordToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArraySpanRecordToTransportTransform(
  items_?: Array<SpanRecord> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanRecordToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySpanRecordToApplicationTransform(
  items_?: any,
): Array<SpanRecord> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSpanRecordToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSpanRecordToTransportTransform(
  input_?: SpanRecord | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    spanId: input_.spanId,traceId: input_.traceId,parentSpanId: input_.parentSpanId,sessionId: input_.sessionId,name: input_.name,kind: input_.kind,startTimeUnixNano: input_.startTimeUnixNano,endTimeUnixNano: input_.endTimeUnixNano,durationNs: input_.durationNs,statusCode: input_.statusCode,statusMessage: input_.statusMessage,serviceName: input_.serviceName,genAiProviderName: input_.genAiProviderName,genAiRequestModel: input_.genAiRequestModel,genAiResponseModel: input_.genAiResponseModel,genAiInputTokens: input_.genAiInputTokens,genAiOutputTokens: input_.genAiOutputTokens,genAiTemperature: input_.genAiTemperature,genAiStopReason: input_.genAiStopReason,genAiToolName: input_.genAiToolName,genAiToolCallId: input_.genAiToolCallId,genAiCostUsd: input_.genAiCostUsd,attributesJson: input_.attributesJson,resourceJson: input_.resourceJson,baggageJson: input_.baggageJson,schemaUrl: input_.schemaUrl,createdAt: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonSpanRecordToApplicationTransform(
  input_?: any,
): SpanRecord {
  if(!input_) {
    return input_ as any;
  }
    return {
    spanId: input_.spanId,traceId: input_.traceId,parentSpanId: input_.parentSpanId,sessionId: input_.sessionId,name: input_.name,kind: input_.kind,startTimeUnixNano: input_.startTimeUnixNano,endTimeUnixNano: input_.endTimeUnixNano,durationNs: input_.durationNs,statusCode: input_.statusCode,statusMessage: input_.statusMessage,serviceName: input_.serviceName,genAiProviderName: input_.genAiProviderName,genAiRequestModel: input_.genAiRequestModel,genAiResponseModel: input_.genAiResponseModel,genAiInputTokens: input_.genAiInputTokens,genAiOutputTokens: input_.genAiOutputTokens,genAiTemperature: input_.genAiTemperature,genAiStopReason: input_.genAiStopReason,genAiToolName: input_.genAiToolName,genAiToolCallId: input_.genAiToolCallId,genAiCostUsd: input_.genAiCostUsd,attributesJson: input_.attributesJson,resourceJson: input_.resourceJson,baggageJson: input_.baggageJson,schemaUrl: input_.schemaUrl,createdAt: dateDeserializer(input_.createdAt)!
  }!;
}export function jsonTraceQueryToTransportTransform(
  input_?: TraceQuery | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,service_name: input_.serviceName,operation_name: input_.operationName,min_duration_ms: input_.minDurationMs,max_duration_ms: input_.maxDurationMs,status: input_.status,start_time: dateRfc3339Serializer(input_.startTime),end_time: dateRfc3339Serializer(input_.endTime),tags: jsonRecordStringToTransportTransform(input_.tags),limit: input_.limit,cursor: input_.cursor
  }!;
}export function jsonTraceQueryToApplicationTransform(
  input_?: any,
): TraceQuery {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,serviceName: input_.service_name,operationName: input_.operation_name,minDurationMs: input_.min_duration_ms,maxDurationMs: input_.max_duration_ms,status: input_.status,startTime: dateDeserializer(input_.start_time)!,endTime: dateDeserializer(input_.end_time)!,tags: jsonRecordStringToApplicationTransform(input_.tags),limit: input_.limit,cursor: input_.cursor
  }!;
}export function jsonRecordStringToTransportTransform(
  items_?: Record<string, any> | null,
): any {
  if(!items_) {
    return items_ as any;
  }

  const _transformedRecord: any = {};

  for (const [key, value] of Object.entries(items_ ?? {})) {
    const transformedItem = value as any;
    _transformedRecord[key] = transformedItem;
  }

  return _transformedRecord;
}export function jsonRecordStringToApplicationTransform(
  items_?: any,
): Record<string, any> {
  if(!items_) {
    return items_ as any;
  }

  const _transformedRecord: any = {};

  for (const [key, value] of Object.entries(items_ ?? {})) {
    const transformedItem = value as any;
    _transformedRecord[key] = transformedItem;
  }

  return _transformedRecord;
}export function jsonCursorPageToTransportTransform_3(
  input_?: CursorPage_3 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayLogRecordToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_3(
  input_?: any,
): CursorPage_3 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayLogRecordToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayLogRecordToTransportTransform(
  items_?: Array<LogRecord> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogRecordToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogRecordToApplicationTransform(
  items_?: any,
): Array<LogRecord> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogRecordToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogRecordToTransportTransform(
  input_?: LogRecord | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    time_unix_nano: input_.timeUnixNano,observed_time_unix_nano: input_.observedTimeUnixNano,severity_number: input_.severityNumber,severity_text: input_.severityText,body: jsonLogBodyToTransportTransform(input_.body),attributes: jsonArrayAttributeToTransportTransform(input_.attributes),dropped_attributes_count: input_.droppedAttributesCount,flags: input_.flags,trace_id: input_.traceId,span_id: input_.spanId,resource: jsonResourceToTransportTransform(input_.resource),instrumentation_scope: jsonInstrumentationScopeToTransportTransform(input_.instrumentationScope)
  }!;
}export function jsonLogRecordToApplicationTransform(input_?: any): LogRecord {
  if(!input_) {
    return input_ as any;
  }
    return {
    timeUnixNano: input_.time_unix_nano,observedTimeUnixNano: input_.observed_time_unix_nano,severityNumber: input_.severity_number,severityText: input_.severity_text,body: jsonLogBodyToApplicationTransform(input_.body),attributes: jsonArrayAttributeToApplicationTransform(input_.attributes),droppedAttributesCount: input_.dropped_attributes_count,flags: input_.flags,traceId: input_.trace_id,spanId: input_.span_id,resource: jsonResourceToApplicationTransform(input_.resource),instrumentationScope: jsonInstrumentationScopeToApplicationTransform(input_.instrumentation_scope)
  }!;
}export function jsonLogBodyToTransportDiscriminator(input_?: LogBody): any {
  if(!input_) {
    return input_ as any;
  }const discriminatorValue = input_.kind;if( discriminatorValue === "stringBody") {
    return jsonLogBodyStringToTransportTransform(input_ as any)!
  }

  if( discriminatorValue === "kvListBody") {
    return jsonLogBodyKvListToTransportTransform(input_ as any)!
  }

  if( discriminatorValue === "arrayBody") {
    return jsonLogBodyArrayToTransportTransform(input_ as any)!
  }

  if( discriminatorValue === "bytesBody") {
    return jsonLogBodyBytesToTransportTransform(input_ as any)!
  }console.warn(`Received unknown kind: ` + discriminatorValue); return input_ as any
}export function jsonLogBodyToTransportTransform(input_?: LogBody | null): any {
  if(!input_) {
    return input_ as any;
  }return jsonLogBodyToTransportDiscriminator(input_)
}export function jsonLogBodyToApplicationDiscriminator(input_?: any): LogBody {
  if(!input_) {
    return input_ as any;
  }const discriminatorValue = input_.kind;if( discriminatorValue === "stringBody") {
    return jsonLogBodyStringToApplicationTransform(input_ as any)!
  }

  if( discriminatorValue === "kvListBody") {
    return jsonLogBodyKvListToApplicationTransform(input_ as any)!
  }

  if( discriminatorValue === "arrayBody") {
    return jsonLogBodyArrayToApplicationTransform(input_ as any)!
  }

  if( discriminatorValue === "bytesBody") {
    return jsonLogBodyBytesToApplicationTransform(input_ as any)!
  }console.warn(`Received unknown kind: ` + discriminatorValue); return input_ as any
}export function jsonLogBodyToApplicationTransform(input_?: any): LogBody {
  if(!input_) {
    return input_ as any;
  }return jsonLogBodyToApplicationDiscriminator(input_)
}export function jsonLogBodyStringToTransportTransform(
  input_?: LogBodyString | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    string_value: input_.stringValue
  }!;
}export function jsonLogBodyStringToApplicationTransform(
  input_?: any,
): LogBodyString {
  if(!input_) {
    return input_ as any;
  }
    return {
    stringValue: input_.string_value
  }!;
}export function jsonLogBodyKvListToTransportTransform(
  input_?: LogBodyKvList | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    kv_list_value: jsonArrayAttributeToTransportTransform(input_.kvListValue)
  }!;
}export function jsonLogBodyKvListToApplicationTransform(
  input_?: any,
): LogBodyKvList {
  if(!input_) {
    return input_ as any;
  }
    return {
    kvListValue: jsonArrayAttributeToApplicationTransform(input_.kv_list_value)
  }!;
}export function jsonLogBodyArrayToTransportTransform(
  input_?: LogBodyArray | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    array_value: jsonArrayAttributeValueToTransportTransform(input_.arrayValue)
  }!;
}export function jsonLogBodyArrayToApplicationTransform(
  input_?: any,
): LogBodyArray {
  if(!input_) {
    return input_ as any;
  }
    return {
    arrayValue: jsonArrayAttributeValueToApplicationTransform(input_.array_value)
  }!;
}export function jsonArrayAttributeValueToTransportTransform(
  items_?: Array<AttributeValue> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeValueToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayAttributeValueToApplicationTransform(
  items_?: any,
): Array<AttributeValue> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeValueToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogBodyBytesToTransportTransform(
  input_?: LogBodyBytes | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    bytes_value: encodeUint8Array(input_.bytesValue, "base64")!
  }!;
}export function jsonLogBodyBytesToApplicationTransform(
  input_?: any,
): LogBodyBytes {
  if(!input_) {
    return input_ as any;
  }
    return {
    bytesValue: decodeBase64(input_.bytes_value)!
  }!;
}export function jsonLogQueryToTransportTransform(
  input_?: LogQuery | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,severity_min: input_.severityMin,service_name: input_.serviceName,trace_id: input_.traceId,span_id: input_.spanId,time_start: dateRfc3339Serializer(input_.timeStart),time_end: dateRfc3339Serializer(input_.timeEnd),attribute_filters: jsonArrayAttributeFilterToTransportTransform(input_.attributeFilters),limit: input_.limit,order_by: input_.orderBy
  }!;
}export function jsonLogQueryToApplicationTransform(input_?: any): LogQuery {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,severityMin: input_.severity_min,serviceName: input_.service_name,traceId: input_.trace_id,spanId: input_.span_id,timeStart: dateDeserializer(input_.time_start)!,timeEnd: dateDeserializer(input_.time_end)!,attributeFilters: jsonArrayAttributeFilterToApplicationTransform(input_.attribute_filters),limit: input_.limit,orderBy: input_.order_by
  }!;
}export function jsonArrayAttributeFilterToTransportTransform(
  items_?: Array<AttributeFilter> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeFilterToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayAttributeFilterToApplicationTransform(
  items_?: any,
): Array<AttributeFilter> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAttributeFilterToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonAttributeFilterToTransportTransform(
  input_?: AttributeFilter | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,operator: input_.operator,value: input_.value
  }!;
}export function jsonAttributeFilterToApplicationTransform(
  input_?: any,
): AttributeFilter {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,operator: input_.operator,value: input_.value
  }!;
}export function jsonLogAggregationRequestToTransportTransform(
  input_?: LogAggregationRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: jsonLogQueryToTransportTransform(input_.query),aggregation: jsonLogAggregationToTransportTransform(input_.aggregation)
  }!;
}export function jsonLogAggregationRequestToApplicationTransform(
  input_?: any,
): LogAggregationRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: jsonLogQueryToApplicationTransform(input_.query),aggregation: jsonLogAggregationToApplicationTransform(input_.aggregation)
  }!;
}export function jsonLogAggregationToTransportTransform(
  input_?: LogAggregation | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    group_by: jsonArrayStringToTransportTransform(input_.groupBy),function: input_.function_,field: input_.field,time_bucket: input_.timeBucket,top_n: input_.topN
  }!;
}export function jsonLogAggregationToApplicationTransform(
  input_?: any,
): LogAggregation {
  if(!input_) {
    return input_ as any;
  }
    return {
    groupBy: jsonArrayStringToApplicationTransform(input_.group_by),function_: input_.function,field: input_.field,timeBucket: input_.time_bucket,topN: input_.top_n
  }!;
}export function jsonLogAggregationResponseToTransportTransform(
  input_?: LogAggregationResponse | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    results: jsonArrayLogAggregationBucketToTransportTransform(input_.results),total_count: input_.totalCount
  }!;
}export function jsonLogAggregationResponseToApplicationTransform(
  input_?: any,
): LogAggregationResponse {
  if(!input_) {
    return input_ as any;
  }
    return {
    results: jsonArrayLogAggregationBucketToApplicationTransform(input_.results),totalCount: input_.total_count
  }!;
}export function jsonArrayLogAggregationBucketToTransportTransform(
  items_?: Array<LogAggregationBucket> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogAggregationBucketToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogAggregationBucketToApplicationTransform(
  items_?: any,
): Array<LogAggregationBucket> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogAggregationBucketToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogAggregationBucketToTransportTransform(
  input_?: LogAggregationBucket | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,value: input_.value,count: input_.count,timestamp: dateRfc3339Serializer(input_.timestamp)
  }!;
}export function jsonLogAggregationBucketToApplicationTransform(
  input_?: any,
): LogAggregationBucket {
  if(!input_) {
    return input_ as any;
  }
    return {
    key: input_.key,value: input_.value,count: input_.count,timestamp: dateDeserializer(input_.timestamp)!
  }!;
}export function jsonArrayLogPatternToTransportTransform(
  items_?: Array<LogPattern> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogPatternToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogPatternToApplicationTransform(
  items_?: any,
): Array<LogPattern> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogPatternToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogPatternToTransportTransform(
  input_?: LogPattern | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    pattern_id: input_.patternId,template: input_.template,sample: input_.sample,count: input_.count,first_seen: dateRfc3339Serializer(input_.firstSeen),last_seen: dateRfc3339Serializer(input_.lastSeen),trend: input_.trend,severity_distribution: jsonArrayLogSeverityStatsToTransportTransform(input_.severityDistribution)
  }!;
}export function jsonLogPatternToApplicationTransform(
  input_?: any,
): LogPattern {
  if(!input_) {
    return input_ as any;
  }
    return {
    patternId: input_.pattern_id,template: input_.template,sample: input_.sample,count: input_.count,firstSeen: dateDeserializer(input_.first_seen)!,lastSeen: dateDeserializer(input_.last_seen)!,trend: input_.trend,severityDistribution: jsonArrayLogSeverityStatsToApplicationTransform(input_.severity_distribution)
  }!;
}export function jsonArrayLogSeverityStatsToTransportTransform(
  items_?: Array<LogSeverityStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogSeverityStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogSeverityStatsToApplicationTransform(
  items_?: any,
): Array<LogSeverityStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogSeverityStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogSeverityStatsToTransportTransform(
  input_?: LogSeverityStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    severity: input_.severity,severity_text: input_.severityText,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonLogSeverityStatsToApplicationTransform(
  input_?: any,
): LogSeverityStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    severity: input_.severity,severityText: input_.severity_text,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonLogStatsToTransportTransform(
  input_?: LogStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    total_count: input_.totalCount,by_severity: jsonArrayLogCountBySeverityToTransportTransform(input_.bySeverity),by_service: jsonArrayLogCountByDimensionToTransportTransform(input_.byService),logs_per_second: input_.logsPerSecond,error_rate: input_.errorRate
  }!;
}export function jsonLogStatsToApplicationTransform(input_?: any): LogStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    totalCount: input_.total_count,bySeverity: jsonArrayLogCountBySeverityToApplicationTransform(input_.by_severity),byService: jsonArrayLogCountByDimensionToApplicationTransform(input_.by_service),logsPerSecond: input_.logs_per_second,errorRate: input_.error_rate
  }!;
}export function jsonArrayLogCountBySeverityToTransportTransform(
  items_?: Array<LogCountBySeverity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogCountBySeverityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogCountBySeverityToApplicationTransform(
  items_?: any,
): Array<LogCountBySeverity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogCountBySeverityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogCountBySeverityToTransportTransform(
  input_?: LogCountBySeverity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    severity: input_.severity,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonLogCountBySeverityToApplicationTransform(
  input_?: any,
): LogCountBySeverity {
  if(!input_) {
    return input_ as any;
  }
    return {
    severity: input_.severity,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonArrayLogCountByDimensionToTransportTransform(
  items_?: Array<LogCountByDimension> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogCountByDimensionToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayLogCountByDimensionToApplicationTransform(
  items_?: any,
): Array<LogCountByDimension> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonLogCountByDimensionToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonLogCountByDimensionToTransportTransform(
  input_?: LogCountByDimension | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    dimension: input_.dimension,count: input_.count,error_count: input_.errorCount
  }!;
}export function jsonLogCountByDimensionToApplicationTransform(
  input_?: any,
): LogCountByDimension {
  if(!input_) {
    return input_ as any;
  }
    return {
    dimension: input_.dimension,count: input_.count,errorCount: input_.error_count
  }!;
}export function jsonCursorPageToTransportTransform_4(
  input_?: CursorPage_4 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayMetricMetadataToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_4(
  input_?: any,
): CursorPage_4 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayMetricMetadataToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayMetricMetadataToTransportTransform(
  items_?: Array<MetricMetadata> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricMetadataToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayMetricMetadataToApplicationTransform(
  items_?: any,
): Array<MetricMetadata> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricMetadataToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonMetricMetadataToTransportTransform(
  input_?: MetricMetadata | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,description: input_.description,unit: input_.unit,type: input_.type,label_keys: jsonArrayStringToTransportTransform(input_.labelKeys),services: jsonArrayStringToTransportTransform(input_.services)
  }!;
}export function jsonMetricMetadataToApplicationTransform(
  input_?: any,
): MetricMetadata {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,description: input_.description,unit: input_.unit,type: input_.type,labelKeys: jsonArrayStringToApplicationTransform(input_.label_keys),services: jsonArrayStringToApplicationTransform(input_.services)
  }!;
}export function jsonMetricQueryRequestToTransportTransform(
  input_?: MetricQueryRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    metric_name: input_.metricName,filters: jsonRecordStringToTransportTransform(input_.filters),start_time: dateRfc3339Serializer(input_.startTime),end_time: dateRfc3339Serializer(input_.endTime),step: input_.step,aggregation: input_.aggregation,group_by: jsonArrayStringToTransportTransform(input_.groupBy)
  }!;
}export function jsonMetricQueryRequestToApplicationTransform(
  input_?: any,
): MetricQueryRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    metricName: input_.metric_name,filters: jsonRecordStringToApplicationTransform(input_.filters),startTime: dateDeserializer(input_.start_time)!,endTime: dateDeserializer(input_.end_time)!,step: input_.step,aggregation: input_.aggregation,groupBy: jsonArrayStringToApplicationTransform(input_.group_by)
  }!;
}export function jsonMetricQueryResponseToTransportTransform(
  input_?: MetricQueryResponse | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    metric_name: input_.metricName,series: jsonArrayMetricTimeSeriesToTransportTransform(input_.series)
  }!;
}export function jsonMetricQueryResponseToApplicationTransform(
  input_?: any,
): MetricQueryResponse {
  if(!input_) {
    return input_ as any;
  }
    return {
    metricName: input_.metric_name,series: jsonArrayMetricTimeSeriesToApplicationTransform(input_.series)
  }!;
}export function jsonArrayMetricTimeSeriesToTransportTransform(
  items_?: Array<MetricTimeSeries> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricTimeSeriesToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayMetricTimeSeriesToApplicationTransform(
  items_?: any,
): Array<MetricTimeSeries> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricTimeSeriesToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonMetricTimeSeriesToTransportTransform(
  input_?: MetricTimeSeries | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    labels: jsonRecordStringToTransportTransform(input_.labels),points: jsonArrayMetricDataPointToTransportTransform(input_.points)
  }!;
}export function jsonMetricTimeSeriesToApplicationTransform(
  input_?: any,
): MetricTimeSeries {
  if(!input_) {
    return input_ as any;
  }
    return {
    labels: jsonRecordStringToApplicationTransform(input_.labels),points: jsonArrayMetricDataPointToApplicationTransform(input_.points)
  }!;
}export function jsonArrayMetricDataPointToTransportTransform(
  items_?: Array<MetricDataPoint> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricDataPointToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayMetricDataPointToApplicationTransform(
  items_?: any,
): Array<MetricDataPoint> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonMetricDataPointToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonMetricDataPointToTransportTransform(
  input_?: MetricDataPoint | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    timestamp: dateRfc3339Serializer(input_.timestamp),value: input_.value
  }!;
}export function jsonMetricDataPointToApplicationTransform(
  input_?: any,
): MetricDataPoint {
  if(!input_) {
    return input_ as any;
  }
    return {
    timestamp: dateDeserializer(input_.timestamp)!,value: input_.value
  }!;
}export function jsonArrayProfileRecordToTransportTransform(
  items_?: Array<ProfileRecord> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProfileRecordToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayProfileRecordToApplicationTransform(
  items_?: any,
): Array<ProfileRecord> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProfileRecordToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonProfileRecordToTransportTransform(
  input_?: ProfileRecord | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    profileId: input_.profileId,traceId: input_.traceId,spanId: input_.spanId,sessionId: input_.sessionId,timeUnixNano: input_.timeUnixNano,durationNano: input_.durationNano,sampleCount: input_.sampleCount,sampleType: input_.sampleType,sampleUnit: input_.sampleUnit,originalPayloadFormat: input_.originalPayloadFormat,serviceName: input_.serviceName,profileFrameType: input_.profileFrameType,attributesJson: input_.attributesJson,resourceJson: input_.resourceJson,profileDataJson: input_.profileDataJson,schemaUrl: input_.schemaUrl,createdAt: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonProfileRecordToApplicationTransform(
  input_?: any,
): ProfileRecord {
  if(!input_) {
    return input_ as any;
  }
    return {
    profileId: input_.profileId,traceId: input_.traceId,spanId: input_.spanId,sessionId: input_.sessionId,timeUnixNano: input_.timeUnixNano,durationNano: input_.durationNano,sampleCount: input_.sampleCount,sampleType: input_.sampleType,sampleUnit: input_.sampleUnit,originalPayloadFormat: input_.originalPayloadFormat,serviceName: input_.serviceName,profileFrameType: input_.profileFrameType,attributesJson: input_.attributesJson,resourceJson: input_.resourceJson,profileDataJson: input_.profileDataJson,schemaUrl: input_.schemaUrl,createdAt: dateDeserializer(input_.createdAt)!
  }!;
}export function jsonCursorPageToTransportTransform_5(
  input_?: CursorPage_5 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArraySessionEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_5(
  input_?: any,
): CursorPage_5 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArraySessionEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArraySessionEntityToTransportTransform(
  items_?: Array<SessionEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySessionEntityToApplicationTransform(
  items_?: any,
): Array<SessionEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSessionEntityToTransportTransform(
  input_?: SessionEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    "session.id": input_.sessionId,"user.id": input_.userId,start_time: dateRfc3339Serializer(input_.startTime),end_time: dateRfc3339Serializer(input_.endTime),duration_ms: input_.durationMs,trace_count: input_.traceCount,span_count: input_.spanCount,error_count: input_.errorCount,services: jsonArrayStringToTransportTransform(input_.services),state: input_.state,client: jsonSessionClientInfoToTransportTransform(input_.client),geo: jsonSessionGeoInfoToTransportTransform(input_.geo),genai_usage: jsonSessionGenAiUsageToTransportTransform(input_.genaiUsage)
  }!;
}export function jsonSessionEntityToApplicationTransform(
  input_?: any,
): SessionEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    sessionId: input_.session.id,userId: input_.user.id,startTime: dateDeserializer(input_.start_time)!,endTime: dateDeserializer(input_.end_time)!,durationMs: input_.duration_ms,traceCount: input_.trace_count,spanCount: input_.span_count,errorCount: input_.error_count,services: jsonArrayStringToApplicationTransform(input_.services),state: input_.state,client: jsonSessionClientInfoToApplicationTransform(input_.client),geo: jsonSessionGeoInfoToApplicationTransform(input_.geo),genaiUsage: jsonSessionGenAiUsageToApplicationTransform(input_.genai_usage)
  }!;
}export function jsonSessionClientInfoToTransportTransform(
  input_?: SessionClientInfo | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    ip: input_.ip,user_agent: input_.userAgent,device_type: input_.deviceType,os: input_.os,browser: input_.browser,browser_version: input_.browserVersion
  }!;
}export function jsonSessionClientInfoToApplicationTransform(
  input_?: any,
): SessionClientInfo {
  if(!input_) {
    return input_ as any;
  }
    return {
    ip: input_.ip,userAgent: input_.user_agent,deviceType: input_.device_type,os: input_.os,browser: input_.browser,browserVersion: input_.browser_version
  }!;
}export function jsonSessionGeoInfoToTransportTransform(
  input_?: SessionGeoInfo | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    country_code: input_.countryCode,country_name: input_.countryName,region: input_.region,city: input_.city,postal_code: input_.postalCode,timezone: input_.timezone
  }!;
}export function jsonSessionGeoInfoToApplicationTransform(
  input_?: any,
): SessionGeoInfo {
  if(!input_) {
    return input_ as any;
  }
    return {
    countryCode: input_.country_code,countryName: input_.country_name,region: input_.region,city: input_.city,postalCode: input_.postal_code,timezone: input_.timezone
  }!;
}export function jsonSessionGenAiUsageToTransportTransform(
  input_?: SessionGenAiUsage | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    request_count: input_.requestCount,total_input_tokens: input_.totalInputTokens,total_output_tokens: input_.totalOutputTokens,models_used: jsonArrayStringToTransportTransform(input_.modelsUsed),providers_used: jsonArrayStringToTransportTransform(input_.providersUsed),estimated_cost_usd: input_.estimatedCostUsd
  }!;
}export function jsonSessionGenAiUsageToApplicationTransform(
  input_?: any,
): SessionGenAiUsage {
  if(!input_) {
    return input_ as any;
  }
    return {
    requestCount: input_.request_count,totalInputTokens: input_.total_input_tokens,totalOutputTokens: input_.total_output_tokens,modelsUsed: jsonArrayStringToApplicationTransform(input_.models_used),providersUsed: jsonArrayStringToApplicationTransform(input_.providers_used),estimatedCostUsd: input_.estimated_cost_usd
  }!;
}export function jsonSessionStatsToTransportTransform(
  input_?: SessionStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    active_sessions: input_.activeSessions,total_sessions: input_.totalSessions,unique_users: input_.uniqueUsers,avg_duration_ms: input_.avgDurationMs,sessions_with_errors: input_.sessionsWithErrors,sessions_with_genai: input_.sessionsWithGenAi,bounce_rate: input_.bounceRate,by_device_type: jsonArraySessionDeviceStatsToTransportTransform(input_.byDeviceType),by_country: jsonArraySessionCountryStatsToTransportTransform(input_.byCountry)
  }!;
}export function jsonSessionStatsToApplicationTransform(
  input_?: any,
): SessionStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    activeSessions: input_.active_sessions,totalSessions: input_.total_sessions,uniqueUsers: input_.unique_users,avgDurationMs: input_.avg_duration_ms,sessionsWithErrors: input_.sessions_with_errors,sessionsWithGenAi: input_.sessions_with_genai,bounceRate: input_.bounce_rate,byDeviceType: jsonArraySessionDeviceStatsToApplicationTransform(input_.by_device_type),byCountry: jsonArraySessionCountryStatsToApplicationTransform(input_.by_country)
  }!;
}export function jsonArraySessionDeviceStatsToTransportTransform(
  items_?: Array<SessionDeviceStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionDeviceStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySessionDeviceStatsToApplicationTransform(
  items_?: any,
): Array<SessionDeviceStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionDeviceStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSessionDeviceStatsToTransportTransform(
  input_?: SessionDeviceStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    device_type: input_.deviceType,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonSessionDeviceStatsToApplicationTransform(
  input_?: any,
): SessionDeviceStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    deviceType: input_.device_type,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonArraySessionCountryStatsToTransportTransform(
  items_?: Array<SessionCountryStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionCountryStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySessionCountryStatsToApplicationTransform(
  items_?: any,
): Array<SessionCountryStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSessionCountryStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSessionCountryStatsToTransportTransform(
  input_?: SessionCountryStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    country_code: input_.countryCode,country_name: input_.countryName,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonSessionCountryStatsToApplicationTransform(
  input_?: any,
): SessionCountryStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    countryCode: input_.country_code,countryName: input_.country_name,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonCursorPageToTransportTransform_6(
  input_?: CursorPage_6 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_6(
  input_?: any,
): CursorPage_6 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayErrorEntityToTransportTransform(
  items_?: Array<ErrorEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorEntityToApplicationTransform(
  items_?: any,
): Array<ErrorEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorEntityToTransportTransform(
  input_?: ErrorEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    error_id: input_.errorId,"error.type": input_.errorType,message: input_.message,category: input_.category,fingerprint: input_.fingerprint,first_seen: dateRfc3339Serializer(input_.firstSeen),last_seen: dateRfc3339Serializer(input_.lastSeen),occurrence_count: input_.occurrenceCount,affected_users: input_.affectedUsers,affected_services: jsonArrayStringToTransportTransform(input_.affectedServices),status: input_.status,assigned_to: input_.assignedTo,issue_url: input_.issueUrl,sample_traces: jsonArrayTraceIdToTransportTransform(input_.sampleTraces)
  }!;
}export function jsonErrorEntityToApplicationTransform(
  input_?: any,
): ErrorEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    errorId: input_.error_id,errorType: input_.error.type,message: input_.message,category: input_.category,fingerprint: input_.fingerprint,firstSeen: dateDeserializer(input_.first_seen)!,lastSeen: dateDeserializer(input_.last_seen)!,occurrenceCount: input_.occurrence_count,affectedUsers: input_.affected_users,affectedServices: jsonArrayStringToApplicationTransform(input_.affected_services),status: input_.status,assignedTo: input_.assigned_to,issueUrl: input_.issue_url,sampleTraces: jsonArrayTraceIdToApplicationTransform(input_.sample_traces)
  }!;
}export function jsonArrayTraceIdToTransportTransform(
  items_?: Array<string> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayTraceIdToApplicationTransform(
  items_?: any,
): Array<string> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorEntityMergePatchUpdateToTransportTransform(
  input_?: ErrorEntityMergePatchUpdate | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,assigned_to: input_.assignedTo,issue_url: input_.issueUrl
  }!;
}export function jsonErrorEntityMergePatchUpdateToApplicationTransform(
  input_?: any,
): ErrorEntityMergePatchUpdate {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,assignedTo: input_.assigned_to,issueUrl: input_.issue_url
  }!;
}export function jsonErrorStatsToTransportTransform(
  input_?: ErrorStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    total_count: input_.totalCount,unique_types: input_.uniqueTypes,error_rate: input_.errorRate,by_category: jsonArrayErrorCategoryStatsToTransportTransform(input_.byCategory),by_service: jsonArrayErrorServiceStatsToTransportTransform(input_.byService),top_errors: jsonArrayErrorTypeStatsToTransportTransform(input_.topErrors),trend: input_.trend
  }!;
}export function jsonErrorStatsToApplicationTransform(
  input_?: any,
): ErrorStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    totalCount: input_.total_count,uniqueTypes: input_.unique_types,errorRate: input_.error_rate,byCategory: jsonArrayErrorCategoryStatsToApplicationTransform(input_.by_category),byService: jsonArrayErrorServiceStatsToApplicationTransform(input_.by_service),topErrors: jsonArrayErrorTypeStatsToApplicationTransform(input_.top_errors),trend: input_.trend
  }!;
}export function jsonArrayErrorCategoryStatsToTransportTransform(
  items_?: Array<ErrorCategoryStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorCategoryStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorCategoryStatsToApplicationTransform(
  items_?: any,
): Array<ErrorCategoryStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorCategoryStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorCategoryStatsToTransportTransform(
  input_?: ErrorCategoryStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    category: input_.category,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonErrorCategoryStatsToApplicationTransform(
  input_?: any,
): ErrorCategoryStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    category: input_.category,count: input_.count,percentage: input_.percentage
  }!;
}export function jsonArrayErrorServiceStatsToTransportTransform(
  items_?: Array<ErrorServiceStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorServiceStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorServiceStatsToApplicationTransform(
  items_?: any,
): Array<ErrorServiceStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorServiceStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorServiceStatsToTransportTransform(
  input_?: ErrorServiceStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    service_name: input_.serviceName,count: input_.count,error_rate: input_.errorRate,top_error_type: input_.topErrorType
  }!;
}export function jsonErrorServiceStatsToApplicationTransform(
  input_?: any,
): ErrorServiceStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    serviceName: input_.service_name,count: input_.count,errorRate: input_.error_rate,topErrorType: input_.top_error_type
  }!;
}export function jsonArrayErrorTypeStatsToTransportTransform(
  items_?: Array<ErrorTypeStats> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorTypeStatsToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorTypeStatsToApplicationTransform(
  items_?: any,
): Array<ErrorTypeStats> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorTypeStatsToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorTypeStatsToTransportTransform(
  input_?: ErrorTypeStats | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    error_type: input_.errorType,count: input_.count,percentage: input_.percentage,affected_users: input_.affectedUsers,status: input_.status
  }!;
}export function jsonErrorTypeStatsToApplicationTransform(
  input_?: any,
): ErrorTypeStats {
  if(!input_) {
    return input_ as any;
  }
    return {
    errorType: input_.error_type,count: input_.count,percentage: input_.percentage,affectedUsers: input_.affected_users,status: input_.status
  }!;
}export function jsonErrorCorrelationToTransportTransform(
  input_?: ErrorCorrelation | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    error_id: input_.errorId,correlated_errors: jsonArrayCorrelatedErrorToTransportTransform(input_.correlatedErrors),root_cause: input_.rootCause,common_attributes: jsonArrayAttributeToTransportTransform(input_.commonAttributes)
  }!;
}export function jsonErrorCorrelationToApplicationTransform(
  input_?: any,
): ErrorCorrelation {
  if(!input_) {
    return input_ as any;
  }
    return {
    errorId: input_.error_id,correlatedErrors: jsonArrayCorrelatedErrorToApplicationTransform(input_.correlated_errors),rootCause: input_.root_cause,commonAttributes: jsonArrayAttributeToApplicationTransform(input_.common_attributes)
  }!;
}export function jsonArrayCorrelatedErrorToTransportTransform(
  items_?: Array<CorrelatedError> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonCorrelatedErrorToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayCorrelatedErrorToApplicationTransform(
  items_?: any,
): Array<CorrelatedError> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonCorrelatedErrorToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonCorrelatedErrorToTransportTransform(
  input_?: CorrelatedError | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    error_id: input_.errorId,error_type: input_.errorType,correlation_strength: input_.correlationStrength,temporal_relationship: input_.temporalRelationship
  }!;
}export function jsonCorrelatedErrorToApplicationTransform(
  input_?: any,
): CorrelatedError {
  if(!input_) {
    return input_ as any;
  }
    return {
    errorId: input_.error_id,errorType: input_.error_type,correlationStrength: input_.correlation_strength,temporalRelationship: input_.temporal_relationship
  }!;
}export function jsonCursorPageToTransportTransform_7(
  input_?: CursorPage_7 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayDeploymentEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_7(
  input_?: any,
): CursorPage_7 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayDeploymentEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayDeploymentEntityToTransportTransform(
  items_?: Array<DeploymentEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonDeploymentEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayDeploymentEntityToApplicationTransform(
  items_?: any,
): Array<DeploymentEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonDeploymentEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonDeploymentEntityToTransportTransform(
  input_?: DeploymentEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    "deployment.id": input_.deploymentId,"service.name": input_.serviceName,"service.version": input_.serviceVersion,environment: input_.environment,status: input_.status,strategy: input_.strategy,start_time: dateRfc3339Serializer(input_.startTime),end_time: dateRfc3339Serializer(input_.endTime),duration_s: input_.durationS,deployed_by: input_.deployedBy,git_commit: input_.gitCommit,git_branch: input_.gitBranch,previous_version: input_.previousVersion,rollback_target: input_.rollbackTarget,replica_count: input_.replicaCount,healthy_replicas: input_.healthyReplicas,error_message: input_.errorMessage
  }!;
}export function jsonDeploymentEntityToApplicationTransform(
  input_?: any,
): DeploymentEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    deploymentId: input_.deployment.id,serviceName: input_.service.name,serviceVersion: input_.service.version,environment: input_.environment,status: input_.status,strategy: input_.strategy,startTime: dateDeserializer(input_.start_time)!,endTime: dateDeserializer(input_.end_time)!,durationS: input_.duration_s,deployedBy: input_.deployed_by,gitCommit: input_.git_commit,gitBranch: input_.git_branch,previousVersion: input_.previous_version,rollbackTarget: input_.rollback_target,replicaCount: input_.replica_count,healthyReplicas: input_.healthy_replicas,errorMessage: input_.error_message
  }!;
}export function jsonCreateDeploymentEntityToTransportTransform(
  input_?: CreateDeploymentEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    "service.name": input_.serviceName,"service.version": input_.serviceVersion,environment: input_.environment,strategy: input_.strategy,deployed_by: input_.deployedBy,git_commit: input_.gitCommit,git_branch: input_.gitBranch
  }!;
}export function jsonCreateDeploymentEntityToApplicationTransform(
  input_?: any,
): CreateDeploymentEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    serviceName: input_.service.name,serviceVersion: input_.service.version,environment: input_.environment,strategy: input_.strategy,deployedBy: input_.deployed_by,gitCommit: input_.git_commit,gitBranch: input_.git_branch
  }!;
}export function jsonDeploymentEntityMergePatchUpdateToTransportTransform(
  input_?: DeploymentEntityMergePatchUpdate | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,healthy_replicas: input_.healthyReplicas,error_message: input_.errorMessage
  }!;
}export function jsonDeploymentEntityMergePatchUpdateToApplicationTransform(
  input_?: any,
): DeploymentEntityMergePatchUpdate {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,healthyReplicas: input_.healthy_replicas,errorMessage: input_.error_message
  }!;
}export function jsonDoraMetricsToTransportTransform(
  input_?: DoraMetrics | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    deployment_frequency: input_.deploymentFrequency,lead_time_hours: input_.leadTimeHours,change_failure_rate: input_.changeFailureRate,mttr_hours: input_.mttrHours,performance_level: input_.performanceLevel
  }!;
}export function jsonDoraMetricsToApplicationTransform(
  input_?: any,
): DoraMetrics {
  if(!input_) {
    return input_ as any;
  }
    return {
    deploymentFrequency: input_.deployment_frequency,leadTimeHours: input_.lead_time_hours,changeFailureRate: input_.change_failure_rate,mttrHours: input_.mttr_hours,performanceLevel: input_.performance_level
  }!;
}export function jsonCursorPageToTransportTransform_8(
  input_?: CursorPage_8 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayServiceInfoToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_8(
  input_?: any,
): CursorPage_8 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayServiceInfoToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayServiceInfoToTransportTransform(
  items_?: Array<ServiceInfo> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonServiceInfoToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayServiceInfoToApplicationTransform(
  items_?: any,
): Array<ServiceInfo> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonServiceInfoToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonServiceInfoToTransportTransform(
  input_?: ServiceInfo | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,namespace_name: input_.namespaceName,version: input_.version,instance_count: input_.instanceCount,last_seen: dateRfc3339Serializer(input_.lastSeen)
  }!;
}export function jsonServiceInfoToApplicationTransform(
  input_?: any,
): ServiceInfo {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,namespaceName: input_.namespace_name,version: input_.version,instanceCount: input_.instance_count,lastSeen: dateDeserializer(input_.last_seen)!
  }!;
}export function jsonServiceDetailsToTransportTransform(
  input_?: ServiceDetails | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,namespace_name: input_.namespaceName,version: input_.version,instance_count: input_.instanceCount,last_seen: dateRfc3339Serializer(input_.lastSeen),resource_attributes: jsonArrayAttributeToTransportTransform(input_.resourceAttributes),instrumentation_libraries: jsonArrayInstrumentationScopeToTransportTransform(input_.instrumentationLibraries),request_rate: input_.requestRate,error_rate: input_.errorRate,avg_latency_ms: input_.avgLatencyMs,p99_latency_ms: input_.p99LatencyMs
  }!;
}export function jsonServiceDetailsToApplicationTransform(
  input_?: any,
): ServiceDetails {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,namespaceName: input_.namespace_name,version: input_.version,instanceCount: input_.instance_count,lastSeen: dateDeserializer(input_.last_seen)!,resourceAttributes: jsonArrayAttributeToApplicationTransform(input_.resource_attributes),instrumentationLibraries: jsonArrayInstrumentationScopeToApplicationTransform(input_.instrumentation_libraries),requestRate: input_.request_rate,errorRate: input_.error_rate,avgLatencyMs: input_.avg_latency_ms,p99LatencyMs: input_.p99_latency_ms
  }!;
}export function jsonArrayInstrumentationScopeToTransportTransform(
  items_?: Array<InstrumentationScope> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonInstrumentationScopeToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayInstrumentationScopeToApplicationTransform(
  items_?: any,
): Array<InstrumentationScope> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonInstrumentationScopeToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayServiceDependencyToTransportTransform(
  items_?: Array<ServiceDependency> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonServiceDependencyToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayServiceDependencyToApplicationTransform(
  items_?: any,
): Array<ServiceDependency> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonServiceDependencyToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonServiceDependencyToTransportTransform(
  input_?: ServiceDependency | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    source_service: input_.sourceService,target_service: input_.targetService,request_count: input_.requestCount,error_rate: input_.errorRate,avg_latency_ms: input_.avgLatencyMs
  }!;
}export function jsonServiceDependencyToApplicationTransform(
  input_?: any,
): ServiceDependency {
  if(!input_) {
    return input_ as any;
  }
    return {
    sourceService: input_.source_service,targetService: input_.target_service,requestCount: input_.request_count,errorRate: input_.error_rate,avgLatencyMs: input_.avg_latency_ms
  }!;
}export function jsonCursorPageToTransportTransform_9(
  input_?: CursorPage_9 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayOperationInfoToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_9(
  input_?: any,
): CursorPage_9 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayOperationInfoToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayOperationInfoToTransportTransform(
  items_?: Array<OperationInfo> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonOperationInfoToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayOperationInfoToApplicationTransform(
  items_?: any,
): Array<OperationInfo> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonOperationInfoToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonOperationInfoToTransportTransform(
  input_?: OperationInfo | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,span_kind: input_.spanKind,request_count: input_.requestCount,error_count: input_.errorCount,avg_duration_ms: input_.avgDurationMs,p99_duration_ms: input_.p99DurationMs
  }!;
}export function jsonOperationInfoToApplicationTransform(
  input_?: any,
): OperationInfo {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,spanKind: input_.span_kind,requestCount: input_.request_count,errorCount: input_.error_count,avgDurationMs: input_.avg_duration_ms,p99DurationMs: input_.p99_duration_ms
  }!;
}export function jsonWorkspaceEnvelopeEntityToTransportTransform(
  input_?: WorkspaceEnvelopeEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,project_id: input_.projectId,environment_id: input_.environmentId,node_id: input_.nodeId,name: input_.name,root_path: input_.rootPath,heartbeat_at: dateRfc3339Serializer(input_.heartbeatAt),heartbeat_interval_seconds: input_.heartbeatIntervalSeconds,status: input_.status,config_json: input_.configJson,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt)
  }!;
}export function jsonWorkspaceEnvelopeEntityToApplicationTransform(
  input_?: any,
): WorkspaceEnvelopeEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,projectId: input_.project_id,environmentId: input_.environment_id,nodeId: input_.node_id,name: input_.name,rootPath: input_.root_path,heartbeatAt: dateDeserializer(input_.heartbeat_at)!,heartbeatIntervalSeconds: input_.heartbeat_interval_seconds,status: input_.status,configJson: input_.config_json,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!
  }!;
}export function jsonCursorPageToTransportTransform_10(
  input_?: CursorPage_10 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayProjectEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_10(
  input_?: any,
): CursorPage_10 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayProjectEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayProjectEntityToTransportTransform(
  items_?: Array<ProjectEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProjectEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayProjectEntityToApplicationTransform(
  items_?: any,
): Array<ProjectEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProjectEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonProjectEntityToTransportTransform(
  input_?: ProjectEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,name: input_.name,slug: input_.slug,description: input_.description,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt),archived_at: dateRfc3339Serializer(input_.archivedAt)
  }!;
}export function jsonProjectEntityToApplicationTransform(
  input_?: any,
): ProjectEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,name: input_.name,slug: input_.slug,description: input_.description,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!,archivedAt: dateDeserializer(input_.archived_at)!
  }!;
}export function jsonCreateProjectEntityToTransportTransform(
  input_?: CreateProjectEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,slug: input_.slug,description: input_.description
  }!;
}export function jsonCreateProjectEntityToApplicationTransform(
  input_?: any,
): CreateProjectEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,slug: input_.slug,description: input_.description
  }!;
}export function jsonArrayProjectEnvironmentEntityToTransportTransform(
  items_?: Array<ProjectEnvironmentEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProjectEnvironmentEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayProjectEnvironmentEntityToApplicationTransform(
  items_?: any,
): Array<ProjectEnvironmentEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonProjectEnvironmentEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonProjectEnvironmentEntityToTransportTransform(
  input_?: ProjectEnvironmentEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,project_id: input_.projectId,name: input_.name,display_name: input_.displayName,color: input_.color,sort_order: input_.sortOrder,created_at: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonProjectEnvironmentEntityToApplicationTransform(
  input_?: any,
): ProjectEnvironmentEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,projectId: input_.project_id,name: input_.name,displayName: input_.display_name,color: input_.color,sortOrder: input_.sort_order,createdAt: dateDeserializer(input_.created_at)!
  }!;
}export function jsonHandshakeStartRequestToTransportTransform(
  input_?: HandshakeStartRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    code_challenge: input_.codeChallenge,client_id: input_.clientId
  }!;
}export function jsonHandshakeStartRequestToApplicationTransform(
  input_?: any,
): HandshakeStartRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    codeChallenge: input_.code_challenge,clientId: input_.client_id
  }!;
}export function jsonHandshakeSessionEntityToTransportTransform(
  input_?: HandshakeSessionEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspace_id: input_.workspaceId,challenge: input_.challenge,challenge_method: input_.challengeMethod,browser_fingerprint: input_.browserFingerprint,origin_url: input_.originUrl,state: input_.state,verified_at: dateRfc3339Serializer(input_.verifiedAt),expires_at: dateRfc3339Serializer(input_.expiresAt),created_at: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonHandshakeSessionEntityToApplicationTransform(
  input_?: any,
): HandshakeSessionEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspaceId: input_.workspace_id,challenge: input_.challenge,challengeMethod: input_.challenge_method,browserFingerprint: input_.browser_fingerprint,originUrl: input_.origin_url,state: input_.state,verifiedAt: dateDeserializer(input_.verified_at)!,expiresAt: dateDeserializer(input_.expires_at)!,createdAt: dateDeserializer(input_.created_at)!
  }!;
}export function jsonHandshakeVerifyRequestToTransportTransform(
  input_?: HandshakeVerifyRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    code_verifier: input_.codeVerifier,code: input_.code
  }!;
}export function jsonHandshakeVerifyRequestToApplicationTransform(
  input_?: any,
): HandshakeVerifyRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    codeVerifier: input_.code_verifier,code: input_.code
  }!;
}export function jsonHandshakeVerifyResponseToTransportTransform(
  input_?: HandshakeVerifyResponse | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    access_token: input_.accessToken,expires_at: dateRfc3339Serializer(input_.expiresAt),workspace_id: input_.workspaceId
  }!;
}export function jsonHandshakeVerifyResponseToApplicationTransform(
  input_?: any,
): HandshakeVerifyResponse {
  if(!input_) {
    return input_ as any;
  }
    return {
    accessToken: input_.access_token,expiresAt: dateDeserializer(input_.expires_at)!,workspaceId: input_.workspace_id
  }!;
}export function jsonCursorPageToTransportTransform_11(
  input_?: CursorPage_11 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayGenerationProfileEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_11(
  input_?: any,
): CursorPage_11 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayGenerationProfileEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayGenerationProfileEntityToTransportTransform(
  items_?: Array<GenerationProfileEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonGenerationProfileEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayGenerationProfileEntityToApplicationTransform(
  items_?: any,
): Array<GenerationProfileEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonGenerationProfileEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonGenerationProfileEntityToTransportTransform(
  input_?: GenerationProfileEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,project_id: input_.projectId,name: input_.name,description: input_.description,target_framework: input_.targetFramework,target_language: input_.targetLanguage,semconv_version: input_.semconvVersion,features_json: input_.featuresJson,template_overrides_json: input_.templateOverridesJson,is_default: input_.isDefault,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt)
  }!;
}export function jsonGenerationProfileEntityToApplicationTransform(
  input_?: any,
): GenerationProfileEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,projectId: input_.project_id,name: input_.name,description: input_.description,targetFramework: input_.target_framework,targetLanguage: input_.target_language,semconvVersion: input_.semconv_version,featuresJson: input_.features_json,templateOverridesJson: input_.template_overrides_json,isDefault: input_.is_default,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!
  }!;
}export function jsonCreateGenerationProfileEntityToTransportTransform(
  input_?: CreateGenerationProfileEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,description: input_.description,target_framework: input_.targetFramework,target_language: input_.targetLanguage,semconv_version: input_.semconvVersion,features_json: input_.featuresJson,template_overrides_json: input_.templateOverridesJson
  }!;
}export function jsonCreateGenerationProfileEntityToApplicationTransform(
  input_?: any,
): CreateGenerationProfileEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    name: input_.name,description: input_.description,targetFramework: input_.target_framework,targetLanguage: input_.target_language,semconvVersion: input_.semconv_version,featuresJson: input_.features_json,templateOverridesJson: input_.template_overrides_json
  }!;
}export function jsonArrayGenerationSelectionEntityToTransportTransform(
  items_?: Array<GenerationSelectionEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonGenerationSelectionEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayGenerationSelectionEntityToApplicationTransform(
  items_?: any,
): Array<GenerationSelectionEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonGenerationSelectionEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonGenerationSelectionEntityToTransportTransform(
  input_?: GenerationSelectionEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspace_id: input_.workspaceId,profile_id: input_.profileId,selection_type: input_.selectionType,selection_key: input_.selectionKey,enabled: input_.enabled,config_json: input_.configJson,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt)
  }!;
}export function jsonGenerationSelectionEntityToApplicationTransform(
  input_?: any,
): GenerationSelectionEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspaceId: input_.workspace_id,profileId: input_.profile_id,selectionType: input_.selection_type,selectionKey: input_.selection_key,enabled: input_.enabled,configJson: input_.config_json,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!
  }!;
}export function jsonGenerationSelectionSaveRequestToTransportTransform(
  input_?: GenerationSelectionSaveRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    workspace_id: input_.workspaceId,profile_id: input_.profileId,selected_keys_json: input_.selectedKeysJson
  }!;
}export function jsonGenerationSelectionSaveRequestToApplicationTransform(
  input_?: any,
): GenerationSelectionSaveRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    workspaceId: input_.workspace_id,profileId: input_.profile_id,selectedKeysJson: input_.selected_keys_json
  }!;
}export function jsonCreateGenerationJobEntityToTransportTransform(
  input_?: CreateGenerationJobEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    workspace_id: input_.workspaceId,profile_id: input_.profileId,job_type: input_.jobType
  }!;
}export function jsonCreateGenerationJobEntityToApplicationTransform(
  input_?: any,
): CreateGenerationJobEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    workspaceId: input_.workspace_id,profileId: input_.profile_id,jobType: input_.job_type
  }!;
}export function jsonGenerationJobEntityToTransportTransform(
  input_?: GenerationJobEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspace_id: input_.workspaceId,profile_id: input_.profileId,job_type: input_.jobType,status: input_.status,priority: input_.priority,input_hash: input_.inputHash,output_path: input_.outputPath,output_hash: input_.outputHash,error_message: input_.errorMessage,queued_at: dateRfc3339Serializer(input_.queuedAt),started_at: dateRfc3339Serializer(input_.startedAt),completed_at: dateRfc3339Serializer(input_.completedAt),duration_ms: input_.durationMs
  }!;
}export function jsonGenerationJobEntityToApplicationTransform(
  input_?: any,
): GenerationJobEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workspaceId: input_.workspace_id,profileId: input_.profile_id,jobType: input_.job_type,status: input_.status,priority: input_.priority,inputHash: input_.input_hash,outputPath: input_.output_path,outputHash: input_.output_hash,errorMessage: input_.error_message,queuedAt: dateDeserializer(input_.queued_at)!,startedAt: dateDeserializer(input_.started_at)!,completedAt: dateDeserializer(input_.completed_at)!,durationMs: input_.duration_ms
  }!;
}export function jsonCursorPageToTransportTransform_12(
  input_?: CursorPage_12 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorIssueEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_12(
  input_?: any,
): CursorPage_12 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorIssueEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayErrorIssueEntityToTransportTransform(
  items_?: Array<ErrorIssueEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorIssueEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorIssueEntityToApplicationTransform(
  items_?: any,
): Array<ErrorIssueEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorIssueEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorIssueEntityToTransportTransform(
  input_?: ErrorIssueEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,project_id: input_.projectId,fingerprint: input_.fingerprint,title: input_.title,culprit: input_.culprit,error_type: input_.errorType,category: input_.category,level: input_.level,platform: input_.platform,first_seen_at: dateRfc3339Serializer(input_.firstSeenAt),last_seen_at: dateRfc3339Serializer(input_.lastSeenAt),occurrence_count: input_.occurrenceCount,affected_users_count: input_.affectedUsersCount,status: input_.status,substatus: input_.substatus,priority: input_.priority,assigned_to: input_.assignedTo,resolved_at: dateRfc3339Serializer(input_.resolvedAt),resolved_by: input_.resolvedBy,regression_count: input_.regressionCount,last_release: input_.lastRelease,tags_json: input_.tagsJson,metadata_json: input_.metadataJson,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt)
  }!;
}export function jsonErrorIssueEntityToApplicationTransform(
  input_?: any,
): ErrorIssueEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,projectId: input_.project_id,fingerprint: input_.fingerprint,title: input_.title,culprit: input_.culprit,errorType: input_.error_type,category: input_.category,level: input_.level,platform: input_.platform,firstSeenAt: dateDeserializer(input_.first_seen_at)!,lastSeenAt: dateDeserializer(input_.last_seen_at)!,occurrenceCount: input_.occurrence_count,affectedUsersCount: input_.affected_users_count,status: input_.status,substatus: input_.substatus,priority: input_.priority,assignedTo: input_.assigned_to,resolvedAt: dateDeserializer(input_.resolved_at)!,resolvedBy: input_.resolved_by,regressionCount: input_.regression_count,lastRelease: input_.last_release,tagsJson: input_.tags_json,metadataJson: input_.metadata_json,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!
  }!;
}export function jsonErrorIssueEntityMergePatchUpdateToTransportTransform(
  input_?: ErrorIssueEntityMergePatchUpdate | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,priority: input_.priority,assigned_to: input_.assignedTo
  }!;
}export function jsonErrorIssueEntityMergePatchUpdateToApplicationTransform(
  input_?: any,
): ErrorIssueEntityMergePatchUpdate {
  if(!input_) {
    return input_ as any;
  }
    return {
    status: input_.status,priority: input_.priority,assignedTo: input_.assigned_to
  }!;
}export function jsonCursorPageToTransportTransform_13(
  input_?: CursorPage_13 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorIssueEventEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_13(
  input_?: any,
): CursorPage_13 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayErrorIssueEventEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayErrorIssueEventEntityToTransportTransform(
  items_?: Array<ErrorIssueEventEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorIssueEventEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorIssueEventEntityToApplicationTransform(
  items_?: any,
): Array<ErrorIssueEventEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorIssueEventEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorIssueEventEntityToTransportTransform(
  input_?: ErrorIssueEventEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,issue_id: input_.issueId,trace_id: input_.traceId,span_id: input_.spanId,message: input_.message,stack_trace: input_.stackTrace,stack_frames_json: input_.stackFramesJson,environment: input_.environment,release_version: input_.releaseVersion,user_id: input_.userId,user_ip: input_.userIp,request_url: input_.requestUrl,request_method: input_.requestMethod,browser: input_.browser,os: input_.os,device: input_.device,runtime: input_.runtime,runtime_version: input_.runtimeVersion,context_json: input_.contextJson,tags_json: input_.tagsJson,timestamp: dateRfc3339Serializer(input_.timestamp)
  }!;
}export function jsonErrorIssueEventEntityToApplicationTransform(
  input_?: any,
): ErrorIssueEventEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,issueId: input_.issue_id,traceId: input_.trace_id,spanId: input_.span_id,message: input_.message,stackTrace: input_.stack_trace,stackFramesJson: input_.stack_frames_json,environment: input_.environment,releaseVersion: input_.release_version,userId: input_.user_id,userIp: input_.user_ip,requestUrl: input_.request_url,requestMethod: input_.request_method,browser: input_.browser,os: input_.os,device: input_.device,runtime: input_.runtime,runtimeVersion: input_.runtime_version,contextJson: input_.context_json,tagsJson: input_.tags_json,timestamp: dateDeserializer(input_.timestamp)!
  }!;
}export function jsonArrayErrorBreadcrumbEntityToTransportTransform(
  items_?: Array<ErrorBreadcrumbEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorBreadcrumbEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayErrorBreadcrumbEntityToApplicationTransform(
  items_?: any,
): Array<ErrorBreadcrumbEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonErrorBreadcrumbEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonErrorBreadcrumbEntityToTransportTransform(
  input_?: ErrorBreadcrumbEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,event_id: input_.eventId,breadcrumb_type: input_.breadcrumbType,category: input_.category,message: input_.message,level: input_.level,data_json: input_.dataJson,timestamp: dateRfc3339Serializer(input_.timestamp)
  }!;
}export function jsonErrorBreadcrumbEntityToApplicationTransform(
  input_?: any,
): ErrorBreadcrumbEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,eventId: input_.event_id,breadcrumbType: input_.breadcrumb_type,category: input_.category,message: input_.message,level: input_.level,dataJson: input_.data_json,timestamp: dateDeserializer(input_.timestamp)!
  }!;
}export function jsonCursorPageToTransportTransform_14(
  input_?: CursorPage_14 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayWorkflowRunEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_14(
  input_?: any,
): CursorPage_14 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayWorkflowRunEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayWorkflowRunEntityToTransportTransform(
  items_?: Array<WorkflowRunEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowRunEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayWorkflowRunEntityToApplicationTransform(
  items_?: any,
): Array<WorkflowRunEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowRunEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonWorkflowRunEntityToTransportTransform(
  input_?: WorkflowRunEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workflow_id: input_.workflowId,workflow_version: input_.workflowVersion,project_id: input_.projectId,trigger_type: input_.triggerType,trigger_source: input_.triggerSource,input_json: input_.inputJson,output_json: input_.outputJson,status: input_.status,error_message: input_.errorMessage,parent_run_id: input_.parentRunId,correlation_id: input_.correlationId,started_at: dateRfc3339Serializer(input_.startedAt),completed_at: dateRfc3339Serializer(input_.completedAt),duration_ms: input_.durationMs,created_at: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonWorkflowRunEntityToApplicationTransform(
  input_?: any,
): WorkflowRunEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,workflowId: input_.workflow_id,workflowVersion: input_.workflow_version,projectId: input_.project_id,triggerType: input_.trigger_type,triggerSource: input_.trigger_source,inputJson: input_.input_json,outputJson: input_.output_json,status: input_.status,errorMessage: input_.error_message,parentRunId: input_.parent_run_id,correlationId: input_.correlation_id,startedAt: dateDeserializer(input_.started_at)!,completedAt: dateDeserializer(input_.completed_at)!,durationMs: input_.duration_ms,createdAt: dateDeserializer(input_.created_at)!
  }!;
}export function jsonCursorPageToTransportTransform_15(
  input_?: CursorPage_15 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayWorkflowNodeEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_15(
  input_?: any,
): CursorPage_15 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayWorkflowNodeEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayWorkflowNodeEntityToTransportTransform(
  items_?: Array<WorkflowNodeEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowNodeEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayWorkflowNodeEntityToApplicationTransform(
  items_?: any,
): Array<WorkflowNodeEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowNodeEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonWorkflowNodeEntityToTransportTransform(
  input_?: WorkflowNodeEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,run_id: input_.runId,node_id: input_.nodeId,node_type: input_.nodeType,node_name: input_.nodeName,attempt: input_.attempt,input_json: input_.inputJson,output_json: input_.outputJson,status: input_.status,error_message: input_.errorMessage,retry_count: input_.retryCount,max_retries: input_.maxRetries,timeout_ms: input_.timeoutMs,started_at: dateRfc3339Serializer(input_.startedAt),completed_at: dateRfc3339Serializer(input_.completedAt),duration_ms: input_.durationMs,created_at: dateRfc3339Serializer(input_.createdAt)
  }!;
}export function jsonWorkflowNodeEntityToApplicationTransform(
  input_?: any,
): WorkflowNodeEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,runId: input_.run_id,nodeId: input_.node_id,nodeType: input_.node_type,nodeName: input_.node_name,attempt: input_.attempt,inputJson: input_.input_json,outputJson: input_.output_json,status: input_.status,errorMessage: input_.error_message,retryCount: input_.retry_count,maxRetries: input_.max_retries,timeoutMs: input_.timeout_ms,startedAt: dateDeserializer(input_.started_at)!,completedAt: dateDeserializer(input_.completed_at)!,durationMs: input_.duration_ms,createdAt: dateDeserializer(input_.created_at)!
  }!;
}export function jsonArrayWorkflowEventEntityToTransportTransform(
  items_?: Array<WorkflowEventEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowEventEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayWorkflowEventEntityToApplicationTransform(
  items_?: any,
): Array<WorkflowEventEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonWorkflowEventEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonWorkflowEventEntityToTransportTransform(
  input_?: WorkflowEventEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,run_id: input_.runId,node_id: input_.nodeId,event_type: input_.eventType,event_name: input_.eventName,payload_json: input_.payloadJson,sequence_number: input_.sequenceNumber,source: input_.source,correlation_id: input_.correlationId,timestamp: dateRfc3339Serializer(input_.timestamp)
  }!;
}export function jsonWorkflowEventEntityToApplicationTransform(
  input_?: any,
): WorkflowEventEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,runId: input_.run_id,nodeId: input_.node_id,eventType: input_.event_type,eventName: input_.event_name,payloadJson: input_.payload_json,sequenceNumber: input_.sequence_number,source: input_.source,correlationId: input_.correlation_id,timestamp: dateDeserializer(input_.timestamp)!
  }!;
}export function jsonSearchRequestToTransportTransform(
  input_?: SearchRequest | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,entity_types: jsonArraySearchEntityTypeToTransportTransform(input_.entityTypes),project_id: input_.projectId,limit: input_.limit,cursor: input_.cursor
  }!;
}export function jsonSearchRequestToApplicationTransform(
  input_?: any,
): SearchRequest {
  if(!input_) {
    return input_ as any;
  }
    return {
    query: input_.query,entityTypes: jsonArraySearchEntityTypeToApplicationTransform(input_.entity_types),projectId: input_.project_id,limit: input_.limit,cursor: input_.cursor
  }!;
}export function jsonArraySearchEntityTypeToTransportTransform(
  items_?: Array<SearchEntityType> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySearchEntityTypeToApplicationTransform(
  items_?: any,
): Array<SearchEntityType> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = item as any;
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSearchResponseToTransportTransform(
  input_?: SearchResponse | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    results: jsonArraySearchResultToTransportTransform(input_.results),total_count: input_.totalCount,duration_ms: input_.durationMs,next_cursor: input_.nextCursor,suggestions: jsonArrayStringToTransportTransform(input_.suggestions)
  }!;
}export function jsonSearchResponseToApplicationTransform(
  input_?: any,
): SearchResponse {
  if(!input_) {
    return input_ as any;
  }
    return {
    results: jsonArraySearchResultToApplicationTransform(input_.results),totalCount: input_.total_count,durationMs: input_.duration_ms,nextCursor: input_.next_cursor,suggestions: jsonArrayStringToApplicationTransform(input_.suggestions)
  }!;
}export function jsonArraySearchResultToTransportTransform(
  items_?: Array<SearchResult> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSearchResultToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArraySearchResultToApplicationTransform(
  items_?: any,
): Array<SearchResult> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonSearchResultToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonSearchResultToTransportTransform(
  input_?: SearchResult | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    document_id: input_.documentId,entity_type: input_.entityType,entity_id: input_.entityId,title: input_.title,snippet: input_.snippet,score: input_.score,url: input_.url
  }!;
}export function jsonSearchResultToApplicationTransform(
  input_?: any,
): SearchResult {
  if(!input_) {
    return input_ as any;
  }
    return {
    documentId: input_.document_id,entityType: input_.entity_type,entityId: input_.entity_id,title: input_.title,snippet: input_.snippet,score: input_.score,url: input_.url
  }!;
}export function jsonCursorPageToTransportTransform_16(
  input_?: CursorPage_16 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayAlertRuleEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_16(
  input_?: any,
): CursorPage_16 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayAlertRuleEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayAlertRuleEntityToTransportTransform(
  items_?: Array<AlertRuleEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAlertRuleEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayAlertRuleEntityToApplicationTransform(
  items_?: any,
): Array<AlertRuleEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAlertRuleEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonAlertRuleEntityToTransportTransform(
  input_?: AlertRuleEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,project_id: input_.projectId,name: input_.name,description: input_.description,rule_type: input_.ruleType,condition_json: input_.conditionJson,threshold_json: input_.thresholdJson,target_type: input_.targetType,target_filter_json: input_.targetFilterJson,severity: input_.severity,cooldown_seconds: input_.cooldownSeconds,notification_channels_json: input_.notificationChannelsJson,enabled: input_.enabled,last_triggered_at: dateRfc3339Serializer(input_.lastTriggeredAt),trigger_count: input_.triggerCount,created_at: dateRfc3339Serializer(input_.createdAt),updated_at: dateRfc3339Serializer(input_.updatedAt)
  }!;
}export function jsonAlertRuleEntityToApplicationTransform(
  input_?: any,
): AlertRuleEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,projectId: input_.project_id,name: input_.name,description: input_.description,ruleType: input_.rule_type,conditionJson: input_.condition_json,thresholdJson: input_.threshold_json,targetType: input_.target_type,targetFilterJson: input_.target_filter_json,severity: input_.severity,cooldownSeconds: input_.cooldown_seconds,notificationChannelsJson: input_.notification_channels_json,enabled: input_.enabled,lastTriggeredAt: dateDeserializer(input_.last_triggered_at)!,triggerCount: input_.trigger_count,createdAt: dateDeserializer(input_.created_at)!,updatedAt: dateDeserializer(input_.updated_at)!
  }!;
}export function jsonCursorPageToTransportTransform_17(
  input_?: CursorPage_17 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayAlertFiringEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_17(
  input_?: any,
): CursorPage_17 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayAlertFiringEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayAlertFiringEntityToTransportTransform(
  items_?: Array<AlertFiringEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAlertFiringEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayAlertFiringEntityToApplicationTransform(
  items_?: any,
): Array<AlertFiringEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonAlertFiringEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonAlertFiringEntityToTransportTransform(
  input_?: AlertFiringEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,rule_id: input_.ruleId,fingerprint: input_.fingerprint,severity: input_.severity,title: input_.title,message: input_.message,trigger_value: input_.triggerValue,threshold_value: input_.thresholdValue,context_json: input_.contextJson,status: input_.status,acknowledged_at: dateRfc3339Serializer(input_.acknowledgedAt),acknowledged_by: input_.acknowledgedBy,resolved_at: dateRfc3339Serializer(input_.resolvedAt),fired_at: dateRfc3339Serializer(input_.firedAt),dedup_key: input_.dedupKey
  }!;
}export function jsonAlertFiringEntityToApplicationTransform(
  input_?: any,
): AlertFiringEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,ruleId: input_.rule_id,fingerprint: input_.fingerprint,severity: input_.severity,title: input_.title,message: input_.message,triggerValue: input_.trigger_value,thresholdValue: input_.threshold_value,contextJson: input_.context_json,status: input_.status,acknowledgedAt: dateDeserializer(input_.acknowledged_at)!,acknowledgedBy: input_.acknowledged_by,resolvedAt: dateDeserializer(input_.resolved_at)!,firedAt: dateDeserializer(input_.fired_at)!,dedupKey: input_.dedup_key
  }!;
}export function jsonAlertFiringAcknowledgementToTransportTransform(
  input_?: AlertFiringAcknowledgement | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    acknowledged_by: input_.acknowledgedBy
  }!;
}export function jsonAlertFiringAcknowledgementToApplicationTransform(
  input_?: any,
): AlertFiringAcknowledgement {
  if(!input_) {
    return input_ as any;
  }
    return {
    acknowledgedBy: input_.acknowledged_by
  }!;
}export function jsonCursorPageToTransportTransform_18(
  input_?: CursorPage_18 | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayFixRunEntityToTransportTransform(input_.items),next_cursor: input_.nextCursor,prev_cursor: input_.prevCursor,has_more: input_.hasMore
  }!;
}export function jsonCursorPageToApplicationTransform_18(
  input_?: any,
): CursorPage_18 {
  if(!input_) {
    return input_ as any;
  }
    return {
    items: jsonArrayFixRunEntityToApplicationTransform(input_.items),nextCursor: input_.next_cursor,prevCursor: input_.prev_cursor,hasMore: input_.has_more
  }!;
}export function jsonArrayFixRunEntityToTransportTransform(
  items_?: Array<FixRunEntity> | null,
): any {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonFixRunEntityToTransportTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonArrayFixRunEntityToApplicationTransform(
  items_?: any,
): Array<FixRunEntity> {
  if(!items_) {
    return items_ as any;
  }
  const _transformedArray = [];

  for (const item of items_ ?? []) {
    const transformedItem = jsonFixRunEntityToApplicationTransform(item as any);
    _transformedArray.push(transformedItem);
  }

  return _transformedArray as any;
}export function jsonFixRunEntityToTransportTransform(
  input_?: FixRunEntity | null,
): any {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,issue_id: input_.issueId,alert_firing_id: input_.alertFiringId,trigger_type: input_.triggerType,strategy: input_.strategy,model_name: input_.modelName,model_provider: input_.modelProvider,status: input_.status,error_message: input_.errorMessage,tokens_used: input_.tokensUsed,duration_ms: input_.durationMs,created_at: dateRfc3339Serializer(input_.createdAt),started_at: dateRfc3339Serializer(input_.startedAt),completed_at: dateRfc3339Serializer(input_.completedAt)
  }!;
}export function jsonFixRunEntityToApplicationTransform(
  input_?: any,
): FixRunEntity {
  if(!input_) {
    return input_ as any;
  }
    return {
    id: input_.id,issueId: input_.issue_id,alertFiringId: input_.alert_firing_id,triggerType: input_.trigger_type,strategy: input_.strategy,modelName: input_.model_name,modelProvider: input_.model_provider,status: input_.status,errorMessage: input_.error_message,tokensUsed: input_.tokens_used,durationMs: input_.duration_ms,createdAt: dateDeserializer(input_.created_at)!,startedAt: dateDeserializer(input_.started_at)!,completedAt: dateDeserializer(input_.completed_at)!
  }!;
}
