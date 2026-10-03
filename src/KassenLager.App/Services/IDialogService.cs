using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace KassenLager.App.Services;

/// <summary>User dialogs, abstracted so view models do not depend on pages.</summary>
public interface IDialogService
{
    Task ShowErrorAsync(string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = "Abbrechen");

    /// <summary>Returns the entered text, or <c>null</c> when cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string? initialValue = null, int maxLength = -1);

    /// <summary>Returns the chosen option, or <c>null</c> when cancelled.</summary>
    Task<string?> ChooseAsync(string title, string? destructive, params string[] options);

    Task ToastAsync(string message);
}

public sealed class DialogService : IDialogService
{
    private const string Cancel = "Abbrechen";

    private static Page CurrentPage =>
        Shell.Current?.CurrentPage
        ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No page available to show a dialog.");

    public Task ShowErrorAsync(string message) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlertAsync("Hinweis", message, "OK"));

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = Cancel) =>
        MainThread.InvokeOnMainThreadAsync(() => CurrentPage.DisplayAlertAsync(title, message, accept, cancel));

    public Task<string?> PromptAsync(string title, string message, string? initialValue = null, int maxLength = -1) =>
        MainThread.InvokeOnMainThreadAsync(() =>
            CurrentPage.DisplayPromptAsync(title, message, "OK", Cancel, maxLength: maxLength, initialValue: initialValue ?? string.Empty));

    public async Task<string?> ChooseAsync(string title, string? destructive, params string[] options)
    {
        var choice = await MainThread.InvokeOnMainThreadAsync(() =>
            CurrentPage.DisplayActionSheetAsync(title, Cancel, destructive, options));
        return choice is null or Cancel ? null : choice;
    }

    public Task ToastAsync(string message) =>
        MainThread.InvokeOnMainThreadAsync(() => Toast.Make(message, ToastDuration.Short).Show());
}
