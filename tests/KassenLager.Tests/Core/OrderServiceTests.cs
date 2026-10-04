using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class OrderServiceTests : LedgerTestBase
{
    private OrderService Orders => new(Database.Factory, Stock, Clock);

    [Fact]
    public async Task Suggestions_AreMinimumMinusUsableStockAndPendingQuantity()
    {
        var cable = await CreateQuantityArticleAsync("Kabel");
        var pc = await CreateSerialArticleAsync(model: "X1");
        var paper = await CreateQuantityArticleAsync("Papier");
        await ReceiveQuantityAsync(Customer1, cable, 2);
        await ReceiveDeviceAsync(Customer1, pc, "S1");
        await ReceiveDeviceAsync(Customer1, pc, "S2", DeviceState.Defective);
        await ReceiveQuantityAsync(Customer1, paper, 10);
        await Stock.SetMinimumAsync(cable, Customer1, 10);
        await Stock.SetMinimumAsync(pc, Customer1, 3);
        await Stock.SetMinimumAsync(paper, Customer1, 5);
        await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 3)]);

        var suggestions = await Orders.GetSuggestionsAsync(Customer1);

        Assert.Equal(2, suggestions.Count);
        var cableSuggestion = suggestions.Single(s => s.ArticleId == cable);
        Assert.Equal((10, 2, 3, 5), (cableSuggestion.Minimum, cableSuggestion.UsableStock, cableSuggestion.Pending, cableSuggestion.SuggestedQuantity));
        Assert.Equal(2, suggestions.Single(s => s.ArticleId == pc).SuggestedQuantity);
        Assert.Empty(await Orders.GetSuggestionsAsync(Customer2));
    }

    [Fact]
    public async Task Suggestions_IgnoreCancelledAndDeliveredOrders()
    {
        var cable = await CreateQuantityArticleAsync();
        await Stock.SetMinimumAsync(cable, Customer1, 4);
        var cancelled = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 4)]);
        await Orders.PlaceAsync(cancelled);
        await Orders.CancelAsync(cancelled);

        Assert.Equal(4, Assert.Single(await Orders.GetSuggestionsAsync(Customer1)).SuggestedQuantity);
    }

    [Fact]
    public async Task Draft_LinesCanBeAddedChangedAndRemoved_ThenTheyAreFixed()
    {
        var cable = await CreateQuantityArticleAsync("Kabel");
        var paper = await CreateQuantityArticleAsync("Papier");
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 2), new OrderLineInput(cable, 1)]);

        await Orders.SetLineAsync(orderId, paper, 5, "dringend");
        await Orders.SetLineAsync(orderId, cable, 0, null);

        var line = Assert.Single((await Orders.GetDetailAsync(orderId)).Lines);
        Assert.Equal((paper, 5, "dringend"), (line.ArticleId, line.Quantity, line.Note));

        await Orders.PlaceAsync(orderId);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Orders.SetLineAsync(orderId, paper, 6, null));
        Assert.Equal(Messages.OrderNotDraft, ex.Message);
        Assert.Equal(OrderStatus.Ordered, (await Orders.GetDetailAsync(orderId)).Order.Status);
    }

    [Fact]
    public async Task CreateAsync_MergesTheSameArticle()
    {
        var cable = await CreateQuantityArticleAsync();

        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 2), new OrderLineInput(cable, 3)]);

        Assert.Equal(5, Assert.Single((await Orders.GetDetailAsync(orderId)).Lines).Quantity);
    }

    [Fact]
    public async Task Place_EmptyDraft_Throws_DeleteDraft_Removes()
    {
        var orderId = await Orders.CreateAsync(Customer1, []);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Orders.PlaceAsync(orderId));
        Assert.Equal(Messages.OrderHasNoLines, ex.Message);

        await Orders.DeleteDraftAsync(orderId);
        Assert.Empty(await Orders.GetListAsync(openOnly: false));
    }

    [Fact]
    public async Task Receive_PartialThenRest_UpdatesStatusAndStock()
    {
        var cable = await CreateQuantityArticleAsync();
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 10)]);
        await Orders.SetHeaderAsync(orderId, "ZB-4711", null);
        await Orders.PlaceAsync(orderId);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;

        await Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 4, [])], DeviceState.New, null, null));
        var partial = await Orders.GetDetailAsync(orderId);
        await Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 6, [])], DeviceState.New, null, null));
        var complete = await Orders.GetDetailAsync(orderId);

        Assert.Equal(OrderStatus.PartiallyDelivered, partial.Order.Status);
        Assert.Equal(6, partial.Lines[0].Open);
        Assert.Equal(OrderStatus.Delivered, complete.Order.Status);
        Assert.Equal(10, await Stock.GetQuantityAsync(cable, Customer1));
        Assert.All(await Journal.GetPageAsync(new MovementFilter(Type: MovementType.GoodsReceipt), 0, 10), m => Assert.Equal("ZB-4711", m.Reference));
    }

    [Fact]
    public async Task Receive_MoreThanOpen_Throws()
    {
        var cable = await CreateQuantityArticleAsync();
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 3)]);
        await Orders.PlaceAsync(orderId);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 4, [])], DeviceState.New, null, null)));

        Assert.Contains("nur noch 3", ex.Message);
        Assert.Equal(0, await Stock.GetQuantityAsync(cable, Customer1));
    }

    [Fact]
    public async Task Receive_NothingEntered_Throws_DraftCannotReceive()
    {
        var cable = await CreateQuantityArticleAsync();
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 3)]);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;

        var draft = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 1, [])], DeviceState.New, null, null)));
        await Orders.PlaceAsync(orderId);
        var empty = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 0, [])], DeviceState.New, null, null)));

        Assert.Equal(Messages.OrderNotReceivable, draft.Message);
        Assert.Equal(Messages.OrderReceiptEmpty, empty.Message);
    }

    [Fact]
    public async Task Receive_SerialNumbers_CreateDevicesForTheOrdersCustomer()
    {
        var pc = await CreateSerialArticleAsync(model: "X1");
        var orderId = await Orders.CreateAsync(Customer2, [new OrderLineInput(pc, 2)]);
        await Orders.PlaceAsync(orderId);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;

        await Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 0, ["S1", " ", "S2"])], DeviceState.New, "Lieferung 1", null));

        var devices = await Devices.GetListAsync(customerId: Customer2);
        Assert.Equal(["S1", "S2"], devices.Select(d => d.SerialNumber));
        var detail = await Orders.GetDetailAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, detail.Order.Status);
        Assert.Equal(2, detail.Lines[0].Received);
    }

    [Fact]
    public async Task ReversingAReceipt_ReopensTheOrder()
    {
        var cable = await CreateQuantityArticleAsync();
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 5)]);
        await Orders.PlaceAsync(orderId);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;
        await Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 5, [])], DeviceState.New, null, null));

        await Reversals.ReverseAsync(await LatestMovementIdAsync(), "falsche Menge");

        var detail = await Orders.GetDetailAsync(orderId);
        Assert.Equal(OrderStatus.Ordered, detail.Order.Status);
        Assert.Equal(0, detail.Lines[0].Received);
        Assert.True(detail.CanCancel);
    }

    [Fact]
    public async Task CancelAndClose_FollowTheDeliveryState()
    {
        var cable = await CreateQuantityArticleAsync();
        var orderId = await Orders.CreateAsync(Customer1, [new OrderLineInput(cable, 5)]);
        await Orders.PlaceAsync(orderId);
        var lineId = (await Orders.GetDetailAsync(orderId)).Lines[0].Id;
        await Orders.ReceiveAsync(new OrderReceiptInput(orderId, [new OrderReceiptLine(lineId, 2, [])], DeviceState.New, null, null));

        var cancel = await Assert.ThrowsAsync<BusinessRuleException>(() => Orders.CancelAsync(orderId));
        await Orders.CloseAsync(orderId);

        Assert.Equal(Messages.OrderCannotCancel, cancel.Message);
        var detail = await Orders.GetDetailAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, detail.Order.Status);
        Assert.Empty(await Orders.GetListAsync(openOnly: true));
        Assert.Equal(0, await Orders.GetOpenCountAsync());
    }

    [Fact]
    public async Task Excel_HasTheColumnsForHeadquarters()
    {
        var cable = await Articles.SaveAsync(null, new ArticleInput("K-1", "USB-Kabel", TestDatabase.QuantityCategoryId, "Generic", "U2", null, TestDatabase.PieceUnitId, null, true));
        var orderId = await Orders.CreateAsync(Customer1, []);
        await Orders.SetLineAsync(orderId, cable, 7, "blau");

        using var stream = new MemoryStream();
        await Orders.WriteExcelAsync(orderId, stream);

        var rows = Xlsx.Rows(stream, "Bestellung");
        Assert.Equal("Kunde|Artikelnummer|Bezeichnung|Hersteller|Modell|Menge|Einheit|Notiz", string.Join("|", rows[0]));
        Assert.Equal("Kunde 1|K-1|USB-Kabel|Generic|U2|7|Stück|blau", string.Join("|", rows[1]));
        Assert.StartsWith($"Bestellung_{orderId}_Kunde-1_", await Orders.GetFileNameAsync(orderId));
    }

    [Fact]
    public async Task ArticleOrCustomerWithOrders_CannotBeDeleted()
    {
        var cable = await CreateQuantityArticleAsync();
        await Orders.CreateAsync(Customer2, [new OrderLineInput(cable, 1)]);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Articles.DeleteAsync(cable));
        await Assert.ThrowsAsync<BusinessRuleException>(() => new CustomerService(Database.Factory).DeleteAsync(Customer2));
    }
}
