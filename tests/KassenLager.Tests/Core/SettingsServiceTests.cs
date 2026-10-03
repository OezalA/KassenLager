using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly SettingsService _service;

    public SettingsServiceTests() => _service = new SettingsService(_database.Factory);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task UserName_IsNullUntilSet()
    {
        Assert.Null(await _service.GetUserNameAsync());
    }

    [Fact]
    public async Task UserName_RoundTripsTrimmedAndCanBeCleared()
    {
        await _service.SetUserNameAsync("  Max Mustermann ");
        Assert.Equal("Max Mustermann", await _service.GetUserNameAsync());

        await _service.SetUserNameAsync("   ");
        Assert.Null(await _service.GetUserNameAsync());
    }
}
