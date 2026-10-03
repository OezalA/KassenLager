using KassenLager.App.Views.Articles;
using KassenLager.App.Views.Categories;
using KassenLager.App.Views.Customers;
using KassenLager.App.Views.Settings;
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
        Routing.RegisterRoute(Routes.Categories, typeof(CategoryListPage));
        Routing.RegisterRoute(Routes.CategoryEdit, typeof(CategoryEditPage));
        Routing.RegisterRoute(Routes.Units, typeof(UnitListPage));
        Routing.RegisterRoute(Routes.Articles, typeof(ArticleListPage));
        Routing.RegisterRoute(Routes.ArticleEdit, typeof(ArticleEditPage));
    }
}
