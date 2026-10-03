using KassenLager.App.Views.Articles;
using KassenLager.App.Views.Booking;
using KassenLager.App.Views.BranchIssues;
using KassenLager.App.Views.Categories;
using KassenLager.App.Views.Customers;
using KassenLager.App.Views.Devices;
using KassenLager.App.Views.Movements;
using KassenLager.App.Views.Pickers;
using KassenLager.App.Views.Settings;
using KassenLager.App.Views.Stock;
using KassenLager.App.Views.Units;

namespace KassenLager.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Detail pages pushed on top of the tabs; resolved through DI.
        Routing.RegisterRoute(Routes.Settings, typeof(SettingsPage));
        Routing.RegisterRoute(Routes.Customers, typeof(CustomerListPage));
        Routing.RegisterRoute(Routes.CustomerEdit, typeof(CustomerEditPage));
        Routing.RegisterRoute(Routes.CustomerStock, typeof(CustomerStockPage));
        Routing.RegisterRoute(Routes.Categories, typeof(CategoryListPage));
        Routing.RegisterRoute(Routes.CategoryEdit, typeof(CategoryEditPage));
        Routing.RegisterRoute(Routes.Units, typeof(UnitListPage));
        Routing.RegisterRoute(Routes.Articles, typeof(ArticleListPage));
        Routing.RegisterRoute(Routes.ArticleDetail, typeof(ArticleDetailPage));
        Routing.RegisterRoute(Routes.ArticleEdit, typeof(ArticleEditPage));
        Routing.RegisterRoute(Routes.Devices, typeof(DeviceListPage));
        Routing.RegisterRoute(Routes.DeviceDetail, typeof(DeviceDetailPage));
        Routing.RegisterRoute(Routes.BranchIssues, typeof(BranchIssueListPage));
        Routing.RegisterRoute(Routes.BranchIssueDetail, typeof(BranchIssueDetailPage));
        Routing.RegisterRoute(Routes.Movements, typeof(MovementListPage));
        Routing.RegisterRoute(Routes.MovementDetail, typeof(MovementDetailPage));

        Routing.RegisterRoute(Routes.GoodsReceipt, typeof(GoodsReceiptPage));
        Routing.RegisterRoute(Routes.Consumption, typeof(ConsumptionPage));
        Routing.RegisterRoute(Routes.BranchIssueBooking, typeof(BranchIssueFormPage));
        Routing.RegisterRoute(Routes.BranchReturnBooking, typeof(BranchReturnFormPage));
        Routing.RegisterRoute(Routes.DeviceAction, typeof(DeviceActionPage));

        Routing.RegisterRoute(Routes.ArticlePicker, typeof(ArticlePickerPage));
        Routing.RegisterRoute(Routes.DevicePicker, typeof(DevicePickerPage));
    }
}
