using KassenLager.App.ViewModels.Settings;

namespace KassenLager.App.Views.Settings;

public partial class SettingsPage : ContentPageBase
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
