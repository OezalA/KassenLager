using KassenLager.App.ViewModels.Data;

namespace KassenLager.App.Views.Data;

public partial class ExportPage : ContentPageBase
{
    public ExportPage(ExportViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
