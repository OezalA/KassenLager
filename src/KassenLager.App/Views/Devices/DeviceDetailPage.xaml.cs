using KassenLager.App.ViewModels.Devices;

namespace KassenLager.App.Views.Devices;

public partial class DeviceDetailPage : ContentPageBase
{
    public DeviceDetailPage(DeviceDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
