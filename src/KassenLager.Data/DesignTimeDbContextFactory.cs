using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KassenLager.Data;

/// <summary>Used by <c>dotnet ef</c> only (migrations); the app configures the context via DI.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KassenLagerDbContext>
{
    public KassenLagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KassenLagerDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new KassenLagerDbContext(options);
    }
}
