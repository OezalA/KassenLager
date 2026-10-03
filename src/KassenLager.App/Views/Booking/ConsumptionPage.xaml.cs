using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.Views.Booking;

public partial class ConsumptionPage : ContentPageBase
{
    public ConsumptionPage(ConsumptionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
