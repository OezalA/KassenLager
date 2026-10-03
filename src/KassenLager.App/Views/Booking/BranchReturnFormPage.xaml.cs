using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.Views.Booking;

public partial class BranchReturnFormPage : ContentPageBase
{
    public BranchReturnFormPage(BranchReturnFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
