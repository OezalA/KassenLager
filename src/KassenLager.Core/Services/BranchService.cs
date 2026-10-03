using System.Linq.Expressions;
using KassenLager.Core.Abstractions;
using KassenLager.Core.Domain;
using KassenLager.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace KassenLager.Core.Services;

public sealed record BranchIssueInput(
    int? CustomerId,
    int? DeviceId,
    string? Branch,
    BranchIssueKind Kind,
    string? CustomerDeviceSerialNumber,
    string? CustomerDeviceModel,
    DateOnly? CustomerDeviceSentOn,
    string? Reference,
    string? Note,
    DateOnly? Date);

/// <summary>
/// A return from a branch. The device is identified by <paramref name="DeviceId"/> or by its serial
/// number; <paramref name="ArticleId"/> narrows the lookup and is required to create an unknown device.
/// </summary>
public sealed record BranchReturnInput(
    int? CustomerId,
    string? SerialNumber,
    int? DeviceId,
    int? ArticleId,
    string? Branch,
    DeviceState State,
    string? Reference,
    string? Note,
    DateOnly? Date);

public sealed record BranchReturnResult(int MovementId, int DeviceId, bool IsNewDevice);

public sealed record BranchIssueListItem(
    int Id,
    int MovementId,
    DateTime OccurredAt,
    int CustomerId,
    string CustomerName,
    string? Branch,
    BranchIssueKind Kind,
    int DeviceId,
    string DeviceSerialNumber,
    string ArticleName,
    string? Manufacturer,
    string? Model,
    string? CustomerDeviceSerialNumber,
    string? CustomerDeviceModel,
    DateOnly? CustomerDeviceSentOn,
    string? Reference,
    string? Note,
    DateTime? ReturnedAt,
    string? ReturnBranch,
    bool IsReversed)
{
    public DateTime LocalTime => AppTime.ToLocal(OccurredAt);

    public DateTime? ReturnedLocalTime => ReturnedAt is { } returned ? AppTime.ToLocal(returned) : null;

    public string KindName => Labels.Of(Kind);

    public bool IsReturned => ReturnedAt is not null;

    public string DeviceText => $"{Labels.ManufacturerAndModel(Manufacturer, Model)} · SN {DeviceSerialNumber}";

    public string? CustomerDeviceText => CustomerDeviceSerialNumber is null && CustomerDeviceModel is null
        ? null
        : string.Join(" · ", new[] { CustomerDeviceModel, CustomerDeviceSerialNumber is null ? null : $"SN {CustomerDeviceSerialNumber}" }
            .Where(s => s is not null));

    /// <summary>Searchable by branch, both serial numbers, ticket and model.</summary>
    public bool Matches(string? text) =>
        TextSearch.Matches(text, Branch, DeviceSerialNumber, CustomerDeviceSerialNumber, Reference, Model, ArticleName, CustomerDeviceModel);
}

/// <summary>Issues to branches (Ausgabe an Filiale) and returns from branches (Rücknahme aus Filiale).</summary>
public sealed class BranchService(IAppDbContextFactory dbFactory, TimeProvider clock)
{
    private const int SuggestionSourceLimit = 500;

    private static readonly Expression<Func<BranchIssue, BranchIssueListItem>> ToListItem = b => new BranchIssueListItem(
        b.Id,
        b.MovementId,
        b.Movement!.OccurredAt,
        b.Movement.CustomerId,
        b.Movement.Customer!.Name,
        b.Movement.Branch,
        b.Kind,
        b.Movement.DeviceId!.Value,
        b.Movement.Device!.SerialNumber,
        b.Movement.Article!.Name,
        b.Movement.Article.Manufacturer,
        b.Movement.Article.Model,
        b.CustomerDeviceSerialNumber,
        b.CustomerDeviceModel,
        b.CustomerDeviceSentOn,
        b.Movement.Reference,
        b.Movement.Note,
        (DateTime?)b.ReturnMovement!.OccurredAt,
        b.ReturnMovement!.Branch,
        b.Movement.ReversedBy != null);

