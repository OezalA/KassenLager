using KassenLager.App.ViewModels.Articles;

namespace KassenLager.App.Views.Articles;

public partial class ArticleListPage : ContentPageBase
{
    public ArticleListPage(ArticleListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
