using KassenLager.App.ViewModels.Articles;

namespace KassenLager.App.Views.Articles;

public partial class ArticleDetailPage : ContentPageBase
{
    public ArticleDetailPage(ArticleDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
