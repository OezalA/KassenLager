using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.Views.Booking;

public partial class DeviceActionPage : ContentPageBase
{
    public DeviceActionPage(DeviceActionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
