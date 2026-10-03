namespace KassenLager.App.Services;

public interface INavigationService
{
    /// <summary>Navigates to a route; <paramref name="id"/> is passed as <see cref="Routes.IdParameter"/>.</summary>
    Task GoToAsync(string route, int? id = null);

    /// <summary>Navigates to a route with query parameters (values may be objects).</summary>
    Task GoToAsync(string route, IDictionary<string, object> parameters);

    /// <summary>Switches to a tab of the shell, e.g. <see cref="Routes.Search"/>.</summary>
    Task GoToTabAsync(string tabRoute);

    /// <summary>Pops <paramref name="levels"/> pages, e.g. 2 after deleting an entity whose detail page is below.</summary>
    Task GoBackAsync(int levels = 1);
}

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, int? id = null)
    {
        var parameters = new Dictionary<string, object>();
        if (id is not null)
        {
            parameters[Routes.IdParameter] = id.Value;
        }

        return GoToAsync(route, parameters);
    }

    // Single-use parameters: they are not applied again when navigating back to the page.
    public Task GoToAsync(string route, IDictionary<string, object> parameters) =>
        Shell.Current.GoToAsync(route, new ShellNavigationQueryParameters(parameters));

    public Task GoToTabAsync(string tabRoute) => Shell.Current.GoToAsync($"//{tabRoute}");

    public Task GoBackAsync(int levels = 1) => Shell.Current.GoToAsync(string.Join("/", Enumerable.Repeat("..", levels)));
}
