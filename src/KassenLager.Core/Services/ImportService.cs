using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Excel;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public enum ImportRowStatus
{
    New = 1,
    Updated = 2,
    Unchanged = 3,
    Error = 4,
}

public sealed record ImportRowResult(string Sheet, int RowNumber, ImportRowStatus Status, string Title, string? Message)
{
    public string StatusName => Status switch
    {
        ImportRowStatus.New => "neu",
        ImportRowStatus.Updated => "wird aktualisiert",
        ImportRowStatus.Unchanged => "unverändert",
        _ => "fehlerhaft",
    };

    public bool IsError => Status == ImportRowStatus.Error;

    public string Location => $"{Sheet}, Zeile {RowNumber}";
}

public sealed record ImportResult(IReadOnlyList<ImportRowResult> Rows)
{
    public int NewCount => Rows.Count(r => r.Status == ImportRowStatus.New);

    public int UpdatedCount => Rows.Count(r => r.Status == ImportRowStatus.Updated);

    public int UnchangedCount => Rows.Count(r => r.Status == ImportRowStatus.Unchanged);

    public int ErrorCount => Rows.Count(r => r.Status == ImportRowStatus.Error);

    public bool HasErrors => ErrorCount > 0;

    public bool HasChanges => NewCount + UpdatedCount > 0;
}

public sealed record ImportLogItem(DateTime ImportedAt, string FileName, ImportMode Mode, int NewRows, int UpdatedRows, int UnchangedRows, int ErrorRows)
{
    public DateTime LocalTime => AppTime.ToLocal(ImportedAt);

    public string ModeName => Mode == ImportMode.Set ? "Bestand setzen" : "Bestand addieren";

    public string CountsText => $"{NewRows} neu · {UpdatedRows} aktualisiert · {UnchangedRows} unverändert · {ErrorRows} fehlerhaft";
}

/// <summary>
/// Excel import (sheets Artikel, Bestand, Geräte). The preview runs the complete import in a
/// transaction that is rolled back, so it shows exactly what the import will do; the import
/// itself applies all valid rows in one transaction and skips the faulty ones.
/// </summary>
public sealed class ImportService(IAppDbContextFactory dbFactory, TimeProvider clock)
{
    public Task<ImportResult> PreviewAsync(ExcelWorkbook workbook, ImportMode mode, CancellationToken ct = default) =>
        RunAsync(workbook, mode, null, ct);

    public Task<ImportResult> ImportAsync(ExcelWorkbook workbook, ImportMode mode, string fileName, CancellationToken ct = default) =>
        RunAsync(workbook, mode, string.IsNullOrWhiteSpace(fileName) ? "Import.xlsx" : fileName.Trim(), ct);

    public async Task<IReadOnlyList<ImportLogItem>> GetLogAsync(int take = 20, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.ImportLogs.AsNoTracking()
            .OrderByDescending(l => l.Id)
            .Take(take)
            .Select(l => new ImportLogItem(l.ImportedAt, l.FileName, l.Mode, l.NewRows, l.UpdatedRows, l.UnchangedRows, l.ErrorRows))
            .ToListAsync(ct);
    }

