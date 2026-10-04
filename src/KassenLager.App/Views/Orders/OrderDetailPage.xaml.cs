using KassenLager.App.ViewModels.Orders;

namespace KassenLager.App.Views.Orders;

public partial class OrderDetailPage : ContentPageBase
{
    public OrderDetailPage(OrderDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
