using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class ReversalServiceTests : LedgerTestBase
{
    [Fact]
    public async Task Reverse_QuantityReceipt_RestoresStockAndLinksBothMovements()
    {
        var cable = await CreateQuantityArticleAsync();
        var receipt = await ReceiveQuantityAsync(Customer1, cable, 5);

        var reversal = await Reversals.ReverseAsync(receipt, "falscher Kunde");

        Assert.Equal(0, await Stock.GetQuantityAsync(cable, Customer1));
        var original = await Journal.GetDetailAsync(receipt);
        Assert.Equal(reversal, original.ReversedById);
        Assert.False(original.CanReverse);
        var storno = await Journal.GetDetailAsync(reversal);
        Assert.Equal(MovementType.Reversal, storno.Movement.Type);
        Assert.Equal(MovementType.GoodsReceipt, storno.ReversalOfType);
        Assert.Equal("falscher Kunde", storno.Movement.Note);
    }

    [Fact]
    public async Task Reverse_Consumption_GivesTheQuantityBack()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 5);
        var consumption = await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 2, null, null, null, null));

        await Reversals.ReverseAsync(consumption, null);

        Assert.Equal(5, await Stock.GetQuantityAsync(cable, Customer1));
    }

    [Fact]
    public async Task Reverse_ReceiptWhoseQuantityWasAlreadyUsed_IsBlocked()
    {
        var cable = await CreateQuantityArticleAsync();
        var receipt = await ReceiveQuantityAsync(Customer1, cable, 5);
        await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 3, null, null, null, null));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Reversals.ReverseAsync(receipt, null));

        Assert.Contains("negativ", ex.Message);
        Assert.Equal(2, await Stock.GetQuantityAsync(cable, Customer1));
    }

    [Fact]
    public async Task Reverse_Twice_IsBlocked()
    {
        var cable = await CreateQuantityArticleAsync();
        var receipt = await ReceiveQuantityAsync(Customer1, cable, 5);
        await Reversals.ReverseAsync(receipt, null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Reversals.ReverseAsync(receipt, null));
        Assert.Equal(Messages.AlreadyReversed, ex.Message);
    }

    [Fact]
    public async Task Reverse_AReversal_IsBlocked()
    {
        var cable = await CreateQuantityArticleAsync();
        var receipt = await ReceiveQuantityAsync(Customer1, cable, 5);
        var reversal = await Reversals.ReverseAsync(receipt, null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Reversals.ReverseAsync(reversal, null));
        Assert.Equal(Messages.ReversalOfReversal, ex.Message);
    }

    [Fact]
    public async Task Reverse_DeviceReceipt_VoidsTheDeviceAndFreesTheSerialNumber()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Reversals.ReverseAsync(await LatestMovementIdAsync(), "falsche Seriennummer");

        var device = await LoadDeviceAsync(deviceId);
        Assert.True(device.IsVoided);
        Assert.Empty(await Devices.GetListAsync());
        Assert.Empty(await Stock.GetLinesAsync(Customer1));
        Assert.Equal(2, (await Devices.GetDetailAsync(deviceId)).History.Count);

        var newDevice = await ReceiveDeviceAsync(Customer2, pc, "SN-1");
        Assert.NotEqual(deviceId, newDevice);
    }

    [Fact]
    public async Task Reverse_DeviceMovementThatIsNotTheLatest_IsBlocked()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        var receipt = await LatestMovementIdAsync();
        await IssueAsync(Customer1, deviceId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Reversals.ReverseAsync(receipt, null));
        Assert.Equal(Messages.ReversalNotLatest, ex.Message);
    }

    [Fact]
    public async Task Reverse_DeviceHistoryCanBeUnwoundStepByStep()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        var receipt = await LatestMovementIdAsync();
        var issue = await IssueAsync(Customer1, deviceId);

        await Reversals.ReverseAsync(issue, null);
        Assert.Equal(DeviceState.New, (await LoadDeviceAsync(deviceId)).State);
        Assert.Empty(await Branches.GetListAsync());

        await Reversals.ReverseAsync(receipt, null);
        Assert.True((await LoadDeviceAsync(deviceId)).IsVoided);
    }

    [Fact]
    public async Task Reverse_ReturnOfKnownDevice_SetsItIssuedAgainAndReopensTheIssue()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await IssueAsync(Customer1, deviceId);
        var result = await Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "SN-1", null, null, "Filiale Nord", DeviceState.Defective, null, null, null));

        await Reversals.ReverseAsync(result.MovementId, null);

        Assert.Equal(DeviceState.Issued, (await LoadDeviceAsync(deviceId)).State);
        Assert.False(Assert.Single(await Branches.GetListAsync()).IsReturned);
    }

    [Fact]
    public async Task Reverse_ReturnThatCreatedTheDevice_VoidsIt()
    {
        var pc = await CreateSerialArticleAsync();
        var result = await Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "FREMD-1", null, pc, "Filiale Ost", DeviceState.UsedWorking, null, null, null));

        await Reversals.ReverseAsync(result.MovementId, null);

        Assert.True((await LoadDeviceAsync(result.DeviceId)).IsVoided);
        Assert.Empty(await Stock.GetLinesAsync(Customer1));
    }

    [Fact]
    public async Task Reverse_StateChange_RestoresThePreviousState()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        var change = await Bookings.ChangeDeviceStateAsync(new DeviceActionInput(Customer1, deviceId, null, null, null), DeviceState.Defective);

        await Reversals.ReverseAsync(change, null);

        Assert.Equal(DeviceState.New, (await LoadDeviceAsync(deviceId)).State);
    }

    [Fact]
    public async Task ReversalBlocker_IsReportedWithTheMovementDetail()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        var receipt = await LatestMovementIdAsync();
        var issue = await IssueAsync(Customer1, deviceId);

        Assert.Equal(Messages.ReversalNotLatest, (await Journal.GetDetailAsync(receipt)).ReversalBlocker);
        Assert.True((await Journal.GetDetailAsync(issue)).CanReverse);
        Assert.NotNull((await Journal.GetDetailAsync(issue)).BranchIssueId);
    }

    [Fact]
    public async Task LongMixedHistory_ReplaysConsistently()
    {
        var pc = await CreateSerialArticleAsync();
        var cable = await CreateQuantityArticleAsync();

        var ids = await Bookings.ReceiveDevicesAsync(new DeviceReceiptInput(Customer1, pc, ["A", "B", "C"], DeviceState.New, null, null, null));
        await ReceiveQuantityAsync(Customer1, cable, 10);
        await ReceiveQuantityAsync(Customer2, cable, 4);

        await IssueAsync(Customer1, ids[0], "Filiale 1");
        await Bookings.ChangeDeviceStateAsync(new DeviceActionInput(Customer1, ids[1], null, null, null), DeviceState.Defective);
        await Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, ids[1], null, null, null));
        await Branches.ReturnAsync(new BranchReturnInput(Customer1, "A", null, null, "Filiale 1", DeviceState.Defective, null, null, null));
        await Bookings.ReceiveDevicesAsync(new DeviceReceiptInput(Customer1, pc, ["B"], DeviceState.UsedWorking, null, null, null));
        var consumption = await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 4, "Filiale 2", null, null, null));
        await Bookings.ConsumeAsync(new ConsumptionInput(Customer2, cable, 4, null, null, null, null));
        await Reversals.ReverseAsync(consumption, null);
        await Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer1, ids[2], null, null, null));
        await Reversals.ReverseAsync(await LatestMovementIdAsync(), null);

        var lines = await Stock.GetLinesAsync();
        var pcLine = Assert.Single(lines, l => l.ArticleId == pc);
        Assert.Equal(2, pcLine.Available);
        Assert.Equal(1, pcLine.Defective);
        Assert.Equal(10, Assert.Single(lines, l => l.ArticleId == cable && l.CustomerId == Customer1).Quantity);
        Assert.DoesNotContain(lines, l => l.ArticleId == cable && l.CustomerId == Customer2);

        // The base class replays the complete ledger after the test.
    }
}
