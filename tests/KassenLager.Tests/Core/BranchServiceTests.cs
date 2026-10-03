using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using KassenLager.Tests.Infrastructure;

namespace KassenLager.Tests.Core;

public sealed class BranchServiceTests : LedgerTestBase
{
    [Fact]
    public async Task Issue_RemovesDeviceFromStockAndKeepsTheRecord()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Branches.IssueAsync(new BranchIssueInput(
            Customer1, deviceId, "Filiale Nord", BranchIssueKind.Loan, "KD-99", "BEETLE /M-II", Clock.Today, "T-1", "Lüfter defekt", null));

        Assert.Equal(DeviceState.Issued, (await LoadDeviceAsync(deviceId)).State);
        Assert.Empty(await Stock.GetLinesAsync(Customer1));

        var issue = Assert.Single(await Branches.GetListAsync());
        Assert.Equal("Filiale Nord", issue.Branch);
        Assert.Equal(BranchIssueKind.Loan, issue.Kind);
        Assert.Equal("KD-99", issue.CustomerDeviceSerialNumber);
        Assert.Equal("BEETLE /M-II", issue.CustomerDeviceModel);
        Assert.Equal(Clock.Today, issue.CustomerDeviceSentOn);
        Assert.Equal("T-1", issue.Reference);
        Assert.False(issue.IsReturned);
    }

    [Fact]
    public async Task Issue_LoanWithoutCustomerDeviceSerial_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(Customer1, deviceId, customerSerial: " "));
        Assert.Equal(Messages.CustomerDeviceSerialRequired, ex.Message);
    }

    [Fact]
    public async Task Issue_PermanentInstallationWithoutCustomerDeviceSerial_Succeeds()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        await Branches.IssueAsync(new BranchIssueInput(
            Customer1, deviceId, "Filiale Süd", BranchIssueKind.PermanentInstallation, null, null, null, null, null, null));

        Assert.Equal(BranchIssueKind.PermanentInstallation, Assert.Single(await Branches.GetListAsync()).Kind);
    }

    [Fact]
    public async Task Issue_WithoutBranch_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(Customer1, deviceId, branch: ""));
        Assert.Contains("Filiale", ex.Message);
    }

    [Fact]
    public async Task Issue_DeviceOfAnotherCustomer_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(Customer2, deviceId));
        Assert.Contains("zwischen Kunden", ex.Message);
        Assert.Equal(DeviceState.New, (await LoadDeviceAsync(deviceId)).State);
    }

    [Fact]
    public async Task Issue_DefectiveDevice_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1", DeviceState.Defective);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(Customer1, deviceId));
        Assert.Contains("defekt", ex.Message);
    }

    [Fact]
    public async Task Issue_DeviceAlreadyIssued_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await IssueAsync(Customer1, deviceId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => IssueAsync(Customer1, deviceId, "Filiale West"));
        Assert.Contains("nicht im Lager", ex.Message);
    }

    [Fact]
    public async Task Return_KnownIssuedDevice_ReactivatesItAndMarksTheIssueReturned()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await IssueAsync(Customer1, deviceId);
        Clock.Advance(TimeSpan.FromDays(14));

        var result = await Branches.ReturnAsync(new BranchReturnInput(
            Customer1, " sn-1 ", null, null, "Filiale Nord", DeviceState.UsedWorking, "T-2", null, null));

        Assert.Equal(deviceId, result.DeviceId);
        Assert.False(result.IsNewDevice);
        Assert.Equal(DeviceState.UsedWorking, (await LoadDeviceAsync(deviceId)).State);

        var issue = Assert.Single(await Branches.GetListAsync());
        Assert.True(issue.IsReturned);
        Assert.Equal("Filiale Nord", issue.ReturnBranch);
        Assert.Equal(3, (await Devices.GetDetailAsync(deviceId)).History.Count);
    }

    [Fact]
    public async Task Return_UnknownSerialWithArticle_CreatesDevice()
    {
        var pc = await CreateSerialArticleAsync();

        var result = await Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "FREMD-1", null, pc, "Filiale Ost", DeviceState.Defective, null, null, null));

        Assert.True(result.IsNewDevice);
        var device = await Devices.GetAsync(result.DeviceId);
        Assert.Equal("FREMD-1", device.SerialNumber);
        Assert.Equal(Customer1, device.CustomerId);
        Assert.Equal(DeviceState.Defective, device.State);
    }

    [Fact]
    public async Task Return_UnknownSerialWithoutArticle_AsksForTheArticle()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "FREMD-1", null, null, "Filiale Ost", DeviceState.UsedWorking, null, null, null)));

        Assert.Contains("Artikel (Modell) auswählen", ex.Message);
    }

    [Fact]
    public async Task Return_SerialKnownForTwoArticles_AsksForTheArticle()
    {
        var pc = await CreateSerialArticleAsync();
        var printer = await CreateSerialArticleAsync("Bondrucker", "TM-T88VI", "Epson");
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "SN-1"));
        var printerDevice = await ReceiveDeviceAsync(Customer1, printer, "SN-1");
        await IssueAsync(Customer1, printerDevice, "Filiale West");

        await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "SN-1", null, null, "Filiale West", DeviceState.UsedWorking, null, null, null)));

        var result = await Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "SN-1", null, printer, "Filiale West", DeviceState.UsedWorking, null, null, null));
        Assert.Equal(printerDevice, result.DeviceId);
    }

    [Fact]
    public async Task Return_DeviceOfAnotherCustomer_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        var deviceId = await ReceiveDeviceAsync(Customer1, pc, "SN-1");
        await IssueAsync(Customer1, deviceId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.ReturnAsync(new BranchReturnInput(
            Customer2, "SN-1", null, null, "Filiale Nord", DeviceState.UsedWorking, null, null, null)));

        Assert.Contains("zwischen Kunden", ex.Message);
        Assert.Equal(DeviceState.Issued, (await LoadDeviceAsync(deviceId)).State);
    }

    [Fact]
    public async Task Return_DeviceStillInStock_Throws()
    {
        var pc = await CreateSerialArticleAsync();
        await ReceiveDeviceAsync(Customer1, pc, "SN-1");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "SN-1", null, null, "Filiale Nord", DeviceState.UsedWorking, null, null, null)));
        Assert.Contains("bereits im Lager", ex.Message);
    }

    [Theory]
    [InlineData(DeviceState.New)]
    [InlineData(DeviceState.Issued)]
    public async Task Return_StateOtherThanUsedOrDefective_Throws(DeviceState state)
    {
        var pc = await CreateSerialArticleAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.ReturnAsync(new BranchReturnInput(
            Customer1, "SN-1", null, pc, "Filiale Nord", state, null, null, null)));
    }

    [Fact]
    public async Task BranchSuggestions_AreDistinctPerCustomerAndMostRecentFirst()
    {
        var pc = await CreateSerialArticleAsync();
        var cable = await CreateQuantityArticleAsync();
        await ReceiveQuantityAsync(Customer1, cable, 10);
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "SN-1"), "Filiale Nord");
        await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 1, "Filiale Süd", null, null, null));
        await Bookings.ConsumeAsync(new ConsumptionInput(Customer1, cable, 1, "filiale nord", null, null, null));
        await IssueAsync(Customer2, await ReceiveDeviceAsync(Customer2, pc, "SN-2"), "Markt 7");

        var suggestions = await Branches.GetBranchSuggestionsAsync(Customer1);

        Assert.Equal(["filiale nord", "Filiale Süd"], suggestions);
    }

    [Fact]
    public async Task SetCustomerDeviceSentOn_CanBeEnteredLaterAndCleared()
    {
        var pc = await CreateSerialArticleAsync();
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "SN-1"));
        var issueId = Assert.Single(await Branches.GetListAsync()).Id;

        await Branches.SetCustomerDeviceSentOnAsync(issueId, Clock.Today.AddDays(-1));
        Assert.Equal(Clock.Today.AddDays(-1), (await Branches.GetAsync(issueId)).CustomerDeviceSentOn);

        await Branches.SetCustomerDeviceSentOnAsync(issueId, null);
        Assert.Null((await Branches.GetAsync(issueId)).CustomerDeviceSentOn);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Branches.SetCustomerDeviceSentOnAsync(issueId, Clock.Today.AddDays(1)));
    }

    [Fact]
    public async Task BranchIssueListItem_Matches_BranchAndBothSerialNumbers()
    {
        var pc = await CreateSerialArticleAsync();
        await IssueAsync(Customer1, await ReceiveDeviceAsync(Customer1, pc, "LEIH-123"), "Filiale Nord", "KUNDE-456");
        var issue = Assert.Single(await Branches.GetListAsync());

        Assert.True(issue.Matches("nord"));
        Assert.True(issue.Matches("leih-1"));
        Assert.True(issue.Matches("456"));
        Assert.False(issue.Matches("Süd"));
    }
}
