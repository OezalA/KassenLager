namespace KassenLager.App;

public static class Routes
{
    // Tabs
    public const string Overview = "overview";
    public const string Search = "search";
    public const string Booking = "booking";
    public const string Inventory = "inventory";
    public const string More = "more";

    // Pushed pages
    public const string Settings = "settings";
    public const string Customers = "customers";
    public const string CustomerEdit = "customer-edit";
    public const string CustomerStock = "customer-stock";
    public const string Categories = "categories";
    public const string CategoryEdit = "category-edit";
    public const string Units = "units";
    public const string Articles = "articles";
    public const string ArticleDetail = "article-detail";
    public const string ArticleEdit = "article-edit";
    public const string Devices = "devices";
    public const string DeviceDetail = "device-detail";
    public const string BranchIssues = "branch-issues";
    public const string BranchIssueDetail = "branch-issue-detail";
    public const string Movements = "movements";
    public const string MovementDetail = "movement-detail";
    public const string Data = "data";
    public const string Import = "import";
    public const string Export = "export";

    // Booking forms
    public const string GoodsReceipt = "goods-receipt";
    public const string Consumption = "consumption";
    public const string BranchIssueBooking = "branch-issue-booking";
    public const string BranchReturnBooking = "branch-return-booking";
    public const string DeviceAction = "device-action";

    // Pickers (return a selection to the calling page)
    public const string ArticlePicker = "article-picker";
    public const string DevicePicker = "device-picker";

    /// <summary>Query parameter carrying the id of the entity to show or edit; absent for "new".</summary>
    public const string IdParameter = "id";

    /// <summary>Optional customer to preselect (int).</summary>
    public const string CustomerIdParameter = "customerId";

    /// <summary>Optional device to preselect in a booking form (int).</summary>
    public const string DeviceIdParameter = "deviceId";

    /// <summary>Which device booking the device action form performs (<see cref="ViewModels.Booking.DeviceAction"/>).</summary>
    public const string ActionParameter = "action";

    /// <summary>Which export the export page creates (<see cref="ViewModels.Data.ExportKind"/>).</summary>
    public const string KindParameter = "kind";

    /// <summary>Picker request object.</summary>
    public const string RequestParameter = "request";
}
