using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Categories;

public sealed partial class CategoryEditViewModel(
    CategoryService categories,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<CategoryEditViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private int? _id;
    private bool _isLoaded;

    public IReadOnlyList<TrackingTypeChoice> TrackingTypeChoices => TrackingTypeText.Choices;

    public int NameMaxLength => Category.NameMaxLength;

    public bool IsExisting => _id is not null;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial TrackingTypeChoice? SelectedTrackingType { get; set; }

    [ObservableProperty]
    public partial string? SortOrderText { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChangeTrackingType), nameof(TrackingTypeHint))]
    public partial int ArticleCount { get; set; }

    public bool CanChangeTrackingType => ArticleCount == 0;

    public string TrackingTypeHint => CanChangeTrackingType
        ? "Seriennummer: jedes Gerät wird einzeln erfasst. Menge: nur die Stückzahl."
        : $"Nicht änderbar, da bereits {ArticleCount} Artikel zugeordnet sind.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _id = ReadId(query);
        OnPropertyChanged(nameof(IsExisting));
    }

    public Task LoadAsync() => _isLoaded ? Task.CompletedTask : RunAsync(async () =>
    {
        if (_id is null)
        {
            Title = "Neue Kategorie";
            SelectedTrackingType = TrackingTypeChoices[0];
            IsActive = true;
            var existing = await categories.GetAllAsync();
            SortOrderText = ((existing.Count == 0 ? 0 : existing.Max(c => c.SortOrder)) + 10).ToString(CultureInfo.CurrentCulture);
        }
        else
        {
            var category = await categories.GetAsync(_id.Value);
            Title = "Kategorie bearbeiten";
            Name = category.Name;
            SelectedTrackingType = TrackingTypeChoices.First(c => c.Value == category.TrackingType);
            SortOrderText = category.SortOrder.ToString(CultureInfo.CurrentCulture);
            IsActive = category.IsActive;
            ArticleCount = category.ArticleCount;
        }

        _isLoaded = true;
    });

    [RelayCommand]
    private async Task SaveAsync()
    {
        var saved = await RunAsync(() =>
        {
            if (!int.TryParse(SortOrderText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var sortOrder))
            {
                throw new BusinessRuleException("Die Sortierung muss eine ganze Zahl sein.");
            }

            var trackingType = SelectedTrackingType?.Value ?? TrackingType.Quantity;
            return categories.SaveAsync(_id, new CategoryInput(Name, trackingType, sortOrder, IsActive));
        });

        if (saved)
        {
            await Dialogs.ToastAsync("Kategorie gespeichert");
            await navigation.GoBackAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is not { } id
            || !await Dialogs.ConfirmAsync("Kategorie löschen", $"„{Name}“ wirklich löschen?", "Löschen"))
        {
            return;
        }

        if (await RunAsync(() => categories.DeleteAsync(id)))
        {
            await Dialogs.ToastAsync("Kategorie gelöscht");
            await navigation.GoBackAsync();
        }
    }
}
