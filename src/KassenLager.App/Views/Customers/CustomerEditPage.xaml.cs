using KassenLager.App.ViewModels.Customers;

namespace KassenLager.App.Views.Customers;

public partial class CustomerEditPage : ContentPageBase
{
    public CustomerEditPage(CustomerEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
