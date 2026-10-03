using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Articles;

public sealed partial class ArticleEditViewModel(
    ArticleService articles,
    CategoryService categories,
    UnitService units,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<ArticleEditViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private const string DefaultUnitName = "Stück";

    private int? _id;
    private bool _isLoaded;

    public int ArticleNumberMaxLength => Article.ArticleNumberMaxLength;

    public int NameMaxLength => Article.NameMaxLength;

    public int ManufacturerMaxLength => Article.ManufacturerMaxLength;

    public int ModelMaxLength => Article.ModelMaxLength;

    public int EanMaxLength => Article.EanMaxLength;

    public int NoteMaxLength => Article.NoteMaxLength;

    public bool IsExisting => _id is not null;

    [ObservableProperty]
    public partial string? Title { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CategorySummary>? Categories { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelLabel), nameof(TrackingHint))]
    public partial CategorySummary? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<UnitSummary>? Units { get; set; }

    [ObservableProperty]
    public partial UnitSummary? SelectedUnit { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? Manufacturer { get; set; }

    [ObservableProperty]
    public partial string? Model { get; set; }

    [ObservableProperty]
    public partial string? ArticleNumber { get; set; }

    [ObservableProperty]
    public partial string? Ean { get; set; }

    [ObservableProperty]
    public partial string? Note { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    public string ModelLabel => SelectedCategory?.TrackingType == TrackingType.Serial ? "Modell *" : "Modell";

    public string? TrackingHint => SelectedCategory?.TrackingType switch
    {
        TrackingType.Serial => "Seriennummern-geführt: jedes Gerät wird einzeln mit Seriennummer erfasst.",
        TrackingType.Quantity => "Mengen-geführt: nur die Stückzahl wird erfasst.",
        _ => null,
    };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _id = ReadId(query);
        OnPropertyChanged(nameof(IsExisting));
    }

    public Task LoadAsync() => _isLoaded ? Task.CompletedTask : RunAsync(async () =>
    {
        var allCategories = await categories.GetAllAsync();
        Units = await units.GetAllAsync();

        if (_id is null)
        {
            Title = "Neuer Artikel";
            Categories = [.. allCategories.Where(c => c.IsActive)];
            SelectedUnit = Units.FirstOrDefault(u => u.Name == DefaultUnitName) ?? Units.FirstOrDefault();
            IsActive = true;
        }
        else
        {
            var article = await articles.GetAsync(_id.Value);
            Title = "Artikel bearbeiten";

            // An inactive category stays selectable for articles that already use it.
            Categories = [.. allCategories.Where(c => c.IsActive || c.Id == article.CategoryId)];
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == article.CategoryId);
            SelectedUnit = Units.FirstOrDefault(u => u.Id == article.UnitId);
            Name = article.Name;
            Manufacturer = article.Manufacturer;
            Model = article.Model;
            ArticleNumber = article.ArticleNumber;
            Ean = article.Ean;
            Note = article.Note;
            IsActive = article.IsActive;
        }

        _isLoaded = true;
    });

    [RelayCommand]
    private async Task SaveAsync()
    {
        ArticleListItem? duplicate = null;
        if (!await RunAsync(async () => duplicate = await articles.FindDuplicateModelAsync(Manufacturer, Model, _id)))
        {
            return;
        }

        if (duplicate is not null
            && !await Dialogs.ConfirmAsync(
                "Möglicher Doppeleintrag",
                $"Der Artikel „{duplicate.Name}“ hat bereits Hersteller und Modell „{duplicate.ManufacturerAndModel}“. Trotzdem speichern?",
                "Speichern"))
        {
            return;
        }

        var input = new ArticleInput(ArticleNumber, Name, SelectedCategory?.Id, Manufacturer, Model, Ean, SelectedUnit?.Id, Note, IsActive);
        if (await RunAsync(() => articles.SaveAsync(_id, input)))
        {
            await Dialogs.ToastAsync("Artikel gespeichert");
            await navigation.GoBackAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is not { } id
            || !await Dialogs.ConfirmAsync("Artikel löschen", $"„{Name}“ wirklich löschen?", "Löschen"))
        {
            return;
        }

        if (await RunAsync(() => articles.DeleteAsync(id)))
        {
            await Dialogs.ToastAsync("Artikel gelöscht");
            await navigation.GoBackAsync();
        }
    }
}
