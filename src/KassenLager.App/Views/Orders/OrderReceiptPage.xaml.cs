using KassenLager.App.ViewModels.Orders;

namespace KassenLager.App.Views.Orders;

public partial class OrderReceiptPage : ContentPageBase
{
    public OrderReceiptPage(OrderReceiptViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
