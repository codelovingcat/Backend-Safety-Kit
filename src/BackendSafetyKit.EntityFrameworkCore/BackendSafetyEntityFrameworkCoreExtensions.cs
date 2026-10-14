using BackendSafetyKit.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BackendSafetyKit.EntityFrameworkCore;

/// <summary>
/// Provides extension methods for registering Backend Safety Kit diagnostics with Entity Framework Core.
/// </summary>
public static class BackendSafetyEntityFrameworkCoreExtensions
{
    /// <summary>
    /// Adds the Backend Safety Kit SaveChanges interceptor to the supplied options builder.
    /// </summary>
    /// <param name="optionsBuilder">The Entity Framework Core options builder.</param>
    /// <param name="diagnosticSinks">Optional diagnostic sinks owned by the application.</param>
    /// <returns>The same options builder for chaining.</returns>
    public static DbContextOptionsBuilder AddBackendSafetyEntityFrameworkCore(
        this DbContextOptionsBuilder optionsBuilder,
        IEnumerable<IBackendSafetyDiagnosticSink>? diagnosticSinks = null)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        return optionsBuilder.AddInterceptors(
            new BackendSafetySaveChangesInterceptor(diagnosticSinks));
    }
}
