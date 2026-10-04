namespace KassenLager.App.Services;

/// <summary>Per-device settings (not part of the data backup): the customers chosen last, the last backup date.</summary>
public sealed class UserPreferences(IPreferences preferences)
{
    private const string BookingCustomerKey = "booking.customerId";
    private const string SearchCustomerKey = "search.customerId";
    private const string LastBackupKey = "backup.lastAt";

    /// <summary>Customer preselected in booking forms.</summary>
    public int? BookingCustomerId
    {
        get => Read(BookingCustomerKey);
        set => Write(BookingCustomerKey, value);
    }

    /// <summary>Customer filter of the search; <c>null</c> = all customers.</summary>
    public int? SearchCustomerId
    {
        get => Read(SearchCustomerKey);
        set => Write(SearchCustomerKey, value);
    }

    /// <summary>When the last full backup was created (UTC); drives the 30-day reminder.</summary>
    public DateTime? LastBackupAt
    {
        get
        {
            var ticks = preferences.Get(LastBackupKey, 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }

        set => preferences.Set(LastBackupKey, value?.ToUniversalTime().Ticks ?? 0L);
    }

    private int? Read(string key)
    {
        var value = preferences.Get(key, 0);
        return value == 0 ? null : value;
    }

    private void Write(string key, int? value)
    {
        if (value is null)
        {
            preferences.Remove(key);
        }
        else
        {
            preferences.Set(key, value.Value);
        }
    }
}
