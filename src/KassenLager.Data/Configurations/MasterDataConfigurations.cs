using KassenLager.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KassenLager.Data.Configurations;

// NOCASE on unique names is a database-level backstop for ASCII; the services
// additionally compare Unicode-aware (Ä/ä) before saving.

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(Customer.NameMaxLength).UseCollation("NOCASE");
        builder.Property(c => c.ShortName).HasMaxLength(Customer.ShortNameMaxLength);
        builder.Property(c => c.Note).HasMaxLength(Customer.NoteMaxLength);
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasData(SeedData.Customers);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.Property(c => c.Name).IsRequired().HasMaxLength(Category.NameMaxLength).UseCollation("NOCASE");
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasData(SeedData.Categories);
    }
}

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.Property(u => u.Name).IsRequired().HasMaxLength(Unit.NameMaxLength).UseCollation("NOCASE");
        builder.HasIndex(u => u.Name).IsUnique();
        builder.HasData(SeedData.Units);
    }
}

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.Property(a => a.ArticleNumber).HasMaxLength(Article.ArticleNumberMaxLength);
        builder.Property(a => a.ArticleNumberKey).HasMaxLength(Article.ArticleNumberMaxLength);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(Article.NameMaxLength);
        builder.Property(a => a.Manufacturer).HasMaxLength(Article.ManufacturerMaxLength);
        builder.Property(a => a.Model).HasMaxLength(Article.ModelMaxLength);
        builder.Property(a => a.Ean).HasMaxLength(Article.EanMaxLength);
        builder.Property(a => a.Note).HasMaxLength(Article.NoteMaxLength);

        // SQLite allows any number of NULLs in a unique index, so articles without number are fine.
        builder.HasIndex(a => a.ArticleNumberKey).IsUnique();
        builder.HasIndex(a => a.Model);

        builder.HasOne(a => a.Category)
            .WithMany(c => c.Articles)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Unit)
            .WithMany(u => u.Articles)
            .HasForeignKey(a => a.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(AppSetting.KeyMaxLength);
        builder.Property(s => s.Value).HasMaxLength(AppSetting.ValueMaxLength);
    }
}
