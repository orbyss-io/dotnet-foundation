using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NativeEfAnalyzer;

/// <summary>Builds the model for the native tool without opening a connection.</summary>
public sealed class NativeContextFactory : IDesignTimeDbContextFactory<NativeContext>
{
    /// <inheritdoc />
    public NativeContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<NativeContext>();
        builder.UseNpgsql("Host=127.0.0.1;Port=1;Database=synthetic;Username=synthetic;Password=synthetic");
        return new NativeContext(builder.Options);
    }
}
