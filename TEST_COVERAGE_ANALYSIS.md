# qyl Test Coverage Analysis

**Generated:** 2026-02-04
**Test Results:** 251/277 passing (90.6%)
**Failed Tests:** 26

## Summary

The qyl project has good test coverage across core functionality, but there are critical gaps in newer features and some failing tests that need attention.

## Test Statistics

### By Project

| Project | Total Tests | Passing | Failing | Coverage |
|---------|-------------|---------|---------|----------|
| qyl.collector.tests | ~155 | 151 | 4 | 97.4% |
| qyl.Analyzers.Tests | ~122 | 100 | 22 | 82.0% |
| **Total** | **277** | **251** | **26** | **90.6%** |

### Test Distribution

```
tests/
├── qyl.collector.tests/
│   ├── Ingestion/ (5 test files)
│   │   ├── OtlpJsonSpanParserTests.cs ✓
│   │   ├── OtlpConverterProtoTests.cs ✓
│   │   ├── OtlpProtobufParserTests.cs ⚠️ (3 failures)
│   │   ├── SchemaNormalizerTests.cs ✓
│   │   └── SqlOperationParserTests.cs ✓
│   ├── Storage/ (1 test file)
│   │   └── DuckDbStoreTests.cs ✓
│   ├── Query/ (1 test file)
│   │   └── SessionQueryServiceTests.cs ⚠️ (1 failure)
│   ├── Integration/ (3 test files)
│   │   ├── ApiIntegrationTests.cs ✓
│   │   ├── OtlpIngestionTests.cs ✓
│   │   └── SessionEndToEndTests.cs ✓
│   └── Diagnostics/ (1 test file)
│       └── DiagnosticTest.cs ✓
│
└── qyl.Analyzers.Tests/
    └── Analyzers/ (5 test files)
        ├── Qyl011MeterClassTests.cs ✓
        ├── Qyl012MetricMethodTests.cs ✓
        ├── Qyl013TracedTests.cs ⚠️ (22 failures)
        ├── Qyl014DeprecatedGenAiTests.cs ✓
        └── Qyl015HighCardinalityTests.cs ✓
```

## Critical Failures (Must Fix)

### 1. Qyl013TracedTests - 22 Failures

**Status:** 🔴 Critical
**Location:** `/Users/ancplua/qyl/tests/qyl.Analyzers.Tests/Analyzers/Qyl013TracedTests.cs`

**Issue:** Recently added analyzer tests for QYL013 (TracedActivitySourceName) are failing.

**Impact:**
- The QYL013 analyzer may not be working correctly
- Code fix provider may not generate correct fixes
- Users may get incorrect diagnostic warnings

**Root Cause:** Likely related to the recent code fix implementation changes.

**Fix Priority:** HIGH - This is a new feature that should work before shipping.

---

### 2. OtlpProtobufParserTests - 3 Failures

**Status:** 🟡 High Priority
**Location:** `/Users/ancplua/qyl/tests/qyl.collector.tests/Ingestion/OtlpProtobufParserTests.cs`

**Failing Tests:**
1. `Parse_ValidProtobuf_ReturnsPopulatedRequest`
2. `RoundTrip_ProtobufToStorageRow_PreservesData`
3. `Parse_ReadOnlySequence_ParsesCorrectly`

**Error:**
```
System.FormatException: The input is not a valid hex string as it contains a non-hex character.
  at System.Convert.FromHexString(ReadOnlySpan`1 chars)
  at WriteSpan(MemoryStream ms, String traceId, String spanId, ...)
```

**Issue:** Test helper methods are using invalid hex strings for trace/span IDs.

**Impact:**
- OTLP protobuf ingestion tests can't verify correctness
- Risk of protobuf parsing bugs going undetected

**Fix Priority:** MEDIUM - JSON ingestion is well-tested, protobuf is secondary path.

---

### 3. SessionQueryServiceTests - 1 Failure

**Status:** 🟡 Medium Priority
**Location:** `/Users/ancplua/qyl/tests/qyl.collector.tests/Query/SessionQueryServiceTests.cs`

**Error:**
```
System.ArgumentOutOfRangeException: Specified argument was out of the range of valid values.
  (Parameter 'wireType') Actual value was 7.
  at ProtobufReader.SkipField(WireType wireType)
