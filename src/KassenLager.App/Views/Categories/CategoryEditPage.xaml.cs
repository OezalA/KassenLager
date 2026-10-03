using KassenLager.App.ViewModels.Categories;

namespace KassenLager.App.Views.Categories;

public partial class CategoryEditPage : ContentPageBase
{
    public CategoryEditPage(CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
