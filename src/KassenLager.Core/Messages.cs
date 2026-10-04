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
    public const string ArticleHasHistory = "Der Artikel kann nicht gelöscht werden, weil es bereits Buchungen oder Geräte dazu gibt. Er kann stattdessen deaktiviert werden.";
    public const string CustomerHasHistory = "Der Kunde kann nicht gelöscht werden, weil es bereits Buchungen oder Geräte für ihn gibt. Er kann stattdessen deaktiviert werden.";

    public const string CustomerRequired = "Bitte einen Kunden auswählen.";
    public const string ArticleRequired = "Bitte einen Artikel auswählen.";
    public const string DeviceRequired = "Bitte ein Gerät auswählen.";
    public const string DateInFuture = "Das Datum darf nicht in der Zukunft liegen.";
    public const string DateRangeInvalid = "Das Enddatum liegt vor dem Startdatum.";
    public const string QuantityOutOfRange = "Die Menge muss eine ganze Zahl zwischen 1 und {0} sein.";
    public const string ArticleIsSerialTracked = "„{0}“ wird mit Seriennummern geführt. Bitte die Geräte einzeln buchen.";
    public const string ArticleIsQuantityTracked = "„{0}“ wird nur nach Menge geführt und hat keine Seriennummern.";
    public const string InsufficientStock = "Nicht genug Bestand: Von „{0}“ sind für {1} nur {2} {3} vorhanden.";
    public const string MinimumOutOfRange = "Der Mindestbestand muss eine ganze Zahl zwischen 0 und {0} sein.";

    public const string SerialNumbersRequired = "Bitte mindestens eine Seriennummer erfassen.";
    public const string SerialNumberEnteredTwice = "Die Seriennummer „{0}“ wurde mehrfach eingegeben.";
    public const string SerialNumberAmbiguous = "Die Seriennummer „{0}“ ist bei mehreren Artikeln erfasst. Bitte den Artikel auswählen.";
    public const string SerialNumberUnknown = "Die Seriennummer „{0}“ ist noch nicht erfasst. Bitte den Artikel (Modell) auswählen, um das Gerät neu anzulegen.";
    public const string DeviceAlreadyInStock = "Das Gerät „{0}“ ist bereits im Lager (Kunde {1}, Zustand „{2}“).";
    public const string DeviceCustomerMismatch = "Das Gerät „{0}“ gehört zum Kunden {1}, nicht zu {2}. Buchungen zwischen Kunden sind nicht möglich.";
    public const string DeviceNotInStock = "Das Gerät „{0}“ ist nicht im Lager (Zustand „{1}“).";
    public const string DeviceDefectiveCannotBeIssued = "Das Gerät „{0}“ ist defekt und kann nicht ausgegeben werden.";
    public const string DeviceVoided = "Das Gerät „{0}“ wurde storniert und kann nicht mehr gebucht werden.";
    public const string DeviceStateUnchanged = "Das Gerät hat bereits den Zustand „{0}“.";
    public const string StateNotAllowed = "Der Zustand „{0}“ ist hier nicht zulässig.";
    public const string DateBeforeLastDeviceMovement = "Das Datum liegt vor der letzten Buchung dieses Geräts ({0:dd.MM.yyyy}).";
    public const string CustomerDeviceSerialRequired = "Bei einem Leihgerät bitte die Seriennummer des Kundengeräts angeben.";

    public const string ReversalOfReversal = "Eine Stornobuchung kann nicht storniert werden.";
    public const string AlreadyReversed = "Diese Buchung wurde bereits storniert.";
    public const string ReversalNotLatest = "Für dieses Gerät gibt es eine spätere Buchung. Bitte zuerst die spätere Buchung stornieren.";
    public const string ReversalStateMismatch = "Storno nicht möglich: Der Zustand des Geräts passt nicht mehr zu dieser Buchung.";
    public const string ReversalStockNegative = "Storno nicht möglich: Der Bestand von „{0}“ für {1} würde negativ (aktuell {2} {3}).";

    public const string ImportFileUnreadable = "Die Datei konnte nicht gelesen werden. Bitte eine Excel-Datei (.xlsx) wählen.";
    public const string ImportNoSheets = "Die Datei enthält keines der Blätter „Artikel“, „Bestand“ oder „Geräte“. Am einfachsten die Vorlage verwenden.";
    public const string ImportMissingColumns = "Im Blatt „{0}“ fehlen die Spalten: {1}.";
    public const string ImportNotAWholeNumber = "„{0}“ muss eine ganze Zahl sein (Wert „{1}“).";
    public const string ImportArticleKeyMissing = "Artikelnummer, Hersteller/Modell oder Bezeichnung fehlt.";
    public const string ImportArticleNotFound = "Kein passender Artikel gefunden ({0}).";
    public const string ImportArticleAmbiguous = "Mehrere Artikel passen ({0}). Bitte die Artikelnummer angeben.";
    public const string ImportUnknownCategory = "Unbekannte Kategorie „{0}“.";
    public const string ImportUnknownUnit = "Unbekannte Einheit „{0}“.";
    public const string ImportCustomerMissing = "Kunde fehlt.";
    public const string ImportUnknownCustomer = "Unbekannter Kunde „{0}“.";
    public const string ImportDuplicateRow = "Doppelt – bereits in Zeile {0}.";
    public const string ImportSerialArticleInStockSheet = "„{0}“ wird mit Seriennummern geführt – Geräte im Blatt „Geräte“ erfassen.";
    public const string ImportQuantityArticleInDeviceSheet = "„{0}“ wird nur nach Menge geführt – Bestand im Blatt „Bestand“ erfassen.";
    public const string ImportNothingToImport = "Bitte Menge oder Mindestbestand angeben.";
    public const string ImportNegativeQuantity = "Die Menge darf nicht negativ sein.";
    public const string ImportUnknownState = "Unbekannter Zustand „{0}“.";
    public const string ImportStateNotAllowed = "Der Zustand „{0}“ kann nicht importiert werden (nur Neu, Gebraucht – funktionsfähig, Defekt).";
    public const string ImportIssuedNotAllowed = "„Ausgegeben“ kann nicht importiert werden – bitte eine Ausgabe an Filiale buchen.";
    public const string ImportDeviceIsIssued = "Das Gerät ist an eine Filiale ausgegeben – bitte eine Rücknahme buchen.";
    public const string ImportCategoryChangeLocked = "Die Kategorie kann nicht in eine mit anderer Erfassungsart geändert werden, weil es zu dem Artikel bereits Buchungen gibt.";

    public const string BackupInvalidFile = "Die Datei ist keine gültige KassenLager-Datensicherung.";
    public const string BackupFromNewerVersion = "Die Datensicherung stammt aus einer neueren App-Version. Bitte zuerst die App aktualisieren.";

    public static string Format(string template, params object?[] args) =>
        string.Format(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), template, args);
}
