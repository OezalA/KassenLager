namespace KassenLager.App.Services;

public interface INavigationService
{
    /// <summary>Navigates to a route; <paramref name="id"/> is passed as <see cref="Routes.IdParameter"/>.</summary>
    Task GoToAsync(string route, int? id = null);

    Task GoBackAsync();
}

public sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, int? id = null)
    {
        var parameters = new ShellNavigationQueryParameters();
        if (id is not null)
        {
            parameters[Routes.IdParameter] = id.Value;
        }

        return Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
