using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Qyl.Sample;

/// <summary>Request body for creating a todo. The DataAnnotations are enforced by the generated validation resolver before the handler runs.</summary>
// Public because the .NET validation generator discovers validatable types through the public
// surface only: made internal, it emits a resolver with no types in it and every request passes.
[SuppressMessage("Design", "CA1515:Consider making public types internal",
    Justification = "The validation generator only discovers public validatable types.")]
public sealed class CreateTodoRequest
{
    /// <summary>What has to be done.</summary>
    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    /// <summary>The day it is due, if any.</summary>
    public DateOnly? DueBy { get; init; }
}
