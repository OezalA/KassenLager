using KassenLager.Core.Text;

namespace KassenLager.Core.Services;

/// <summary>Normalizes and validates form input; throws <see cref="BusinessRuleException"/> with the field label.</summary>
internal static class InputGuard
{
    public static string Required(string? value, string label, int maxLength) =>
        Optional(value, label, maxLength)
        ?? throw new BusinessRuleException(Messages.Format(Messages.Required, label));

    public static string? Optional(string? value, string label, int maxLength)
    {
        var cleaned = TextKey.Clean(value);
        if (cleaned is not null && cleaned.Length > maxLength)
        {
            throw new BusinessRuleException(Messages.Format(Messages.TooLong, label, maxLength));
        }

        return cleaned;
    }
}
