using System.Collections.Concurrent;
using System.Diagnostics;
using BackendSafetyKit;
using BackendSafetyKit.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BackendSafetyKit.EntityFrameworkCore;

/// <summary>
/// Emits safe completion/failure diagnostic events for Entity Framework Core SaveChanges operations.
/// </summary>
/// <remarks>
/// Events contain context type, elapsed time, affected-entry count on success, and exception type on failure.
/// Entity values, SQL statements, parameters, connection strings, and exception messages are not included.
/// </remarks>
public sealed class BackendSafetySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IBackendSafetyDiagnosticSink[] _sinks;
    private readonly ConcurrentDictionary<Guid, long> _startTimestamps = new();

    /// <summary>
    /// Initializes a new interceptor that forwards events to the supplied diagnostic sinks.
    /// </summary>
    /// <param name="diagnosticSinks">Optional sinks registered by the host application.</param>
    public BackendSafetySaveChangesInterceptor(
        IEnumerable<IBackendSafetyDiagnosticSink>? diagnosticSinks = null)
    {
        _sinks = diagnosticSinks?.ToArray() ?? [];
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Start(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Start(eventData.Context);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Complete(eventData.Context, result, null);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context, result, null);
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Complete(eventData.Context, null, eventData.Exception);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context, null, eventData.Exception);
        return Task.CompletedTask;
    }

    private void Start(DbContext? context)
    {
        if (context is not null)
        {
            _startTimestamps[context.ContextId.InstanceId] = Stopwatch.GetTimestamp();
        }
    }

    private void Complete(DbContext? context, int? affectedEntries, Exception? exception)
    {
        if (context is null || _sinks.Length == 0)
        {
            return;
        }

        var elapsedMilliseconds = _startTimestamps.TryRemove(
            context.ContextId.InstanceId,
            out var startedAt)
            ? Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            : 0d;

        var properties = new Dictionary<string, object?>
        {
            ["contextType"] = context.GetType().FullName ?? context.GetType().Name,
            ["durationMilliseconds"] = elapsedMilliseconds
        };

        if (affectedEntries.HasValue)
        {
            properties["affectedEntries"] = affectedEntries.Value;
        }

        if (exception is not null)
        {
            properties["exceptionType"] = exception.GetType().FullName ?? exception.GetType().Name;
        }

        var succeeded = exception is null;
        var diagnosticEvent = new BackendSafetyDiagnosticEvent(
            succeeded
                ? "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Completed"
                : "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Failed",
            DateTimeOffset.UtcNow,
            succeeded ? BackendSafetyDiagnosticSeverity.Information : BackendSafetyDiagnosticSeverity.Error,
            properties);

        foreach (var sink in _sinks)
        {
            try
            {
                sink.Write(diagnosticEvent);
            }
            catch (Exception)
            {
                // Diagnostic sinks must not change persistence outcomes.
            }
        }
    }
}