    /// <param name="fileName"><c>null</c> = preview (rolled back).</param>
    private async Task<ImportResult> RunAsync(ExcelWorkbook workbook, ImportMode mode, string? fileName, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var run = new ImportRun(db, mode, new BookingTime(now, now), fileName);
        await run.LoadAsync(ct);

        var rows = new List<ImportRowResult>();
        foreach (var row in workbook.Articles.Rows)
        {
            rows.Add(await run.ImportArticleAsync(workbook.Articles.Name, row, ct));
        }

        // New articles need their ids before stock and devices refer to them.
        await db.SaveChangesAsync(ct);

        foreach (var row in workbook.Stock.Rows)
        {
            rows.Add(await run.ImportStockAsync(workbook.Stock.Name, row, ct));
        }

        foreach (var row in workbook.Devices.Rows)
        {
            rows.Add(await run.ImportDeviceAsync(workbook.Devices.Name, row, ct));
        }

        var result = new ImportResult(rows);
        if (fileName is null)
        {
            return result; // Disposing the transaction rolls the preview back.
        }

        db.ImportLogs.Add(new ImportLog
        {
            ImportedAt = now,
            FileName = fileName.Length > ImportLog.FileNameMaxLength ? fileName[..ImportLog.FileNameMaxLength] : fileName,
            Mode = mode,
            NewRows = result.NewCount,
            UpdatedRows = result.UpdatedCount,
            UnchangedRows = result.UnchangedCount,
            ErrorRows = result.ErrorCount,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    /// <summary>State of one import run. Every row is validated completely before it changes anything.</summary>
    private sealed class ImportRun(IAppDbContext db, ImportMode mode, BookingTime time, string? fileName)
    {
        private const string MovementNote = "Excel-Import";
        private const string DefaultUnitName = "Stück";

        private readonly Dictionary<Article, int> _seenArticles = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<(int ArticleId, int CustomerId), int> _seenStock = [];
        private readonly Dictionary<(int ArticleId, string SerialKey), int> _seenDevices = [];
        private readonly string? _reference = fileName is null ? null : Truncate($"Import {fileName}", Movement.ReferenceMaxLength);

        private List<Customer> _customers = [];
        private List<Category> _categories = [];
        private List<Unit> _units = [];
        private List<Article> _articles = [];
        private List<MinimumStock> _minimums = [];

        public async Task LoadAsync(CancellationToken ct)
        {
            _customers = await db.Customers.ToListAsync(ct);
            _categories = await db.Categories.ToListAsync(ct);
            _units = await db.Units.ToListAsync(ct);
            _articles = await db.Articles.Include(a => a.Category).Include(a => a.Unit).ToListAsync(ct);
            _minimums = await db.MinimumStocks.ToListAsync(ct);
        }

        public async Task<ImportRowResult> ImportArticleAsync(string sheet, ImportRow row, CancellationToken ct)
        {
            var title = row.Text("Bezeichnung") ?? row.Text("Modell") ?? row.Text("Artikelnummer") ?? "–";
            try
            {
                var number = InputGuard.Optional(row.Text("Artikelnummer"), "Artikelnummer", Article.ArticleNumberMaxLength);
                var name = InputGuard.Optional(row.Text("Bezeichnung"), "Bezeichnung", Article.NameMaxLength);
                var manufacturer = InputGuard.Optional(row.Text("Hersteller"), "Hersteller", Article.ManufacturerMaxLength);
                var model = InputGuard.Optional(row.Text("Modell"), "Modell", Article.ModelMaxLength);
                var ean = InputGuard.Optional(row.Text("EAN"), "EAN", Article.EanMaxLength);
                var note = InputGuard.Optional(row.Text("Notiz"), "Notiz", Article.NoteMaxLength);
                var category = FindByName(_categories, row.Text("Kategorie"), c => c.Name, Messages.ImportUnknownCategory);
                var unit = FindByName(_units, row.Text("Einheit"), u => u.Name, Messages.ImportUnknownUnit);

                var article = FindArticle(number, manufacturer, model, name, category?.Name, notFoundIsNew: true);
                if (article is null)
                {
                    var newArticle = CreateArticle(number, name, manufacturer, model, ean, note, category, unit);
                    _seenArticles[newArticle] = row.RowNumber;
                    return Result(sheet, row, ImportRowStatus.New, title, null);
                }

                EnsureNotSeen(_seenArticles, article);
                var changes = await UpdateArticleAsync(article, number, name, manufacturer, model, ean, note, category, unit, ct);
                _seenArticles[article] = row.RowNumber;
                return changes.Count == 0
                    ? Result(sheet, row, ImportRowStatus.Unchanged, title, null)
                    : Result(sheet, row, ImportRowStatus.Updated, title, $"Geändert: {string.Join(", ", changes)}");
            }
            catch (BusinessRuleException ex)
            {
                return Result(sheet, row, ImportRowStatus.Error, title, ex.Message);
            }
        }

        public async Task<ImportRowResult> ImportStockAsync(string sheet, ImportRow row, CancellationToken ct)
        {
            var title = row.Text("Artikelnummer") ?? row.Text("Modell") ?? row.Text("Bezeichnung") ?? "–";
            try
            {
                var article = FindArticle(row.Text("Artikelnummer"), row.Text("Hersteller"), row.Text("Modell"), row.Text("Bezeichnung"), row.Text("Kategorie"), notFoundIsNew: false)!;
                title = $"{article.Name} · {row.Text("Kunde") ?? "–"}";
                if (article.Category!.TrackingType == TrackingType.Serial)
                {
                    throw new BusinessRuleException(Messages.Format(Messages.ImportSerialArticleInStockSheet, article.Name));
                }

                var customer = FindCustomer(row.Text("Kunde"));
                var quantity = row.Integer("Menge");
                var minimum = row.Integer("Mindestbestand");
                if (quantity is null && minimum is null)
                {
                    throw new BusinessRuleException(Messages.ImportNothingToImport);
                }

                if (quantity < 0)
                {
                    throw new BusinessRuleException(Messages.ImportNegativeQuantity);
                }

                if (quantity > Ledger.MaxQuantity)
                {
                    throw new BusinessRuleException(Messages.Format(Messages.QuantityOutOfRange, Ledger.MaxQuantity));
                }

                if (minimum is < 0 or > StockService.MaxMinimum)
                {
                    throw new BusinessRuleException(Messages.Format(Messages.MinimumOutOfRange, StockService.MaxMinimum));
                }

                EnsureNotSeen(_seenStock, (article.Id, customer.Id));
                _seenStock[(article.Id, customer.Id)] = row.RowNumber;

                var current = await Ledger.GetQuantityAsync(db, article.Id, customer.Id, ct);
                var existingMinimum = _minimums.FirstOrDefault(m => m.ArticleId == article.Id && m.CustomerId == customer.Id);
                var changes = new List<string>();

                if (quantity is { } target)
                {
                    var change = mode == ImportMode.Set ? target - current : target;
                    if (change != 0)
                    {
                        db.Movements.Add(new Movement
                        {
                            Type = mode == ImportMode.Set ? MovementType.ImportCorrection : MovementType.GoodsReceipt,
                            OccurredAt = time.OccurredAt,
                            RecordedAt = time.RecordedAt,
                            CustomerId = customer.Id,
                            ArticleId = article.Id,
                            QuantityChange = change,
                            Reference = _reference,
                            Note = MovementNote,
                        });
                        changes.Add($"Bestand {current} → {current + change} {article.Unit!.Name}");
                    }
                }

                if (minimum is { } newMinimum && newMinimum != (existingMinimum?.Quantity ?? 0))
                {
                    if (newMinimum == 0)
                    {
                        db.MinimumStocks.Remove(existingMinimum!);
                        _minimums.Remove(existingMinimum!);
                    }
                    else if (existingMinimum is null)
                    {
                        var added = new MinimumStock { ArticleId = article.Id, CustomerId = customer.Id, Quantity = newMinimum };
                        db.MinimumStocks.Add(added);
                        _minimums.Add(added);
                    }
                    else
                    {
                        existingMinimum.Quantity = newMinimum;
                    }

                    changes.Add($"Mindestbestand {newMinimum}");
                }

                if (changes.Count == 0)
                {
                    return Result(sheet, row, ImportRowStatus.Unchanged, title, null);
                }

                var status = current == 0 && existingMinimum is null ? ImportRowStatus.New : ImportRowStatus.Updated;
                return Result(sheet, row, status, title, string.Join(" · ", changes));
            }
            catch (BusinessRuleException ex)
            {
                return Result(sheet, row, ImportRowStatus.Error, title, ex.Message);
            }
        }

        public async Task<ImportRowResult> ImportDeviceAsync(string sheet, ImportRow row, CancellationToken ct)
        {
            var title = row.Text("Seriennummer") is { } serial ? $"SN {serial}" : "–";
            try
            {
                var article = FindArticle(row.Text("Artikelnummer"), row.Text("Hersteller"), row.Text("Modell"), row.Text("Bezeichnung"), row.Text("Kategorie"), notFoundIsNew: false)!;
                if (article.Category!.TrackingType != TrackingType.Serial)
                {
                    throw new BusinessRuleException(Messages.Format(Messages.ImportQuantityArticleInDeviceSheet, article.Name));
                }

                var serialNumber = InputGuard.Required(row.Text("Seriennummer"), "Seriennummer", Device.SerialNumberMaxLength);
                title = $"SN {serialNumber} · {Labels.ManufacturerAndModel(article.Manufacturer, article.Model)}";
                var customer = FindCustomer(row.Text("Kunde"));
                var state = ParseState(row.Text("Zustand"));
                var note = InputGuard.Optional(row.Text("Notiz"), "Notiz", Device.NoteMaxLength);
                var key = TextKey.From(serialNumber)!;
                EnsureNotSeen(_seenDevices, (article.Id, key));

                var device = await db.Devices.Include(d => d.Customer)
                    .FirstOrDefaultAsync(d => d.ArticleId == article.Id && d.SerialNumberKey == key && !d.IsVoided, ct);
                var receiptType = mode == ImportMode.Set ? MovementType.ImportCorrection : MovementType.GoodsReceipt;

                if (device is null)
                {
                    _seenDevices[(article.Id, key)] = row.RowNumber;
                    var created = Ledger.AddNewDevice(
                        db, article, customer, serialNumber, state ?? DeviceState.New, receiptType, time, null, _reference, MovementNote).Device!;
                    created.Note = note;
                    return Result(sheet, row, ImportRowStatus.New, title, $"{Labels.Of(created.State)} · {customer.Name}");
                }

                if (device.CustomerId != customer.Id)
                {
                    throw new BusinessRuleException(Messages.Format(
                        Messages.DeviceCustomerMismatch, device.SerialNumber, device.Customer!.Name, customer.Name));
                }

                if (device.State == DeviceState.Issued)
                {
                    throw new BusinessRuleException(Messages.ImportDeviceIsIssued);
                }

                _seenDevices[(article.Id, key)] = row.RowNumber;
                var changes = new List<string>();
                if (device.State.IsInStore())
                {
                    if (state is { } newState && newState != device.State)
                    {
                        changes.Add($"Zustand {Labels.Of(device.State)} → {Labels.Of(newState)}");
                        Ledger.AddDeviceMovement(db, device, MovementType.ImportCorrection, device.State, newState, time, null, _reference, MovementNote);
                    }
                }
                else
                {
                    var newState = state ?? DeviceState.New;
                    changes.Add($"wieder im Lager: {Labels.Of(device.State)} → {Labels.Of(newState)}");
                    Ledger.AddDeviceMovement(db, device, receiptType, device.State, newState, time, null, _reference, MovementNote);
                }

                if (note is not null && note != device.Note)
                {
                    device.Note = note;
                    changes.Add("Notiz");
                }

                return changes.Count == 0
                    ? Result(sheet, row, ImportRowStatus.Unchanged, title, null)
                    : Result(sheet, row, ImportRowStatus.Updated, title, string.Join(" · ", changes));
            }
            catch (BusinessRuleException ex)
            {
                return Result(sheet, row, ImportRowStatus.Error, title, ex.Message);
            }
        }

        private Article CreateArticle(
            string? number, string? name, string? manufacturer, string? model, string? ean, string? note, Category? category, Unit? unit)
        {
            var validName = name ?? throw new BusinessRuleException(Messages.Format(Messages.Required, "Bezeichnung"));
            var validCategory = category ?? throw new BusinessRuleException(Messages.ArticleCategoryRequired);
            var validUnit = unit
                ?? _units.FirstOrDefault(u => TextKey.EqualsIgnoreCase(u.Name, DefaultUnitName))
                ?? throw new BusinessRuleException(Messages.ArticleUnitRequired);
            if (validCategory.TrackingType == TrackingType.Serial && model is null)
            {
                throw new BusinessRuleException(Messages.ArticleModelRequired);
            }

            var article = new Article
            {
                ArticleNumber = number,
                Name = validName,
                Category = validCategory,
                CategoryId = validCategory.Id,
                Manufacturer = manufacturer,
                Model = model,
                Ean = ean,
                Unit = validUnit,
                UnitId = validUnit.Id,
                Note = note,
                IsActive = true,
            };
            db.Articles.Add(article);
            _articles.Add(article);
            return article;
        }

        /// <summary>Empty cells keep the stored value; returns the names of the changed fields.</summary>
        private async Task<List<string>> UpdateArticleAsync(
            Article article, string? number, string? name, string? manufacturer, string? model, string? ean, string? note,
            Category? category, Unit? unit, CancellationToken ct)
        {
            var newCategory = category ?? article.Category!;
            var newModel = model ?? article.Model;
            if (newCategory.Id != article.CategoryId && newCategory.TrackingType != article.Category!.TrackingType
                && article.Id != 0
                && (await db.Movements.AnyAsync(m => m.ArticleId == article.Id, ct) || await db.Devices.AnyAsync(d => d.ArticleId == article.Id, ct)))
            {
                throw new BusinessRuleException(Messages.ImportCategoryChangeLocked);
            }

            if (newCategory.TrackingType == TrackingType.Serial && newModel is null)
            {
                throw new BusinessRuleException(Messages.ArticleModelRequired);
            }

            var numberChanged = number is not null && TextKey.From(number) != TextKey.From(article.ArticleNumber);
            if (numberChanged && _articles.FirstOrDefault(a => a != article && TextKey.EqualsIgnoreCase(a.ArticleNumber, number)) is { } owner)
            {
                throw new BusinessRuleException(Messages.Format(Messages.ArticleNumberExists, number, owner.Name));
            }

            var changes = new List<string>();
            if (numberChanged)
            {
                article.ArticleNumber = number;
                changes.Add("Artikelnummer");
            }

            Apply(name, article.Name, v => article.Name = v, "Bezeichnung");
            Apply(manufacturer, article.Manufacturer, v => article.Manufacturer = v, "Hersteller");
            Apply(model, article.Model, v => article.Model = v, "Modell");
            Apply(ean, article.Ean, v => article.Ean = v, "EAN");
            Apply(note, article.Note, v => article.Note = v, "Notiz");

            if (newCategory.Id != article.CategoryId)
            {
                article.Category = newCategory;
                article.CategoryId = newCategory.Id;
                changes.Add("Kategorie");
            }

            if (unit is not null && unit.Id != article.UnitId)
            {
                article.Unit = unit;
                article.UnitId = unit.Id;
                changes.Add("Einheit");
            }

            return changes;

            void Apply(string? value, string? current, Action<string> set, string label)
            {
                if (value is not null && value != current)
                {
                    set(value);
                    changes.Add(label);
                }
            }
        }

        /// <summary>
        /// Matches by article number; without number by manufacturer + model; without model by
        /// name (+ category). In the Artikel sheet a missing match means "new article".
        /// </summary>
        private Article? FindArticle(string? number, string? manufacturer, string? model, string? name, string? categoryName, bool notFoundIsNew)
        {
            List<Article> matches;
            string description;
            if (TextKey.From(number) is { } numberKey)
            {
                matches = [.. _articles.Where(a => TextKey.From(a.ArticleNumber) == numberKey)];
                description = $"Artikelnummer „{number}“";
            }
            else if (TextKey.From(model) is not null)
            {
                matches = [.. _articles.Where(a => TextKey.EqualsIgnoreCase(a.Model, model)
                    && (TextKey.From(manufacturer) is null || TextKey.EqualsIgnoreCase(a.Manufacturer, manufacturer)))];
                description = $"Modell „{Labels.ManufacturerAndModel(manufacturer, model)}“";
            }
            else if (TextKey.From(name) is not null)
            {
                matches = [.. _articles.Where(a => TextKey.EqualsIgnoreCase(a.Name, name)
                    && (TextKey.From(categoryName) is null || TextKey.EqualsIgnoreCase(a.Category!.Name, categoryName)))];
                description = $"Bezeichnung „{name}“";
            }
            else
            {
                throw new BusinessRuleException(Messages.ImportArticleKeyMissing);
            }

            return matches.Count switch
            {
                1 => matches[0],
                0 when notFoundIsNew => null,
                0 => throw new BusinessRuleException(Messages.Format(Messages.ImportArticleNotFound, description)),
                _ => throw new BusinessRuleException(Messages.Format(Messages.ImportArticleAmbiguous, description)),
            };
        }

        private Customer FindCustomer(string? text)
        {
            if (TextKey.From(text) is null)
            {
                throw new BusinessRuleException(Messages.ImportCustomerMissing);
            }

            return _customers.FirstOrDefault(c => TextKey.EqualsIgnoreCase(c.Name, text) || TextKey.EqualsIgnoreCase(c.ShortName, text))
                ?? throw new BusinessRuleException(Messages.Format(Messages.ImportUnknownCustomer, text));
        }

        private static T? FindByName<T>(List<T> items, string? text, Func<T, string> name, string unknownMessage)
            where T : class =>
            text is null
                ? null
                : items.FirstOrDefault(i => TextKey.EqualsIgnoreCase(name(i), text))
                    ?? throw new BusinessRuleException(Messages.Format(unknownMessage, text));

        /// <summary>Accepts the German state names (also "Gebraucht" and "-" instead of "–"); only states in the store.</summary>
        internal static DeviceState? ParseState(string? text)
        {
            if (TextKey.From(text) is null)
            {
                return null;
            }

            static string Normalize(string value) => value.Replace('–', '-').Replace(" ", string.Empty).ToUpperInvariant();
            var normalized = Normalize(text!);
            DeviceState? state = normalized == "GEBRAUCHT"
                ? DeviceState.UsedWorking
                : Enum.GetValues<DeviceState>().Cast<DeviceState?>().FirstOrDefault(s => Normalize(Labels.Of(s!.Value)) == normalized);

            return state switch
            {
                null => throw new BusinessRuleException(Messages.Format(Messages.ImportUnknownState, text)),
                DeviceState.Issued => throw new BusinessRuleException(Messages.ImportIssuedNotAllowed),
                { } s when !s.IsInStore() => throw new BusinessRuleException(Messages.Format(Messages.ImportStateNotAllowed, Labels.Of(s))),
                _ => state,
            };
        }

        /// <summary>Valid rows register their key once they succeed; a faulty row does not block a later correct one.</summary>
        private static void EnsureNotSeen<TKey>(Dictionary<TKey, int> seen, TKey key)
            where TKey : notnull
        {
            if (seen.TryGetValue(key, out var firstRow))
            {
                throw new BusinessRuleException(Messages.Format(Messages.ImportDuplicateRow, firstRow));
            }
        }

        private static ImportRowResult Result(string sheet, ImportRow row, ImportRowStatus status, string title, string? message) =>
            new(sheet, row.RowNumber, status, title, message);

        private static string Truncate(string value, int maxLength) => value.Length > maxLength ? value[..maxLength] : value;
    }
}
