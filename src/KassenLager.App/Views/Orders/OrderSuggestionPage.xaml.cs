using KassenLager.App.ViewModels.Orders;

namespace KassenLager.App.Views.Orders;

public partial class OrderSuggestionPage : ContentPageBase
{
    public OrderSuggestionPage(OrderSuggestionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
