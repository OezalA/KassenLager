using KassenLager.App.ViewModels.Units;

namespace KassenLager.App.Views.Units;

public partial class UnitListPage : ContentPageBase
{
    public UnitListPage(UnitListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
