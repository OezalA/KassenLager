using KassenLager.App.ViewModels.Stock;

namespace KassenLager.App.Views.Stock;

public partial class CustomerStockPage : ContentPageBase
{
    public CustomerStockPage(CustomerStockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
