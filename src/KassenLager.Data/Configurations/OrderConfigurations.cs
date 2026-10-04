using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KassenLager.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.Property(o => o.Reference).HasMaxLength(Order.ReferenceMaxLength);
        builder.Property(o => o.Note).HasMaxLength(Order.NoteMaxLength);
        builder.HasIndex(o => new { o.CustomerId, o.Status });

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Only drafts are deleted, and they take their lines with them.
        builder.HasMany(o => o.Lines)
            .WithOne(l => l.Order)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.Property(l => l.Note).HasMaxLength(OrderLine.NoteMaxLength);
        builder.HasIndex(l => new { l.OrderId, l.ArticleId }).IsUnique();

        builder.HasOne(l => l.Article)
            .WithMany()
            .HasForeignKey(l => l.ArticleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Movements)
            .WithOne(m => m.OrderLine)
            .HasForeignKey(m => m.OrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
