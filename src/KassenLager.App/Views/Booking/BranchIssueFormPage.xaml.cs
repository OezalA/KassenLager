using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.Views.Booking;

public partial class BranchIssueFormPage : ContentPageBase
{
    public BranchIssueFormPage(BranchIssueFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
