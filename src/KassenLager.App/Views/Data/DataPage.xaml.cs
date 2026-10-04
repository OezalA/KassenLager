using KassenLager.App.ViewModels.Data;

namespace KassenLager.App.Views.Data;

public partial class DataPage : ContentPageBase
{
    public DataPage(DataViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
