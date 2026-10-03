using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KassenLager.Data.Configurations;

// Movements and devices are never deleted, so every reference to them is Restrict:
// the database refuses to remove master data that has history.

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.Property(d => d.SerialNumber).IsRequired().HasMaxLength(Device.SerialNumberMaxLength);
        builder.Property(d => d.SerialNumberKey).IsRequired().HasMaxLength(Device.SerialNumberMaxLength);
        builder.Property(d => d.Note).HasMaxLength(Device.NoteMaxLength);

        // Serial numbers are unique per article among non-voided devices.
        builder.HasIndex(d => new { d.ArticleId, d.SerialNumberKey })
            .IsUnique()
            .HasFilter("\"IsVoided\" = 0");
        builder.HasIndex(d => d.SerialNumberKey);
        builder.HasIndex(d => new { d.CustomerId, d.State });

        builder.HasOne(d => d.Article)
            .WithMany()
            .HasForeignKey(d => d.ArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Customer)
            .WithMany()
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MovementConfiguration : IEntityTypeConfiguration<Movement>
{
    public void Configure(EntityTypeBuilder<Movement> builder)
    {
        builder.ToTable("Movements");
        builder.Property(m => m.Branch).HasMaxLength(Movement.BranchMaxLength);
        builder.Property(m => m.Reference).HasMaxLength(Movement.ReferenceMaxLength);
        builder.Property(m => m.Note).HasMaxLength(Movement.NoteMaxLength);

        builder.HasIndex(m => new { m.ArticleId, m.CustomerId });
        builder.HasIndex(m => m.OccurredAt);

        builder.HasOne(m => m.Customer)
            .WithMany()
            .HasForeignKey(m => m.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Article)
            .WithMany()
            .HasForeignKey(m => m.ArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Device)
            .WithMany(d => d.Movements)
            .HasForeignKey(m => m.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-one: the unique index on ReversalOfId allows only one storno per movement.
        builder.HasOne(m => m.ReversalOf)
            .WithOne(m => m.ReversedBy)
            .HasForeignKey<Movement>(m => m.ReversalOfId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BranchIssueConfiguration : IEntityTypeConfiguration<BranchIssue>
{
    public void Configure(EntityTypeBuilder<BranchIssue> builder)
    {
        builder.ToTable("BranchIssues");
        builder.Property(b => b.CustomerDeviceSerialNumber).HasMaxLength(BranchIssue.CustomerDeviceSerialNumberMaxLength);
        builder.Property(b => b.CustomerDeviceModel).HasMaxLength(BranchIssue.CustomerDeviceModelMaxLength);

        builder.HasOne(b => b.Movement)
            .WithOne(m => m.BranchIssue)
            .HasForeignKey<BranchIssue>(b => b.MovementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.ReturnMovement)
            .WithMany()
            .HasForeignKey(b => b.ReturnMovementId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(b => b.ReturnMovementId).IsUnique();
    }
}

internal sealed class MinimumStockConfiguration : IEntityTypeConfiguration<MinimumStock>
{
    public void Configure(EntityTypeBuilder<MinimumStock> builder)
    {
        builder.ToTable("MinimumStocks");
        builder.HasKey(m => new { m.ArticleId, m.CustomerId });

        // A minimum is a setting, not history: it goes away with its article or customer.
        builder.HasOne(m => m.Article)
            .WithMany()
            .HasForeignKey(m => m.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Customer)
            .WithMany()
            .HasForeignKey(m => m.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
