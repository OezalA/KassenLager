using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Core.Text;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class BookingServiceTests : LedgerTestBase
{
    [Fact]
    public async Task ReceiveQuantity_IncreasesStockOfThatCustomerOnly()
    {
        var cable = await CreateQuantityArticleAsync();

        await ReceiveQuantityAsync(Customer1, cable, 5);
        await ReceiveQuantityAsync(Customer1, cable, 3);

        Assert.Equal(8, await Stock.GetQuantityAsync(cable, Customer1));
        Assert.Equal(0, await Stock.GetQuantityAsync(cable, Customer2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(100_001)]
    public async Task ReceiveQuantity_QuantityOutOfRange_Throws(int quantity)
    {
        var cable = await CreateQuantityArticleAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveQuantityAsync(Customer1, cable, quantity));
    }

    [Fact]
    public async Task ReceiveQuantity_WithoutCustomer_Throws()
    {
        var cable = await CreateQuantityArticleAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ReceiveQuantityAsync(new QuantityReceiptInput(null, cable, 1, null, null, null)));
        Assert.Equal(Messages.CustomerRequired, ex.Message);
    }

    [Fact]
    public async Task ReceiveQuantity_ForSerialArticle_Throws()
    {
        var pc = await CreateSerialArticleAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveQuantityAsync(Customer1, pc, 1));
        Assert.Contains("Seriennummern", ex.Message);
    }

    [Fact]
    public async Task ReceiveQuantity_FutureDate_Throws()
    {
        var cable = await CreateQuantityArticleAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ReceiveQuantityAsync(new QuantityReceiptInput(Customer1, cable, 1, null, null, Clock.Today.AddDays(1))));
        Assert.Equal(Messages.DateInFuture, ex.Message);
    }

    [Fact]
    public async Task ReceiveQuantity_PastDate_IsBookedAtNoonLocalTimeOfThatDay()
    {
        var cable = await CreateQuantityArticleAsync();
        var date = Clock.Today.AddDays(-3);

        await Bookings.ReceiveQuantityAsync(new QuantityReceiptInput(Customer1, cable, 1, "LS-4711", null, date));

        var movement = (await Journal.GetPageAsync(new MovementFilter(), 0, 10)).Single();
        Assert.Equal(date.ToDateTime(new TimeOnly(12, 0)), movement.LocalTime);
        Assert.Equal("LS-4711", movement.Reference);
    }

    [Fact]
    public async Task Consume_ReducesStock()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 5);

        await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 2, " Filiale 12 ", "T-100", null, null));

        Assert.Equal(3, await Stock.GetQuantityAsync(cable, Customer1));
        var movement = (await Journal.GetPageAsync(new MovementFilter(Type: MovementType.Consumption), 0, 10)).Single();
        Assert.Equal(-2, movement.QuantityChange);
        Assert.Equal("Filiale 12", movement.Branch);
    }

    [Fact]
    public async Task Consume_MoreThanInStock_ThrowsAndBooksNothing()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 3, null, null, null, null)));

        Assert.Contains("nur 2 Stück", ex.Message);
        Assert.Equal(2, await Stock.GetQuantityAsync(cable, Customer1));
    }

    [Fact]
    public async Task Consume_StockOfAnotherCustomerDoesNotCount()
    {
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer2, cable, 10);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 1, null, null, null, null)));
    }

    [Fact]
    public async Task ReceiveDevices_CreatesOneDeviceAndMovementPerSerialNumber()
    {
        var pc = await CreateSerialArticleAsync();

        var ids = await Bookings.ReceiveDevicesAsync(new DeviceReceiptInput(
            Customer1, pc, ["SN-1", " SN-2 ", ""], DeviceState.New, "B-77", null, null));

        Assert.Equal(2, ids.Count);
        var devices = await Devices.GetListAsync(customerId: Customer1);
        Assert.Equal(["SN-1", "SN-2"], devices.Select(d => d.SerialNumber));
        Assert.All(devices, d => Assert.Equal(DeviceState.New, d.State));
        Assert.Equal(2, (await Journal.GetPageAsync(new MovementFilter(Type: MovementType.GoodsReceipt), 0, 10)).Count);
    }

    [Fact]
    public async Task ReceiveDevices_SameSerialTwiceInInput_Throws()
    {
        var pc = await CreateSerialArticleAsync();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Bookings.ReceiveDevicesAsync(
            new DeviceReceiptInput(Customer1, pc, ["abc", "ABC"], DeviceState.New, null, null, null)));
        Assert.Contains("mehrfach", ex.Message);
    }

    [Fact]
    public async Task ReceiveDevices_SerialAlreadyInStock_ThrowsAndBooksNothing()
    {
        var pc = await CreateSerialArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Bookings.ReceiveDevicesAsync(
            new DeviceReceiptInput(Customer1, pc, ["SN-2", "sn-1"], DeviceState.New, null, null, null)));

        Assert.Contains("bereits im Lager", ex.Message);
        Assert.Single(await Devices.GetListAsync());
    }

    [Fact]
    public async Task ReceiveDevices_SameSerialForAnotherArticle_IsAllowed()
    {
        var pc = await CreateSerialArticleAsync();
        var printer = await CreateSerialArticleAsync("Bondrucker", "TM-T88VI", "Epson");
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await ReceiveDeviceAsync(Customer1, printer, "SN-1");

        Assert.Equal(2, (await Devices.GetListAsync()).Count);
    }

    [Fact]
    public async Task ReceiveDevices_KnownDeviceBackFromHeadquarters_IsReceivedAgainWithHistory()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1", DeviceState.Defective);
        await Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, deviceId, "RMA-1", null, null));

        var ids = await Bookings.ReceiveDevicesAsync(new DeviceReceiptInput(
            Customer1, pc, ["SN-1"], DeviceState.UsedWorking, null, null, null));

        Assert.Equal(deviceId, ids.Single());
        var detail = await Devices.GetDetailAsync(deviceId);
        Assert.Equal(DeviceState.UsedWorking, detail.Device.State);
        Assert.Equal(3, detail.History.Count);
    }

    [Fact]
    public async Task ReceiveDevices_KnownDeviceOfAnotherCustomer_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer1, deviceId, null, null, null));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveDeviceAsync(Customer2, pc, "SN-1"));
        Assert.Contains("zwischen Kunden", ex.Message);
    }

    [Fact]
    public async Task ReceiveDevices_StateOutsideTheStore_Throws()
    {
        var pc = await CreateSerialArticleAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => ReceiveDeviceAsync(Customer1, pc, "SN-1", DeviceState.Issued));
    }

    [Fact]
    public async Task ChangeDeviceState_KeepsDeviceInStore()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Bookings.ChangeDeviceStateAsync(new DeviceActionInput(Customer1, deviceId, null, "Lüfter laut", null), DeviceState.Defective);

        var line = Assert.Single(await Stock.GetLinesAsync(Customer1));
        Assert.Equal(0, line.Available);
        Assert.Equal(1, line.Defective);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public async Task ChangeDeviceState_ToSameState_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ChangeDeviceStateAsync(new DeviceActionInput(Customer1, deviceId, null, null, null), DeviceState.New));
    }

    [Fact]
    public async Task ChangeDeviceState_ToStateOutsideTheStore_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ChangeDeviceStateAsync(new DeviceActionInput(Customer1, deviceId, null, null, null), DeviceState.Disposed));
    }

    [Fact]
    public async Task ReturnToHeadquarters_WorksForAnyDeviceInStore_AndRemovesItFromStock()
    {
        var pc = await CreateSerialArticleAsync();
        var working = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        var defective = await ReceiveDeviceAsync(Customer1, pc, "SN-2", DeviceState.Defective);

        await Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, working, null, null, null));
        await Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, defective, null, null, null));

        Assert.Empty(await Stock.GetLinesAsync(Customer1));
        Assert.Equal(DeviceState.ReturnedToHeadquarters, (await LoadDeviceAsync(working)).State);
    }

    [Fact]
    public async Task DeviceAction_DeviceOfAnotherCustomer_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer2, deviceId, null, null, null)));
        Assert.Contains("zwischen Kunden", ex.Message);
    }

    [Fact]
    public async Task DeviceAction_DeviceNotInStore_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer1, deviceId, null, null, null));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.ReturnToHeadquartersAsync(new DeviceActionInput(Customer1, deviceId, null, null, null)));
        Assert.Contains("nicht im Lager", ex.Message);
    }

    [Fact]
    public async Task DeviceAction_BackDatedBeforeLatestDeviceMovement_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer1, deviceId, null, null, Clock.Today.AddDays(-1))));
        Assert.Contains("vor der letzten Buchung", ex.Message);
    }

    [Fact]
    public async Task DeviceAction_BackDatedToTheSameDay_IsOrderedAfterTheLatestMovement()
    {
        var pc = await CreateSerialArticleAsync();
        var past = Clock.Today.AddDays(-2);
        var deviceId = (await Bookings.ReceiveDevicesAsync(
            new DeviceReceiptInput(Customer1, pc, ["SN-1"], DeviceState.New, null, null, past))).Single();
        Clock.Advance(TimeSpan.FromHours(1));

        await Bookings.DisposeDeviceAsync(new DeviceActionInput(Customer1, deviceId, null, null, past));

        var history = (await Devices.GetDetailAsync(deviceId)).History;
        Assert.True(history[0].OccurredAt > history[1].OccurredAt);
        Assert.Equal(past, AppTime.ToLocalDate(history[0].OccurredAt));
    }
}
