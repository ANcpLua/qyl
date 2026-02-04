# ANcpLua.Roslyn.Utilities - Guard API Reference

Complete reference for `Guard` class validation methods.

## Design Principles

- All methods use `[CallerArgumentExpression]` for automatic parameter name capture
- Methods return validated values for fluent assignment: `_name = Guard.NotNull(name)`
- Throws specific exception types (ArgumentNullException, ArgumentException, ArgumentOutOfRangeException)
- Numeric methods marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`

---

## Null/Empty Validation

```csharp
// Basic null check - returns T
T NotNull<T>(T? value)

// String validations - return string
string NotNullOrEmpty(string? value)
string NotNullOrWhiteSpace(string? value)

// Fallback variants - return default if null
T NotNullOrElse<T>(T? value, T defaultValue) where T : class
T NotNullOrElse<T>(T? value, Func<T> factory) where T : class  // lazy
T NotNullOrElse<T>(T? value, T defaultValue) where T : struct
T NotNullOrElse<T>(T? value, Func<T> factory) where T : struct // lazy

string NotNullOrEmptyOrElse(string? value, string defaultValue)
string NotNullOrWhiteSpaceOrElse(string? value, string defaultValue)
```

**Patterns to find:**
```csharp
// Before
value ?? throw new ArgumentNullException(nameof(value))
if (value == null) throw new ArgumentNullException(...)

// After
Guard.NotNull(value)
```

---

## String Length Validation

```csharp
string HasLength(string? value, int length)
string HasMinLength(string? value, int minLength)
string HasMaxLength(string? value, int maxLength)
string HasLengthBetween(string? value, int minLength, int maxLength)
```

**Patterns to find:**
```csharp
// Before
if (value.Length != 10) throw new ArgumentException(...)
if (value.Length < 5) throw new ArgumentException(...)

// After
Guard.HasLength(value, 10)
Guard.HasMinLength(value, 5)
```

---

## Numeric Validation - Int32

```csharp
int NotZero(int value)
int NotNegative(int value)      // >= 0
int Positive(int value)         // > 0
int NotGreaterThan(int value, int max)
int NotLessThan(int value, int min)
int LessThan(int value, int max)
int GreaterThan(int value, int min)
```

**Patterns to find:**
```csharp
// Before
if (count <= 0) throw new ArgumentOutOfRangeException(...)
if (value < 0) throw new ArgumentOutOfRangeException(...)
if (value == 0) throw new ArgumentOutOfRangeException(...)

// After
Guard.Positive(count)
Guard.NotNegative(value)
Guard.NotZero(value)
```

---

## Numeric Validation - Int64, Double, Decimal

Same methods as Int32, plus for double:

```csharp
double NotNaN(double value)
double Finite(double value)     // !IsNaN && !IsInfinity
```

**Note:** Double comparisons use `!(value >= 0.0)` pattern to handle NaN correctly.

---

## Range Validation

```csharp
T InRange<T>(T value, T min, T max) where T : IComparable<T>
int ValidIndex(int index, int count)
```

**Patterns to find:**
```csharp
// Before
if (value < min || value > max) throw...
if (index < 0 || index >= count) throw...

// After
Guard.InRange(value, min, max)
Guard.ValidIndex(index, count)
```

---

## Collection Validation

```csharp
IReadOnlyCollection<T> NotNullOrEmpty<T>(IReadOnlyCollection<T>? value)
IReadOnlyList<T> NotNullOrEmpty<T>(IReadOnlyList<T>? value)

void NoDuplicates<T>(IEnumerable<T> value)
void NoDuplicates<T>(IEnumerable<T> value, IEqualityComparer<T> comparer)
IReadOnlyList<T> NoDuplicates<T>(IReadOnlyList<T>? value)
IReadOnlyList<T> NoDuplicates<T>(IReadOnlyList<T>? value, IEqualityComparer<T> comparer)
```

**Patterns to find:**
```csharp
// Before
if (collection == null || collection.Count == 0) throw...
if (items.Distinct().Count() != items.Count()) throw...

// After
Guard.NotNullOrEmpty(collection)
Guard.NoDuplicates(items)
Guard.NoDuplicates(items, StringComparer.OrdinalIgnoreCase)
```

---

## Set Membership Validation

```csharp
T OneOf<T>(T value, T[] allowed)
T OneOf<T>(T value, HashSet<T> allowed)
T NotOneOf<T>(T value, T[] disallowed)
T NotOneOf<T>(T value, HashSet<T> disallowed)
```

**Patterns to find:**
```csharp
// Before
if (!allowedValues.Contains(value)) throw...
if (blacklist.Contains(value)) throw...

// After
Guard.OneOf(value, allowedValues)
Guard.NotOneOf(value, blacklist)
```

---

## Value Type Validation

```csharp
T NotDefault<T>(T value) where T : struct
Guid NotEmpty(Guid value)
```

**Patterns to find:**
```csharp
// Before
if (value.Equals(default(MyStruct))) throw...
if (guid == Guid.Empty) throw...

// After
Guard.NotDefault(value)
Guard.NotEmpty(guid)
```

---

## Enum Validation

```csharp
T DefinedEnum<T>(T value) where T : struct, Enum
```

**Patterns to find:**
```csharp
// Before
if (!Enum.IsDefined(typeof(MyEnum), value)) throw...

// After
Guard.DefinedEnum(value)
```

---

## Type Validation

```csharp
Type NotNullableType(Type? type)
Type AssignableTo<T>(Type? type)
Type AssignableFrom<T>(Type? type)
```

**Patterns to find:**
```csharp
// Before
if (!typeof(IService).IsAssignableFrom(type)) throw...
if (Nullable.GetUnderlyingType(type) != null) throw...

// After
Guard.AssignableFrom<IService>(type)
Guard.NotNullableType(type)
```

---

## Condition Validation

```csharp
void That([DoesNotReturnIf(false)] bool condition, string message)
T Satisfies<T>(T value, Func<T, bool> predicate, string message)
```

**Patterns to find:**
```csharp
// Before
if (!someCondition) throw new ArgumentException("message")

// After
Guard.That(someCondition, "message")
```

---

## Unreachable Code

```csharp
[DoesNotReturn]
void Unreachable(string? message = null)

[DoesNotReturn]
T Unreachable<T>(string? message = null)

void UnreachableIf([DoesNotReturnIf(true)] bool condition, string? message = null)
```

**Patterns to find:**
```csharp
// Before
default:
    throw new InvalidOperationException("Should never reach here");

// After
default:
    Guard.Unreachable();
```

---

## File System Validation

```csharp
string FileExists(string? path)
string DirectoryExists(string? path)
string ValidFileName(string? value)
string? ValidFileNameOrNull(string? value)
string ValidPath(string? value)
string? ValidPathOrNull(string? value)
string ValidExtension(string? value)
string NormalizedExtension(string? value)
```

**Patterns to find:**
```csharp
// Before
if (!File.Exists(path)) throw new FileNotFoundException(...)
if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw...

// After
Guard.FileExists(path)
Guard.ValidFileName(fileName)
```

---

## Member Validation

```csharp
TMember NotNullWithMember<TParam, TMember>(TParam? argument, TMember? member)
TMember MemberNotNull<TParam, TMember>(TParam argument, TMember? member) where TParam : notnull
```

For validating object properties/members aren't null.
