using KassenLager.App.ViewModels.Articles;

namespace KassenLager.App.Views.Articles;

public partial class ArticleEditPage : ContentPageBase
{
    public ArticleEditPage(ArticleEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
