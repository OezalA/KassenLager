using KassenLager.App.ViewModels.Movements;

namespace KassenLager.App.Views.Movements;

public partial class MovementDetailPage : ContentPageBase
{
    public MovementDetailPage(MovementDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
