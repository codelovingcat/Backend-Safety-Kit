using System.Text.Json;
using BackendSafetyKit;
using BackendSafetyKit.Abstractions;
using BackendSafetyKit.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BackendSafetyKit.EntityFrameworkCore.Tests;

public sealed class BackendSafetySaveChangesInterceptorTests
{
    private const string SecretValue = "never-log-this-entity-secret";
    private const string TriggerMessage = "never-expose-this-trigger-message";
    private const string ConnectionString = "Data Source=:memory:";

    [Fact]
    public void SynchronousSaveChangesEmitsSafeCompletionDiagnostic()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var sink = new RecordingDiagnosticSink();
        using var context = CreateContext(connection, sink);
        context.Database.EnsureCreated();
        context.Records.Add(new TestRecord { Id = 42, Secret = SecretValue });

        var affected = context.SaveChanges();

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(1, affected);
        Assert.Equal(
            "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Completed",
            diagnostic.Name);
        Assert.Equal(BackendSafetyDiagnosticSeverity.Information, diagnostic.Severity);
        Assert.Contains("affectedEntries", diagnostic.Properties.Keys);
        AssertSafeDiagnostic(diagnostic);
    }

    [Fact]
    public async Task AsynchronousSaveChangesEmitsSafeCompletionDiagnostic()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var sink = new RecordingDiagnosticSink();
        await using var context = CreateContext(connection, sink);
        await context.Database.EnsureCreatedAsync();
        context.Records.Add(new TestRecord { Id = 43, Secret = SecretValue });

        var affected = await context.SaveChangesAsync();

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(1, affected);
        Assert.Equal(
            "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Completed",
            diagnostic.Name);
        Assert.Equal(BackendSafetyDiagnosticSeverity.Information, diagnostic.Severity);
        Assert.Contains("affectedEntries", diagnostic.Properties.Keys);
        AssertSafeDiagnostic(diagnostic);
    }

    [Fact]
    public void SynchronousSaveChangesFailureEmitsExceptionTypeOnly()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var sink = new RecordingDiagnosticSink();
        using var context = CreateContext(connection, sink);
        context.Database.EnsureCreated();
        AddRejectInsertTrigger(context);
        context.Records.Add(new TestRecord { Id = 44, Secret = SecretValue });

        var exception = Assert.Throws<DbUpdateException>(() => context.SaveChanges());

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(
            "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Failed",
            diagnostic.Name);
        Assert.Equal(BackendSafetyDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("exceptionType", diagnostic.Properties.Keys);
        Assert.Contains("DbUpdateException", diagnostic.Properties["exceptionType"]?.ToString() ?? string.Empty);
        Assert.Contains(TriggerMessage, exception.ToString());
        AssertSafeDiagnostic(diagnostic);
    }

    [Fact]
    public async Task AsynchronousSaveChangesFailureEmitsExceptionTypeOnly()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        var sink = new RecordingDiagnosticSink();
        await using var context = CreateContext(connection, sink);
        await context.Database.EnsureCreatedAsync();
        await AddRejectInsertTriggerAsync(context);
        context.Records.Add(new TestRecord { Id = 45, Secret = SecretValue });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(
            "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Failed",
            diagnostic.Name);
        Assert.Equal(BackendSafetyDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("exceptionType", diagnostic.Properties.Keys);
        Assert.Contains("DbUpdateException", diagnostic.Properties["exceptionType"]?.ToString() ?? string.Empty);
        Assert.Contains(TriggerMessage, exception.ToString());
        AssertSafeDiagnostic(diagnostic);
    }

    [Fact]
    public void DiagnosticSinkFailureDoesNotChangeSaveChangesOutcome()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var context = CreateContext(connection, new ThrowingDiagnosticSink());
        context.Database.EnsureCreated();

        context.Records.Add(new TestRecord { Id = 46, Secret = SecretValue });
        Assert.Equal(1, context.SaveChanges());
        Assert.Equal(1, context.Records.Count());

        AddRejectInsertTrigger(context);
        context.Records.Add(new TestRecord { Id = 47, Secret = SecretValue });
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public async Task DiagnosticSinkFailureDoesNotChangeAsyncSaveChangesOutcome()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using var context = CreateContext(connection, new ThrowingDiagnosticSink());
        await context.Database.EnsureCreatedAsync();

        context.Records.Add(new TestRecord { Id = 48, Secret = SecretValue });
        Assert.Equal(1, await context.SaveChangesAsync());
        Assert.Equal(1, await context.Records.CountAsync());

        await AddRejectInsertTriggerAsync(context);
        context.Records.Add(new TestRecord { Id = 49, Secret = SecretValue });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static TestDbContext CreateContext(
        SqliteConnection connection,
        IBackendSafetyDiagnosticSink sink)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>();
        builder.UseSqlite(connection);
        builder.AddBackendSafetyEntityFrameworkCore([sink]);
        return new TestDbContext(builder.Options);
    }

    private static void AddRejectInsertTrigger(TestDbContext context) =>
        context.Database.ExecuteSqlRaw(
            $"""
             CREATE TRIGGER RejectTestRecordInsert
             BEFORE INSERT ON Records
             BEGIN
                 SELECT RAISE(ABORT, '{TriggerMessage}');
             END;
             """);

    private static Task<int> AddRejectInsertTriggerAsync(TestDbContext context) =>
        context.Database.ExecuteSqlRawAsync(
            $"""
             CREATE TRIGGER RejectTestRecordInsert
             BEFORE INSERT ON Records
             BEGIN
                 SELECT RAISE(ABORT, '{TriggerMessage}');
             END;
             """);

    private static void AssertSafeDiagnostic(BackendSafetyDiagnosticEvent diagnostic)
    {
        var serialized = JsonSerializer.Serialize(diagnostic.Properties);
        Assert.Contains("contextType", diagnostic.Properties.Keys);
        Assert.Contains("durationMilliseconds", diagnostic.Properties.Keys);
        Assert.False(serialized.Contains(SecretValue, StringComparison.Ordinal));
        Assert.False(serialized.Contains(TriggerMessage, StringComparison.Ordinal));
        Assert.False(serialized.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase));
        Assert.False(serialized.Contains("Data Source", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingDiagnosticSink : IBackendSafetyDiagnosticSink
    {
        public List<BackendSafetyDiagnosticEvent> Events { get; } = [];

        public void Write(BackendSafetyDiagnosticEvent diagnosticEvent) =>
            Events.Add(diagnosticEvent);
    }

    private sealed class ThrowingDiagnosticSink : IBackendSafetyDiagnosticSink
    {
        public void Write(BackendSafetyDiagnosticEvent diagnosticEvent) =>
            throw new InvalidOperationException("diagnostic sink deliberately failed");
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestRecord> Records => Set<TestRecord>();
    }

    private sealed class TestRecord
    {
        public int Id { get; set; }

        public string Secret { get; set; } = string.Empty;
    }
}
