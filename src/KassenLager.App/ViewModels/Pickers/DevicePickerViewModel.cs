using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Pickers;

/// <summary>Chooses one of the customer's devices by serial number or model.</summary>
public sealed partial class DevicePickerViewModel(
    DeviceService devices,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<DevicePickerViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private PickerRequest<DevicePickerOptions, DeviceListItem>? _request;
    private IReadOnlyList<DeviceListItem> _all = [];
    private bool _isLoaded;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? FilterText { get; set; }

    [ObservableProperty]
    public partial bool CanFilterDefective { get; set; }

    [ObservableProperty]
    public partial bool DefectiveOnly { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DeviceListItem>? Items { get; set; }

    [ObservableProperty]
    public partial string? EmptyText { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _request = query.TryGetValue(Routes.RequestParameter, out var value)
            ? value as PickerRequest<DevicePickerOptions, DeviceListItem>
            : null;
        if (_request is not null)
        {
            Title = _request.Options.Title;
            CanFilterDefective = _request.Options.States.Contains(DeviceState.Defective) && _request.Options.States.Count > 1;
            DefectiveOnly = CanFilterDefective && _request.Options.DefectiveOnlyByDefault;
        }
    }

    public Task LoadAsync() => _isLoaded || _request is null ? Task.CompletedTask : RunAsync(async () =>
    {
        _all = await devices.GetListAsync(_request.Options.CustomerId, states: _request.Options.States);
        ApplyFilter();
        _isLoaded = true;
    });

    public void Cancel() => _request?.Complete(null);

    partial void OnFilterTextChanged(string? value) => ApplyFilter();

    partial void OnDefectiveOnlyChanged(bool value) => ApplyFilter();

    [RelayCommand]
    private async Task SelectAsync(DeviceListItem device)
    {
        if (_request is null || _request.IsCompleted)
        {
            return;
        }

        _request.Complete(device);
        await navigation.GoBackAsync();
    }

    private void ApplyFilter()
    {
        Items = [.. _all.Where(d => (!DefectiveOnly || d.IsDefective) && d.Matches(FilterText))];
        EmptyText = _all.Count == 0 ? "Für diesen Kunden ist kein passendes Gerät im Lager." : "Keine Geräte gefunden.";
    }
}
