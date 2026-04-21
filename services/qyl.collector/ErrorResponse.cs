namespace Qyl.Collector;

public sealed record ErrorResponse(string Error, string? Message = null);
