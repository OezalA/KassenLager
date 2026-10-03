using KassenLager.App.ViewModels.Customers;

namespace KassenLager.App.Views.Customers;

public partial class CustomerListPage : ContentPageBase
{
    public CustomerListPage(CustomerListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
