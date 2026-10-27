using Microsoft.EntityFrameworkCore;

namespace NativeEfAnalyzer;

/// <summary>Owns a synthetic relational model for native migration generation.</summary>
public sealed class NativeContext(DbContextOptions<NativeContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NativeRecord>();
        entity.ToTable("native_records");
        entity.HasKey(nameof(NativeRecord.Id));
        var name = entity.Property<string>(nameof(NativeRecord.Name));
        name.HasMaxLength(120);
        name.IsRequired();
        entity.HasIndex(nameof(NativeRecord.Name));
    }
}

