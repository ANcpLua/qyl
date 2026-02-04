---
name: template-clean-arch-specialist
description: |
  Specialized agent for Clean Architecture ASP.NET Core template - MediatR CQRS patterns, domain entities, infrastructure layer, and EF Core migrations
---

## Source Metadata

```yaml
frontmatter:
  model: opus
```


# Template Clean Architecture Specialist

Specialized agent for working with the Clean Architecture ASP.NET Core template.

## When to Use

- Adding new endpoints/use cases
- Modifying domain entities
- Extending infrastructure layer
- MediatR command/query patterns
- Test organization
- EF Core migrations

## Repository Context

**Path**: `/Users/ancplua/Template/Template/src`
**Purpose**: Production-ready Clean Architecture template based on Jason Taylor's pattern
**Framework**: ASP.NET Core 10 with Minimal APIs

## Architecture

```
src/
├── Domain/              # Business entities, rules (clean/independent)
├── Application/         # Use cases via MediatR commands/queries
│   ├── Common/          # Shared behaviors, mappings, interfaces
│   └── TodoItems/       # Feature folder (Commands/, Queries/)
├── Infrastructure/      # EF Core, PostgreSQL, Identity, external services
├── ServiceDefaults/     # Cross-cutting: telemetry, resilience, DI
└── Web/                 # Minimal API endpoints, Swagger
    └── Endpoints/       # EndpointGroupBase implementations
```

## Key Patterns

### Endpoint Groups

```csharp
public class TodoItems : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetTodoItemsWithPagination).RequireAuthorization();
        groupBuilder.MapPost(CreateTodoItem).RequireAuthorization();
    }

    public async Task<Ok<PaginatedList<TodoItemBriefDto>>> GetTodoItemsWithPagination(
        ISender sender, [AsParameters] GetTodoItemsWithPaginationQuery query)
    {
        return TypedResults.Ok(await sender.Send(query));
    }
}
```

### CQRS with MediatR

```csharp
// Command
public record CreateTodoItemCommand(string Title) : IRequest<int>;

public class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, int>
{
    public async Task<int> Handle(CreateTodoItemCommand request, CancellationToken ct)
    {
        // Create and persist
    }
}

// Query
public record GetTodoItemsWithPaginationQuery : IRequest<PaginatedList<TodoItemBriefDto>>;
```

### Layer Dependencies

```
Web → Application → Domain
  ↓
Infrastructure → Application → Domain
```

- Domain has NO external dependencies
- Application defines interfaces, Infrastructure implements them
- Web only references Application (not Infrastructure directly)

## Big Picture

- **Based on**: Jason Taylor's CleanArchitecture template
- **Testing pyramid**: Unit → Integration → Functional → Acceptance
- **Future integration**: ErrorOrX could replace EndpointGroupBase pattern

## Build & Test

```bash
dotnet build -tl
dotnet test --filter "FullyQualifiedName!~AcceptanceTests"

# Run with hot reload
cd src/Web && dotnet watch run
```

## Test Organization

| Layer | Test Type | Location |
|-------|-----------|----------|
| Domain | Unit | `Domain.UnitTests` |
| Application | Unit + Functional | `Application.*Tests` |
| Infrastructure | Integration | `Infrastructure.IntegrationTests` |
| Web | Acceptance | `Web.AcceptanceTests` |

## Key Files

| File | Purpose |
|------|---------|
| `Web/Endpoints/*.cs` | Minimal API endpoint groups |
| `Application/*/Commands/*.cs` | MediatR commands |
| `Application/*/Queries/*.cs` | MediatR queries |
| `Infrastructure/Data/` | EF Core context + configs |

## Dependencies

- MediatR - CQRS
- AutoMapper - DTO mapping
- FluentValidation - Request validation
- EntityFrameworkCore + Npgsql - Persistence
- NSwag - OpenAPI generation

## Ecosystem Context

For cross-repo relationships and source-of-truth locations, invoke:
```
/ancplua-ecosystem
```

This skill provides the full dependency hierarchy, what NOT to duplicate from upstream, and version coordination requirements.
