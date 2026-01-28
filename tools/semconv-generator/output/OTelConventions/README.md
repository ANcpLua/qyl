# OTelConventions

OpenTelemetry Semantic Conventions as C# constants.

## Features

- **Zero dependencies** - BCL only, works everywhere
- **AOT compatible** - Fully trimmable
- **Generated** - From `@opentelemetry/semantic-conventions` NPM package v1.39.0
- **Complete** - All stable and experimental conventions

## Installation

```bash
dotnet add package OTelConventions
```

## Supported Frameworks

- .NET 10.0
- .NET 8.0
- .NET Standard 2.0

## Usage

```csharp
using OTelConventions;

// GenAI attributes
activity.SetTag(GenAiAttributes.System, GenAiSystemValues.Anthropic);
activity.SetTag(GenAiAttributes.RequestModel, "claude-sonnet-4-20250514");
activity.SetTag(GenAiUsageAttributes.InputTokens, 150);
activity.SetTag(GenAiUsageAttributes.OutputTokens, 42);

// HTTP attributes
activity.SetTag(HttpAttributes.RequestMethod, "GET");
activity.SetTag(HttpAttributes.ResponseStatusCode, 200);
activity.SetTag(HttpAttributes.Route, "/api/orders/{id}");

// Database attributes
activity.SetTag(DbAttributes.System, DbSystemValues.Postgresql);
activity.SetTag(DbAttributes.Name, "mydb");
activity.SetTag(DbAttributes.Statement, "SELECT * FROM orders");
activity.SetTag(DbAttributes.OperationName, "SELECT");

// Cloud attributes
activity.SetTag(CloudAttributes.Provider, CloudProviderValues.Azure);
activity.SetTag(CloudAttributes.Region, "westeurope");

// Container attributes
activity.SetTag(ContainerAttributes.Id, "abc123");
activity.SetTag(ContainerAttributes.Name, "my-container");

// Kubernetes attributes
activity.SetTag(K8sAttributes.PodName, "my-pod-xyz");
activity.SetTag(K8sAttributes.NamespaceName, "production");

// Messaging attributes
activity.SetTag(MessagingAttributes.System, MessagingSystemValues.Kafka);
activity.SetTag(MessagingAttributes.DestinationName, "orders-topic");

// Network attributes
activity.SetTag(NetworkAttributes.ProtocolName, "http");
activity.SetTag(NetworkAttributes.ProtocolVersion, "1.1");

// And many more...
```

## Available Attribute Classes

| Category   | Attributes Class      | Values Class                                            |
|------------|-----------------------|---------------------------------------------------------|
| GenAI      | `GenAiAttributes`     | `GenAiSystemValues`, `GenAiOperationNameValues`         |
| HTTP       | `HttpAttributes`      | `HttpRequestMethodValues`                               |
| Database   | `DbAttributes`        | `DbSystemValues`, `DbClientConnectionStateValues`       |
| Cloud      | `CloudAttributes`     | `CloudProviderValues`, `CloudPlatformValues`            |
| Container  | `ContainerAttributes` | -                                                       |
| Kubernetes | `K8sAttributes`       | -                                                       |
| Messaging  | `MessagingAttributes` | `MessagingSystemValues`, `MessagingOperationTypeValues` |
| Network    | `NetworkAttributes`   | `NetworkTransportValues`                                |
| RPC        | `RpcAttributes`       | `RpcSystemValues`                                       |
| Server     | `ServerAttributes`    | -                                                       |
| Service    | `ServiceAttributes`   | -                                                       |
| URL        | `UrlAttributes`       | -                                                       |
| User Agent | `UserAgentAttributes` | -                                                       |
| Error      | `ErrorAttributes`     | `ErrorTypeValues`                                       |
| Exception  | `ExceptionAttributes` | -                                                       |
| Code       | `CodeAttributes`      | -                                                       |
| Thread     | `ThreadAttributes`    | -                                                       |
| Process    | `ProcessAttributes`   | -                                                       |
| OS         | `OsAttributes`        | `OsTypeValues`                                          |
| Host       | `HostAttributes`      | -                                                       |
| Device     | `DeviceAttributes`    | -                                                       |
| Browser    | `BrowserAttributes`   | -                                                       |
| TLS        | `TlsAttributes`       | -                                                       |
| DNS        | `DnsAttributes`       | -                                                       |
| ...        | ...                   | ...                                                     |

## Semantic Convention Version

This package is generated from OpenTelemetry Semantic Conventions **v1.39.0**.

## License

MIT

## Related

- [ANcpLua.NET.Sdk](https://github.com/ANcpLua/ANcpLua.NET.Sdk) - Full SDK with auto-instrumentation
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)