using KassenLager.App.ViewModels;

namespace KassenLager.App.Views;

public partial class OverviewPage : ContentPageBase
{
    public OverviewPage(OverviewViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
