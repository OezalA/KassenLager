using KassenLager.App.ViewModels;

namespace KassenLager.App.Views;

public partial class SearchPage : ContentPageBase
{
    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
