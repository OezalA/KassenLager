using KassenLager.App.ViewModels.Pickers;

namespace KassenLager.App.Views.Pickers;

public partial class DevicePickerPage : ContentPageBase
{
    private readonly DevicePickerViewModel _viewModel;

    public DevicePickerPage(DevicePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cancel();
    }
}
