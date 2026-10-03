using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Tests.Infrastructure;

/// <summary>
/// Base for tests of the stock ledger: services wired to a fresh database and a fixed clock,
/// helpers for common bookings, and a replay of the complete history after every test.
/// </summary>
public abstract class LedgerTestBase : IAsyncLifetime
{
    // Seeded customers, see SeedData.
    protected const int Customer1 = 1;
    protected const int Customer2 = 2;

    protected LedgerTestBase()
    {
        Database = new TestDatabase();
        Clock = new TestClock();
        Articles = new ArticleService(Database.Factory);
        Stock = new StockService(Database.Factory);
        Bookings = new BookingService(Database.Factory, Clock);
        Branches = new BranchService(Database.Factory, Clock);
        Reversals = new ReversalService(Database.Factory, Clock);
        Devices = new DeviceService(Database.Factory);
        Journal = new JournalService(Database.Factory);
        Search = new SearchService(Database.Factory, Articles, Stock);
    }

    protected TestDatabase Database { get; }

    protected TestClock Clock { get; }

    protected ArticleService Articles { get; }

    protected StockService Stock { get; }

    protected BookingService Bookings { get; }

    protected BranchService Branches { get; }

    protected ReversalService Reversals { get; }

    protected DeviceService Devices { get; }

    protected JournalService Journal { get; }

    protected SearchService Search { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        try
        {
            await AssertLedgerIsConsistentAsync();
        }
        finally
        {
            Database.Dispose();
        }
    }

    protected Task<int> CreateSerialArticleAsync(string name = "Kassenrechner", string model = "BEETLE /M-III", string? manufacturer = "Diebold Nixdorf") =>
        Articles.SaveAsync(null, new ArticleInput(null, name, TestDatabase.SerialCategoryId, manufacturer, model, null, TestDatabase.PieceUnitId, null, true));

    protected Task<int> CreateQuantityArticleAsync(string name = "USB-Kabel 2 m", string? articleNumber = null) =>
        Articles.SaveAsync(null, new ArticleInput(articleNumber, name, TestDatabase.QuantityCategoryId, null, null, null, TestDatabase.PieceUnitId, null, true));

    protected Task<int> ReceiveQuantityAsync(int customerId, int articleId, int quantity) =>
        Bookings.ReceiveQuantityAsync(new QuantityReceiptInput(customerId, articleId, quantity, null, null, null));

    protected async Task<int> ReceiveDeviceAsync(int customerId, int articleId, string serialNumber, DeviceState state = DeviceState.New) =>
        (await Bookings.ReceiveDevicesAsync(new DeviceReceiptInput(customerId, articleId, [serialNumber], state, null, null, null))).Single();

    protected Task<int> IssueAsync(int customerId, int deviceId, string branch = "Filiale Nord", string? customerSerial = "KD-1") =>
        Branches.IssueAsync(new BranchIssueInput(
            customerId, deviceId, branch, BranchIssueKind.Loan, customerSerial, null, null, null, null, null));

    protected async Task<Device> LoadDeviceAsync(int id)
    {
        await using var db = Database.CreateContext();
        return await db.Devices.AsNoTracking().SingleAsync(d => d.Id == id);
    }

    protected async Task<int> LatestMovementIdAsync()
    {
        await using var db = Database.CreateContext();
        return await db.Movements.MaxAsync(m => m.Id);
    }

    /// <summary>
    /// Replays the whole ledger: every device's state chain must lead to its current state,
    /// device movements must carry the matching stock change, quantities must never be negative
    /// and the movement sums of serial articles must equal the devices in the store.
    /// </summary>
    protected async Task AssertLedgerIsConsistentAsync()
    {
        await using var db = Database.CreateContext();
        var movements = await db.Movements.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        var devices = await db.Devices.AsNoTracking().ToListAsync();
        var trackingTypes = await db.Articles.AsNoTracking()
            .Select(a => new { a.Id, a.Category!.TrackingType })
            .ToDictionaryAsync(a => a.Id, a => a.TrackingType);

        foreach (var device in devices)
        {
            var history = movements.Where(m => m.DeviceId == device.Id).ToList();
            Assert.NotEmpty(history);
            Assert.Null(history[0].FromState);

            DeviceState? state = null;
            foreach (var movement in history)
            {
                Assert.Equal(state, movement.FromState);
                Assert.Equal(DeviceStates.StockChange(movement.FromState, movement.ToState), movement.QuantityChange);
                Assert.Equal(device.CustomerId, movement.CustomerId);
                Assert.Equal(device.ArticleId, movement.ArticleId);
                state = movement.ToState;
            }

            if (device.IsVoided)
            {
                Assert.Null(state);
            }
            else
            {
                Assert.Equal(device.State, state);
            }
        }

        foreach (var reversal in movements.Where(m => m.Type == MovementType.Reversal))
        {
            var original = movements.Single(m => m.Id == reversal.ReversalOfId);
            Assert.NotEqual(MovementType.Reversal, original.Type);
            Assert.Equal(-original.QuantityChange, reversal.QuantityChange);
        }

        foreach (var group in movements.GroupBy(m => (m.ArticleId, m.CustomerId)))
        {
            var sum = group.Sum(m => m.QuantityChange);
            if (trackingTypes[group.Key.ArticleId] == TrackingType.Serial)
            {
                var inStore = devices.Count(d =>
                    d.ArticleId == group.Key.ArticleId && d.CustomerId == group.Key.CustomerId && !d.IsVoided && d.State.IsInStore());
                Assert.Equal(inStore, sum);
            }
            else
            {
                Assert.True(sum >= 0, $"Negative stock for article {group.Key.ArticleId}, customer {group.Key.CustomerId}.");
            }
        }
    }
}
