using KassenLager.App.ViewModels.Devices;

namespace KassenLager.App.Views.Devices;

public partial class DeviceListPage : ContentPageBase
{
    public DeviceListPage(DeviceListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
