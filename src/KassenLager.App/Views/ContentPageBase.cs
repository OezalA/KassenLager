using KassenLager.App.ViewModels;

namespace KassenLager.App.Views;

/// <summary>Reloads the view model each time the page appears (e.g. after returning from an edit page).</summary>
public abstract class ContentPageBase : ContentPage
{
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ILoadable loadable)
        {
            // Exceptions are handled inside the view model (ViewModelBase.RunAsync).
            await loadable.LoadAsync();
        }
    }
}
