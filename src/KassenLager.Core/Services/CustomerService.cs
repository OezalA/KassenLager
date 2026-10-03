using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record CustomerInput(string? Name, string? ShortName, string? Note, bool IsActive);

public sealed class CustomerService(IAppDbContextFactory dbFactory)
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(bool includeInactive = true, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.Customers.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var customers = await query.ToListAsync(ct);
        return [.. customers.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<Customer> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException();
    }

    /// <summary>Creates (<paramref name="id"/> = null) or updates a customer and returns its id.</summary>
    public async Task<int> SaveAsync(int? id, CustomerInput input, CancellationToken ct = default)
    {
        var name = InputGuard.Required(input.Name, "Name", Customer.NameMaxLength);
        var shortName = InputGuard.Optional(input.ShortName, "Kurzname", Customer.ShortNameMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Customer.NoteMaxLength);

        await using var db = dbFactory.CreateDbContext();

        var names = await db.Customers.Where(c => c.Id != id).Select(c => c.Name).ToListAsync(ct);
        if (names.Any(n => TextKey.EqualsIgnoreCase(n, name)))
        {
            throw new BusinessRuleException(Messages.Format(Messages.CustomerNameExists, name));
        }

        var customer = id is null
            ? db.Customers.Add(new Customer()).Entity
            : await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new EntityNotFoundException();

        customer.Name = name;
        customer.ShortName = shortName;
        customer.Note = note;
        customer.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);
        return customer.Id;
    }

    /// <summary>Only customers without history can be deleted; others are deactivated instead.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new EntityNotFoundException();
        if (await db.Movements.AnyAsync(m => m.CustomerId == id, ct) || await db.Devices.AnyAsync(d => d.CustomerId == id, ct))
        {
            throw new BusinessRuleException(Messages.CustomerHasHistory);
        }

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
    }
}
