namespace CookieLearning.AuthorizationServer.Diagnostics;

public sealed record DiagnosticEvent(
    DateTimeOffset Timestamp,
    string Category,
    string Action,
    string TraceIdentifier,
    IReadOnlyDictionary<string, string?> Details);
