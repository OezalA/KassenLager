using KassenLager.App.ViewModels.Data;

namespace KassenLager.App.Views.Data;

public partial class ImportPage : ContentPageBase
{
    public ImportPage(ImportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
