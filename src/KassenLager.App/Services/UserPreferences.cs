namespace KassenLager.App.Services;

/// <summary>Per-device conveniences (not part of the data backup): the customers chosen last.</summary>
public sealed class UserPreferences(IPreferences preferences)
{
    private const string BookingCustomerKey = "booking.customerId";
    private const string SearchCustomerKey = "search.customerId";

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
