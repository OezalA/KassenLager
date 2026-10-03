using KassenLager.App.ViewModels.Categories;

namespace KassenLager.App.Views.Categories;

public partial class CategoryListPage : ContentPageBase
{
    public CategoryListPage(CategoryListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
