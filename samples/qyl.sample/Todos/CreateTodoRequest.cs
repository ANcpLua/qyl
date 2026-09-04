using System.ComponentModel.DataAnnotations;

namespace Qyl.Sample;

/// <summary>Request body for creating a todo. The DataAnnotations are enforced by the generated validation resolver before the handler runs.</summary>
public sealed class CreateTodoRequest
{
    /// <summary>What has to be done.</summary>
    [Required]
    [StringLength(120, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    /// <summary>The day it is due, if any.</summary>
    public DateOnly? DueBy { get; init; }
}
