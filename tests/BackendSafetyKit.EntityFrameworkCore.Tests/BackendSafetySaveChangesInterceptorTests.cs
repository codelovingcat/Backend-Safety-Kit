using BackendSafetyKit;
using BackendSafetyKit.Abstractions;
using BackendSafetyKit.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BackendSafetyKit.EntityFrameworkCore.Tests;

public sealed class BackendSafetySaveChangesInterceptorTests
{
    [Fact]
    public async Task SaveChangesEmitsDiagnosticWithoutEntityValues()
    {
        var sink = new RecordingDiagnosticSink();
        var builder = new DbContextOptionsBuilder<TestDbContext>();
        builder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
        builder.AddBackendSafetyEntityFrameworkCore([sink]);

        await using var context = new TestDbContext(builder.Options);
        context.Records.Add(new TestRecord { Id = 42, Secret = "never-log-this" });

        var affected = await context.SaveChangesAsync();

        var diagnostic = Assert.Single(sink.Events);
        Assert.Equal(1, affected);
        Assert.Equal(
            "BackendSafetyKit.EntityFrameworkCore.SaveChanges.Completed",
            diagnostic.Name);
        Assert.Equal(BackendSafetyDiagnosticSeverity.Information, diagnostic.Severity);
        Assert.Contains("durationMilliseconds", diagnostic.Properties.Keys);
        Assert.Contains("affectedEntries", diagnostic.Properties.Keys);
        Assert.DoesNotContain("never-log-this", string.Join("|", diagnostic.Properties.Values));
        Assert.DoesNotContain("42", string.Join("|", diagnostic.Properties.Values));
    }

    private sealed class RecordingDiagnosticSink : IBackendSafetyDiagnosticSink
    {
        public List<BackendSafetyDiagnosticEvent> Events { get; } = [];

        public void Write(BackendSafetyDiagnosticEvent diagnosticEvent) =>
            Events.Add(diagnosticEvent);
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