    /// <summary>
    /// Leaves an available device at a branch. It leaves the stock immediately; there is no
    /// open tracking, the record is kept for the history only.
    /// </summary>
    public async Task<int> IssueAsync(BranchIssueInput input, CancellationToken ct = default)
    {
        var branch = InputGuard.Required(input.Branch, "Filiale", Movement.BranchMaxLength);
        var customerSerial = InputGuard.Optional(
            input.CustomerDeviceSerialNumber, "Seriennummer Kundengerät", BranchIssue.CustomerDeviceSerialNumberMaxLength);
        var customerModel = InputGuard.Optional(input.CustomerDeviceModel, "Modell Kundengerät", BranchIssue.CustomerDeviceModelMaxLength);
        var reference = InputGuard.Optional(input.Reference, "Ticketnummer", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);

        if (input.Kind == BranchIssueKind.Loan && customerSerial is null)
        {
            throw new BusinessRuleException(Messages.CustomerDeviceSerialRequired);
        }

        EnsureNotInFuture(input.CustomerDeviceSentOn);
        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var device = await Ledger.LoadDeviceAsync(db, input.DeviceId, customer, ct);
        if (device.State == DeviceState.Defective)
        {
            throw new BusinessRuleException(Messages.Format(Messages.DeviceDefectiveCannotBeIssued, device.SerialNumber));
        }

        if (!device.State.IsAvailable())
        {
            throw new BusinessRuleException(Messages.Format(Messages.DeviceNotInStock, device.SerialNumber, Labels.Of(device.State)));
        }

        var movement = Ledger.AddDeviceMovement(
            db, device, MovementType.BranchIssue, device.State, DeviceState.Issued, time, branch, reference, note);
        movement.BranchIssue = new BranchIssue
        {
            Kind = input.Kind,
            CustomerDeviceSerialNumber = customerSerial,
            CustomerDeviceModel = customerModel,
            CustomerDeviceSentOn = input.CustomerDeviceSentOn,
        };

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return movement.Id;
    }

