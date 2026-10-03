using KassenLager.App.ViewModels;

namespace KassenLager.App.Views;

public partial class MorePage : ContentPage
{
    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
