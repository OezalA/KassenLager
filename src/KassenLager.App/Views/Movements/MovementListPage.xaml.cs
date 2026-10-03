using KassenLager.App.ViewModels.Movements;

namespace KassenLager.App.Views.Movements;

public partial class MovementListPage : ContentPageBase
{
    public MovementListPage(MovementListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
