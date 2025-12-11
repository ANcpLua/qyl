# Shared/Throw

Argument validation helpers injected into projects via MSBuild.

## Usage

In your `.csproj`:
```xml
<PropertyGroup>
  <InjectSharedThrow>true</InjectSharedThrow>
</PropertyGroup>
```

Then use:
```csharp
Throw.IfNull(argument);
Throw.IfNullOrWhiteSpace(name);
Throw.IfNegative(count);
```
