namespace KassenLager.Core;

/// <summary>User-facing messages of the business rules. The UI is German-only.</summary>
public static class Messages
{
    public const string NotFound = "Der Datensatz wurde nicht gefunden. Möglicherweise wurde er inzwischen gelöscht.";
    public const string UnexpectedError = "Es ist ein unerwarteter Fehler aufgetreten. Die Details wurden im Protokoll gespeichert.";
    public const string Required = "Bitte „{0}“ ausfüllen.";
    public const string TooLong = "„{0}“ darf höchstens {1} Zeichen lang sein.";

    public const string CustomerNameExists = "Ein Kunde mit dem Namen „{0}“ ist bereits vorhanden.";

    public const string CategoryNameExists = "Eine Kategorie mit dem Namen „{0}“ ist bereits vorhanden.";
    public const string CategoryTrackingTypeLocked = "Die Erfassungsart kann nicht geändert werden, weil der Kategorie bereits {0} Artikel zugeordnet sind.";
    public const string CategoryInUse = "Die Kategorie kann nicht gelöscht werden, weil ihr noch {0} Artikel zugeordnet sind. Sie kann stattdessen deaktiviert werden.";

    public const string UnitNameExists = "Die Einheit „{0}“ ist bereits vorhanden.";
    public const string UnitInUse = "Die Einheit kann nicht gelöscht werden, weil sie noch von {0} Artikel(n) verwendet wird.";

    public const string ArticleCategoryRequired = "Bitte eine Kategorie auswählen.";
    public const string ArticleUnitRequired = "Bitte eine Einheit auswählen.";
    public const string ArticleModelRequired = "Für Artikel mit Seriennummernerfassung ist das Modell ein Pflichtfeld.";
    public const string ArticleNumberExists = "Die Artikelnummer „{0}“ ist bereits dem Artikel „{1}“ zugeordnet.";

    public static string Format(string template, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), template, args);
}
