using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using KassenLager.App.Services;
using KassenLager.Core;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels;

/// <summary>Implemented by view models that load data whenever their page appears.</summary>
public interface ILoadable
{
    Task LoadAsync();
}

public abstract partial class ViewModelBase(IDialogService dialogs, ILogger logger) : ObservableObject
{
    protected IDialogService Dialogs { get; } = dialogs;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Runs a UI operation: business rule violations are shown as-is, anything else
    /// is logged with details and reported with a generic message.
    /// </summary>
    /// <returns><c>true</c> if the operation completed without error.</returns>
    protected async Task<bool> RunAsync(Func<Task> operation, [CallerMemberName] string operationName = "")
    {
        IsBusy = true;
        try
        {
            await operation();
            return true;
        }
        catch (BusinessRuleException ex)
        {
            await Dialogs.ShowErrorAsync(ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{ViewModel}.{Operation} failed", GetType().Name, operationName);
            await Dialogs.ShowErrorAsync(Messages.UnexpectedError);
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected static int? ReadId(IDictionary<string, object> query) =>
        query.TryGetValue(Routes.IdParameter, out var value) && value is int id ? id : null;
}