```

**Issue:** Protobuf parser encountering invalid wire type (7 is not a valid protobuf wire type).

**Impact:**
- Session query functionality may have edge case bugs
- Protobuf parsing robustness issue

**Fix Priority:** MEDIUM - Session queries work in integration tests.

## Coverage Gaps (Missing Tests)

### High Priority Gaps

#### 1. Telemetry Cleanup Service
- **File:** `src/qyl.collector/Storage/TelemetryCleanupService.cs`
- **Tests:** ❌ None
- **Impact:** Background cleanup of old telemetry data
- **Needs:**
  - Retention policy enforcement tests
  - Cleanup interval tests
  - Max span/log count tests
  - Edge case: cleanup during active ingestion

#### 2. ClearTelemetry Operations
- **File:** `src/qyl.collector/Storage/DuckDbStore.cs` (lines 580-669)
- **Tests:** ❌ None
- **Impact:** Admin operations to clear telemetry data
- **Needs:**
  - `ClearAllSpansAsync()` test
  - `ClearAllLogsAsync()` test
  - `ClearAllSessionsAsync()` test
  - `ClearAllTelemetryAsync()` test with transaction rollback

#### 3. MCP Server
- **File:** `src/qyl.collector/Mcp/McpServer.cs`
- **Tests:** ❌ None
- **Impact:** Model Context Protocol integration for AI agents
- **Needs:**
  - Tool discovery tests
  - Query execution tests
  - Error handling tests
  - Session replay tests

#### 4. Real-time SSE Streaming
- **Files:**
  - `src/qyl.collector/Realtime/SpanRingBuffer.cs`
  - `src/qyl.collector/Realtime/SseEndpoints.cs`
- **Tests:** ⚠️ Limited
- **Impact:** Live tail functionality
- **Needs:**
  - Ring buffer overflow tests
  - SSE connection lifecycle tests
  - Multi-client streaming tests
  - Backpressure handling tests

#### 5. Health Checks
- **File:** `src/qyl.collector/Health/DuckDbHealthCheck.cs`
- **Tests:** ❌ None
- **Impact:** Kubernetes liveness/readiness probes
- **Needs:**
  - Healthy state tests
  - Unhealthy state tests
  - Degraded state tests

#### 6. CORS Middleware
- **File:** `src/qyl.collector/Ingestion/OtlpCorsMiddleware.cs`
- **Tests:** ❌ None
- **Impact:** Browser-based OTLP ingestion
- **Needs:**
  - Preflight request tests
  - Origin validation tests
  - Wildcard vs specific origin tests

### Medium Priority Gaps

#### 7. GenAI Analytics
- **Files:** `src/qyl.collector/Query/SpanQueryBuilder.cs` (GenAI methods)
- **Tests:** ⚠️ Limited coverage
- **Needs:**
  - Token usage aggregation tests
  - Model breakdown tests
  - Cost calculation tests
  - Time series tests

#### 8. Copilot Integration
- **File:** `src/qyl.copilot/*`
- **Tests:** ❌ None
- **Impact:** GitHub Copilot workspace integration
- **Needs:**
  - Adapter tests
  - Workflow engine tests
  - Auth provider tests

#### 9. Source Generators
- **Files:**
  - `src/qyl.servicedefaults.generator/*`
  - `src/qyl.instrumentation.generators/*`
- **Tests:** ❌ None
- **Impact:** Code generation for interceptors
- **Needs:**
  - Generator snapshot tests
  - Incremental generation tests
  - Error diagnostic tests

### Low Priority Gaps

#### 10. Dashboard Endpoints
- **File:** `src/qyl.collector/Dashboard/EmbeddedDashboardMiddleware.cs`
- **Tests:** ❌ None
- **Impact:** Static file serving
- **Needs:** Basic smoke tests (low priority - simple middleware)

#### 11. Console Bridge
- **File:** `src/qyl.collector/ConsoleBridge/ConsoleBridge.cs`
- **Tests:** ❌ None
- **Impact:** Console output capture
- **Needs:** Basic write/clear tests

## Well-Tested Areas ✅

### 1. OTLP JSON Ingestion
- **Coverage:** Excellent
- **Files:**
  - `OtlpJsonSpanParserTests.cs` (parsing)
  - `OtlpConverterProtoTests.cs` (conversion)
  - `OtlpIngestionTests.cs` (end-to-end)

### 2. DuckDB Storage
- **Coverage:** Very Good
- **File:** `DuckDbStoreTests.cs`
- **Tests:** Insert, query, session aggregation, error tracking

### 3. Session Queries
- **Coverage:** Good
- **File:** `SessionQueryServiceTests.cs`
- **Tests:** Session lifecycle, error rates, aggregations

### 4. Analyzers (QYL011, QYL012, QYL014, QYL015)
- **Coverage:** Excellent
- **Files:** `Qyl011MeterClassTests.cs`, `Qyl012MetricMethodTests.cs`, etc.
- **Tests:** Diagnostics, code fixes, edge cases

### 5. Schema Normalization
- **Coverage:** Excellent
- **File:** `SchemaNormalizerTests.cs`
- **Tests:** OTel 1.39 attribute mapping, deprecated attribute handling

### 6. Integration Tests
- **Coverage:** Very Good
- **Files:**
  - `ApiIntegrationTests.cs`
  - `SessionEndToEndTests.cs`
  - `OtlpIngestionTests.cs`

## Recommended Actions

### Immediate (This Sprint)

1. **Fix Qyl013TracedTests failures**
   - Debug why 22 tests are failing
   - Fix code fix provider if needed
   - Verify analyzer produces correct diagnostics

2. **Fix OtlpProtobufParserTests**
   - Update test hex strings to valid format
   - Ensure trace/span IDs are proper 32/16 hex chars

3. **Add TelemetryCleanupService tests**
   - Critical for production data management
   - Test retention policy enforcement

4. **Add ClearTelemetry operation tests**
   - Verify admin operations work correctly
   - Test transaction rollback scenarios

### Short Term (Next Sprint)

5. **Add MCP Server tests**
   - Tool discovery
   - Query execution
   - Error handling

6. **Expand Real-time SSE tests**
   - Ring buffer overflow
   - Multi-client streaming
   - Connection lifecycle

7. **Add Health Check tests**
   - All health states
   - DuckDB connection validation

8. **Add CORS tests**
   - Browser OTLP scenarios
   - Origin validation

### Medium Term (Future)

9. **Add GenAI analytics tests**
   - Token aggregation
   - Cost calculations

10. **Add Source Generator tests**
    - Snapshot testing
    - Incremental generation

11. **Add Copilot integration tests**
    - Adapter functionality
    - Workflow execution

## Test Quality Observations

### Strengths
- ✅ Good use of integration tests with `QylWebApplicationFactory`
- ✅ Comprehensive analyzer tests with code fix verification
- ✅ DuckDB test helpers are well-designed (`DuckDbTestHelpers`)
- ✅ Good separation of unit vs integration tests

### Areas for Improvement
- ⚠️ Some tests are too verbose (could use more helper methods)
- ⚠️ Test data builders could reduce duplication
- ⚠️ Missing negative/error path tests in some areas
- ⚠️ No performance/load tests for ingestion pipeline

## Code Coverage Estimate

Based on test file analysis:

| Component | Estimated Coverage | Confidence |
|-----------|-------------------|------------|
| OTLP Ingestion (JSON) | 95% | High |
| OTLP Ingestion (Protobuf) | 60% | Medium |
| DuckDB Storage | 85% | High |
| Query Services | 75% | Medium |
| Analyzers (QYL011, 012, 014, 015) | 90% | High |
| Analyzers (QYL013) | 40% | Low (failing) |
| Real-time SSE | 40% | Low |
| MCP Server | 0% | Low |
| Cleanup Service | 0% | Low |
| Health Checks | 0% | Low |
| **Overall Estimate** | **~65%** | **Medium** |

## Next Steps

1. Run this verification subagent to confirm all tests pass
2. Fix the 26 failing tests
3. Add missing tests for high-priority gaps
4. Set up code coverage reporting (coverlet)
5. Add CI/CD coverage gates (minimum 70%)

---

**Generated by:** Test Coverage Analysis
**Command Used:** `dotnet test --solution qyl.slnx # VERIFY`
