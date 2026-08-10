using System.Collections.Concurrent;

namespace CookieLearning.AuthorizationServer.Diagnostics;

public sealed class DiagnosticEventStore
{
    private const int Capacity = 200;
    private readonly ConcurrentQueue<DiagnosticEvent> _events = new();

    public IReadOnlyList<DiagnosticEvent> GetEvents() =>
        _events.Reverse().ToArray();

    public void Add(string category, string action, string traceIdentifier, IReadOnlyDictionary<string, string?> details)
    {
        _events.Enqueue(new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            category,
            action,
            traceIdentifier,
            details));

        while (_events.Count > Capacity)
        {
            _events.TryDequeue(out _);
        }
    }

    public void Clear() => _events.Clear();
}
