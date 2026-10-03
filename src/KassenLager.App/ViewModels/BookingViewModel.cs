using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.App.ViewModels.Booking;

namespace KassenLager.App.ViewModels;

/// <summary>Menu of the booking types and the booking history.</summary>
public sealed partial class BookingViewModel(INavigationService navigation)
{
    [RelayCommand]
    private Task OpenRouteAsync(string route) => navigation.GoToAsync(route);

    [RelayCommand]
    private Task OpenDeviceActionAsync(DeviceAction action) =>
        navigation.GoToAsync(Routes.DeviceAction, new Dictionary<string, object> { [Routes.ActionParameter] = action });
}
