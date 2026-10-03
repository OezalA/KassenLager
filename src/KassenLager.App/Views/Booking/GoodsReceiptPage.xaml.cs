using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.Views.Booking;

public partial class GoodsReceiptPage : ContentPageBase
{
    public GoodsReceiptPage(GoodsReceiptViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
