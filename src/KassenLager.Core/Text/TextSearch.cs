namespace KassenLager.Core.Text;

/// <summary>
/// Free-text matching for search boxes: every word of the query must occur
/// (case-insensitive, partial) in at least one of the fields.
/// </summary>
public static class TextSearch
{
    public static string[] Tokenize(string? text) =>
        text?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    public static bool MatchesAll(IReadOnlyCollection<string> tokens, params string?[] fields) =>
        tokens.All(token => fields.Any(field => field?.Contains(token, StringComparison.CurrentCultureIgnoreCase) == true));

    public static bool Matches(string? text, params string?[] fields) => MatchesAll(Tokenize(text), fields);
}