    /// <summary>
    /// Takes a device back from a branch. A known device is reactivated with its history and its
    /// open issue is marked as returned; an unknown serial number creates a new device of the
    /// given article (e.g. a loan unit a colleague left there).
    /// </summary>
    public async Task<BranchReturnResult> ReturnAsync(BranchReturnInput input, CancellationToken ct = default)
    {
        if (input.State is not (DeviceState.UsedWorking or DeviceState.Defective))
        {
            throw new BusinessRuleException(Messages.Format(Messages.StateNotAllowed, Labels.Of(input.State)));
        }

        var branch = InputGuard.Required(input.Branch, "Filiale", Movement.BranchMaxLength);
        var serialNumber = InputGuard.Optional(input.SerialNumber, "Seriennummer", Device.SerialNumberMaxLength);
        var reference = InputGuard.Optional(input.Reference, "Ticketnummer", Movement.ReferenceMaxLength);
        var note = InputGuard.Optional(input.Note, "Notiz", Movement.NoteMaxLength);
        if (input.DeviceId is null && serialNumber is null)
        {
            throw new BusinessRuleException(Messages.Format(Messages.Required, "Seriennummer"));
        }

        var time = Ledger.ResolveTime(clock, input.Date);

        await using var db = dbFactory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var customer = await Ledger.LoadCustomerAsync(db, input.CustomerId, ct);
        var device = await FindDeviceForReturnAsync(db, input.DeviceId, serialNumber, input.ArticleId, ct);

        Movement movement;
        if (device is null)
        {
            var article = await Ledger.LoadArticleAsync(db, input.ArticleId, TrackingType.Serial, ct);
            movement = Ledger.AddNewDevice(
                db, article, customer, serialNumber!, input.State, MovementType.BranchReturn, time, branch, reference, note);
        }
        else
        {
            if (device.State.IsInStore())
            {
                throw new BusinessRuleException(Messages.Format(
                    Messages.DeviceAlreadyInStock, device.SerialNumber, device.Customer!.Name, Labels.Of(device.State)));
            }

            Ledger.EnsureBookable(device, customer);
            var wasIssued = device.State == DeviceState.Issued;
            movement = Ledger.AddDeviceMovement(
                db, device, MovementType.BranchReturn, device.State, input.State, time, branch, reference, note);

            if (wasIssued)
            {
                var openIssue = await db.BranchIssues
                    .Where(b => b.Movement!.DeviceId == device.Id && b.ReturnMovementId == null && b.Movement.ReversedBy == null)
                    .OrderByDescending(b => b.MovementId)
                    .FirstOrDefaultAsync(ct);
                openIssue?.ReturnMovement = movement;
            }
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new BranchReturnResult(movement.Id, movement.Device!.Id, device is null);
    }

    /// <summary>Branch names used before for the customer, most recent first (for autocompletion).</summary>
    public async Task<IReadOnlyList<string>> GetBranchSuggestionsAsync(int customerId, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var names = await db.Movements.AsNoTracking()
            .Where(m => m.CustomerId == customerId && m.Branch != null)
            .OrderByDescending(m => m.Id)
            .Select(m => m.Branch!)
            .Take(SuggestionSourceLimit)
            .ToListAsync(ct);

        return [.. names.DistinctBy(TextKey.From)];
    }

    /// <summary>Issues to branches, newest first; reversed issues are left out.</summary>
    public async Task<IReadOnlyList<BranchIssueListItem>> GetListAsync(int? customerId = null, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        var query = db.BranchIssues.AsNoTracking().Where(b => b.Movement!.ReversedBy == null);
        if (customerId is not null)
        {
            query = query.Where(b => b.Movement!.CustomerId == customerId);
        }

        return await query
            .OrderByDescending(b => b.Movement!.OccurredAt)
            .ThenByDescending(b => b.Id)
            .Select(ToListItem)
            .ToListAsync(ct);
    }

    public async Task<BranchIssueListItem> GetAsync(int id, CancellationToken ct = default)
    {
        await using var db = dbFactory.CreateDbContext();
        return await db.BranchIssues.AsNoTracking().Where(b => b.Id == id).Select(ToListItem).FirstOrDefaultAsync(ct)
            ?? throw new EntityNotFoundException();
    }

    /// <summary>The date the customer's device went to headquarters is often known only later.</summary>
    public async Task SetCustomerDeviceSentOnAsync(int id, DateOnly? sentOn, CancellationToken ct = default)
    {
        EnsureNotInFuture(sentOn);

        await using var db = dbFactory.CreateDbContext();
        var issue = await db.BranchIssues.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new EntityNotFoundException();
        issue.CustomerDeviceSentOn = sentOn;
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Device?> FindDeviceForReturnAsync(
        IAppDbContext db, int? deviceId, string? serialNumber, int? articleId, CancellationToken ct)
    {
        var devices = db.Devices.Include(d => d.Customer);
        if (deviceId is not null)
        {
            return await devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct) ?? throw new EntityNotFoundException();
        }

        var key = TextKey.From(serialNumber);
        var candidates = await devices
            .Where(d => d.SerialNumberKey == key && !d.IsVoided && (articleId == null || d.ArticleId == articleId))
            .ToListAsync(ct);

        return candidates.Count switch
        {
            0 when articleId is null => throw new BusinessRuleException(Messages.Format(Messages.SerialNumberUnknown, serialNumber)),
            0 => null,
            1 => candidates[0],
            _ => throw new BusinessRuleException(Messages.Format(Messages.SerialNumberAmbiguous, serialNumber)),
        };
    }

    private void EnsureNotInFuture(DateOnly? date)
    {
        if (date > AppTime.Today(clock))
        {
            throw new BusinessRuleException(Messages.DateInFuture);
        }
    }
}
