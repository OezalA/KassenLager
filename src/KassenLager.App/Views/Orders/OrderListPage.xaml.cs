using KassenLager.App.ViewModels.Orders;

namespace KassenLager.App.Views.Orders;

public partial class OrderListPage : ContentPageBase
{
    public OrderListPage(OrderListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
