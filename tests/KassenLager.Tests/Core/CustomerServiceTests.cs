using KassenLager.Core;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class CustomerServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly CustomerService _service;

    public CustomerServiceTests() => _service = new CustomerService(_database.Factory);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task SaveAsync_New_TrimsAndStoresFields()
    {
        var id = await _service.SaveAsync(null, new CustomerInput("  Markt Nord  ", " MN ", "  ", true));

        var customer = await _service.GetAsync(id);
        Assert.Equal("Markt Nord", customer.Name);
        Assert.Equal("MN", customer.ShortName);
        Assert.Null(customer.Note);
        Assert.Equal("MN", customer.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_WithoutName_Throws(string? name)
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SaveAsync(null, new CustomerInput(name, null, null, true)));
    }

    [Theory]
    [InlineData("kunde 1")]
    [InlineData(" KUNDE 1 ")]
    public async Task SaveAsync_DuplicateNameIgnoringCase_Throws(string name)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SaveAsync(null, new CustomerInput(name, null, null, true)));
        Assert.Contains("bereits vorhanden", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_DuplicateNameWithUmlautsIgnoringCase_Throws()
    {
        await _service.SaveAsync(null, new CustomerInput("Müller Markt", null, null, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SaveAsync(null, new CustomerInput("MÜLLER MARKT", null, null, true)));
    }

    [Fact]
    public async Task SaveAsync_UpdateKeepingOwnName_Succeeds()
    {
        await _service.SaveAsync(1, new CustomerInput("Kunde 1", "K1", "Notiz", false));

        var customer = await _service.GetAsync(1);
        Assert.Equal("K1", customer.ShortName);
        Assert.False(customer.IsActive);
    }

    [Fact]
    public async Task SaveAsync_TooLongShortName_Throws()
    {
        var input = new CustomerInput("Neu", new string('X', 21), null, true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SaveAsync(null, input));
    }

    [Fact]
    public async Task GetAllAsync_CanExcludeInactive()
    {
        await _service.SaveAsync(2, new CustomerInput("Kunde 2", null, null, false));

        var active = await _service.GetAllAsync(includeInactive: false);
        var all = await _service.GetAllAsync();

        Assert.Equal(3, active.Count);
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public async Task DeleteAsync_UnusedCustomer_Removes()
    {
        await _service.DeleteAsync(4);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.GetAsync(4));
    }
}
