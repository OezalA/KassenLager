using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KassenLager.App.Services;
using KassenLager.Core;
using KassenLager.Core.Domain;
using KassenLager.Core.Services;
using Microsoft.Extensions.Logging;

namespace KassenLager.App.ViewModels.Orders;

/// <summary>One order: edit the draft, place it, share the Excel, receive goods, cancel or close.</summary>
public sealed partial class OrderDetailViewModel(
    OrderService orders,
    IPickerService pickers,
    IFileService files,
    INavigationService navigation,
    IDialogService dialogs,
    ILogger<OrderDetailViewModel> logger)
    : ViewModelBase(dialogs, logger), ILoadable, IQueryAttributable
{
    private const string ChangeQuantity = "Menge ändern";
    private const string ChangeNote = "Notiz";
    private const string Remove = "Entfernen";

    private int? _id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote), nameof(HasReference))]
    public partial OrderDetail? Detail { get; set; }

    public bool HasNote => !string.IsNullOrEmpty(Detail?.Note);

    public bool HasReference => !string.IsNullOrEmpty(Detail?.Order.Reference);

    public void ApplyQueryAttributes(IDictionary<string, object> query) => _id = ReadId(query);

    public Task LoadAsync() => _id is not { } id ? Task.CompletedTask : RunAsync(async () => Detail = await orders.GetDetailAsync(id));

    [RelayCommand]
    private async Task AddLineAsync()
    {
        if (Detail is not { IsDraft: true } detail)
        {
            return;
        }

        var article = await pickers.PickArticleAsync(new ArticlePickerOptions("Artikel bestellen", null, detail.Order.CustomerId));
        if (article is null)
        {
            return;
        }

        var existing = detail.Lines.FirstOrDefault(l => l.ArticleId == article.Id);
        await AskQuantityAsync(article.Id, article.Name, existing?.Quantity, existing?.Note);
    }

    [RelayCommand]
    private async Task EditLineAsync(OrderLineItem line)
    {
        if (Detail is not { IsDraft: true } || _id is not { } id)
        {
            return;
        }

        var choice = await Dialogs.ChooseAsync(line.ArticleName, Remove, ChangeQuantity, ChangeNote);
        switch (choice)
        {
            case ChangeQuantity:
                await AskQuantityAsync(line.ArticleId, line.ArticleName, line.Quantity, line.Note);
                break;
            case ChangeNote:
                var note = await Dialogs.PromptAsync("Notiz zur Position", line.ArticleName, line.Note, OrderLine.NoteMaxLength);
                if (note is not null && await RunAsync(() => orders.SetLineAsync(id, line.ArticleId, line.Quantity, note)))
                {
                    await LoadAsync();
                }

                break;
            case Remove:
                if (await RunAsync(() => orders.SetLineAsync(id, line.ArticleId, 0, null)))
                {
                    await LoadAsync();
                }

                break;
        }
    }

    [RelayCommand]
    private async Task EditReferenceAsync()
    {
        if (Detail is not { CanEditHeader: true } detail || _id is not { } id)
        {
            return;
        }

        var reference = await Dialogs.PromptAsync(
            "Bestellnummer", "Bestellnummer der Zentrale (leer = keine):", detail.Order.Reference, Order.ReferenceMaxLength);
        if (reference is not null && await RunAsync(() => orders.SetHeaderAsync(id, reference, detail.Note)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task EditNoteAsync()
    {
        if (Detail is not { CanEditHeader: true } detail || _id is not { } id)
        {
            return;
        }

        var note = await Dialogs.PromptAsync("Notiz zur Bestellung", "Notiz (leer = keine):", detail.Note, Order.NoteMaxLength);
        if (note is not null && await RunAsync(() => orders.SetHeaderAsync(id, detail.Order.Reference, note)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task PlaceAsync()
    {
        if (_id is not { } id
            || !await Dialogs.ConfirmAsync(
                "Bestellen",
                "Die Bestellung wird als bestellt markiert; die Positionen können danach nicht mehr geändert werden.",
                "Bestellt"))
        {
            return;
        }

        if (await RunAsync(() => orders.PlaceAsync(id)))
        {
            await LoadAsync();
            if (await Dialogs.ConfirmAsync("Excel-Datei", "Die Bestellung jetzt als Excel-Datei speichern oder teilen?", "Ja", "Später"))
            {
                await ShareExcelAsync();
            }
        }
    }

    [RelayCommand]
    private async Task ShareExcelAsync()
    {
        if (_id is not { } id)
        {
            return;
        }

        string? path = null;
        if (await RunAsync(async () =>
            {
                var fileName = await orders.GetFileNameAsync(id);
                path = await files.CreateExportAsync(fileName, stream => orders.WriteExcelAsync(id, stream));
            }))
        {
            await RunAsync(() => files.SaveOrShareAsync(path!, Detail?.Order.Title ?? "Bestellung"));
        }
    }

    [RelayCommand]
    private Task ReceiveAsync() => _id is { } id ? navigation.GoToAsync(Routes.OrderReceipt, id) : Task.CompletedTask;

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (_id is { } id
            && await Dialogs.ConfirmAsync("Bestellung stornieren", "Die Bestellung wird storniert und nicht mehr erwartet.", "Stornieren")
            && await RunAsync(() => orders.CancelAsync(id)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (_id is { } id
            && await Dialogs.ConfirmAsync(
                "Bestellung abschließen",
                "Die noch offenen Mengen werden nicht mehr erwartet und zählen nicht mehr für den Bestellvorschlag.",
                "Abschließen")
            && await RunAsync(() => orders.CloseAsync(id)))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is { } id
            && await Dialogs.ConfirmAsync("Entwurf löschen", "Den Bestellentwurf wirklich löschen?", "Löschen")
            && await RunAsync(() => orders.DeleteDraftAsync(id)))
        {
            await Dialogs.ToastAsync("Entwurf gelöscht");
            await navigation.GoBackAsync();
        }
    }

    private async Task AskQuantityAsync(int articleId, string articleName, int? quantity, string? note)
    {
        if (_id is not { } id)
        {
            return;
        }

        var text = await Dialogs.PromptAsync("Menge", $"Bestellmenge für „{articleName}“ (0 = entfernen):", quantity?.ToString(CultureInfo.CurrentCulture), 6);
        if (text is null)
        {
            return;
        }

        var saved = await RunAsync(() => int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            ? orders.SetLineAsync(id, articleId, value, note)
            : throw new BusinessRuleException(Messages.Format(Messages.QuantityOutOfRange, 100_000)));
        if (saved)
        {
            await LoadAsync();
        }
    }
}
