namespace qyl.collector;

public sealed record ErrorResponse(string Error, string? Message = null);
