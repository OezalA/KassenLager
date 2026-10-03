using KassenLager.App.ViewModels.Pickers;

namespace KassenLager.App.Views.Pickers;

public partial class ArticlePickerPage : ContentPageBase
{
    private readonly ArticlePickerViewModel _viewModel;

    public ArticlePickerPage(ArticlePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cancel();
    }
}
