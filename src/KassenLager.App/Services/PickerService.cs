using KassenLager.Core.Domain;
using KassenLager.Core.Services;

namespace KassenLager.App.Services;

/// <summary>
/// Passed to a picker page; the page completes it with the selection, or with <c>null</c>
/// when the user leaves without choosing.
/// </summary>
public sealed class PickerRequest<TOptions, TResult>(TOptions options)
    where TResult : class
{
    private readonly TaskCompletionSource<TResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TOptions Options { get; } = options;

    public Task<TResult?> Result => _completion.Task;

    public bool IsCompleted => _completion.Task.IsCompleted;

    public void Complete(TResult? result) => _completion.TrySetResult(result);
}

/// <param name="TrackingType">Only articles of this tracking type; <c>null</c> = all.</param>
/// <param name="CustomerId">Shows the stock of this customer next to each article.</param>
public sealed record ArticlePickerOptions(string Title, TrackingType? TrackingType, int? CustomerId);

/// <param name="States">Devices of the customer in one of these states are offered.</param>
/// <param name="DefectiveOnlyByDefault">Starts with the "nur defekte" filter switched on.</param>
public sealed record DevicePickerOptions(string Title, int CustomerId, IReadOnlyList<DeviceState> States, bool DefectiveOnlyByDefault = false);

public interface IPickerService
{
    Task<ArticleListItem?> PickArticleAsync(ArticlePickerOptions options);

    Task<DeviceListItem?> PickDeviceAsync(DevicePickerOptions options);
}

/// <summary>Opens a picker page and awaits the user's choice, so forms stay short.</summary>
public sealed class PickerService(INavigationService navigation) : IPickerService
{
    public Task<ArticleListItem?> PickArticleAsync(ArticlePickerOptions options) =>
        PickAsync<ArticlePickerOptions, ArticleListItem>(Routes.ArticlePicker, options);

    public Task<DeviceListItem?> PickDeviceAsync(DevicePickerOptions options) =>
        PickAsync<DevicePickerOptions, DeviceListItem>(Routes.DevicePicker, options);

    private async Task<TResult?> PickAsync<TOptions, TResult>(string route, TOptions options)
        where TResult : class
    {
        var request = new PickerRequest<TOptions, TResult>(options);
        await navigation.GoToAsync(route, new Dictionary<string, object> { [Routes.RequestParameter] = request });
        return await request.Result;
    }
}
