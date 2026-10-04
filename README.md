# KassenLager

An offline-first Android app for managing the spare-parts store of a field service technician who installs and repairs supermarket checkout systems.

It answers the questions that come up every day in the field — *"Do I have this model in stock for this customer?"*, *"Which device did I leave at which branch?"*, *"What do I need to reorder?"* — and turns the twice-yearly stock count into a scan-and-tap workflow with an Excel report at the end.

> **Status:** Phase 3 of 6 complete — master data, stock ledger with devices, bookings, loans to branches, storno, search, Excel import/export and backup. See [Roadmap](#roadmap).

## The problem

- Every item in the store belongs to exactly one of several retail-chain customers and may only be used at that customer's branches.
- Devices (POS PCs, scanners, scales, printers, …) are tracked individually by serial number; cables and consumables only by quantity.
- Devices are regularly left at branches as loan units while the broken one goes to repair, so the history of *what went where* matters.
- Twice a year the complete stock has to be counted and reported to headquarters.
- The phone is the only device, and connectivity at the sites is unreliable — the app must work fully offline.

## Features

| Area | Status |
|---|---|
| Customers, categories (serial- or quantity-tracked), units, articles | ✅ Phase 1 |
| Stock per article × customer, devices, movements, storno, loans to branches, search | ✅ Phase 2 |
| Excel import with validation and preview, exports, full backup & restore | ✅ Phase 3 |
| Reorder suggestions and orders per customer, goods receipt from orders | Phase 4 |
| Stock count (by serial number and quantity) with Excel report | Phase 5 |
| Barcode / serial number scanning with the camera | Phase 6 |

The UI is German (the app is used in Germany); code and documentation are English.

## Architecture

```
src/
  KassenLager.Core   domain model, business rules, services (UI-independent, unit-tested)
  KassenLager.Data   EF Core DbContext, entity configurations, migrations, seed data
  KassenLager.App    .NET MAUI app (Android): views, view models, platform services
tests/
  KassenLager.Tests  xUnit tests for Core and Data against SQLite in-memory
```

- **MVVM** with CommunityToolkit.Mvvm source generators; view models stay thin and delegate to Core services.
- **Persistence:** SQLite via EF Core with migrations. Core services depend on an `IAppDbContext` abstraction and create a short-lived context per operation.
- **Business rule violations** surface as `BusinessRuleException` with a user-facing message; technical errors are logged to a local file and shown as a generic message.
- **Trim-safe UI:** compiled bindings are enforced (`x:DataType` everywhere, binding warnings are errors) and XAML is source-generated.

### Design decisions

- **Movements are the single source of truth for stock.** Quantities are derived from the immutable movement ledger instead of being stored separately, so stock and history cannot drift apart. Mistakes are corrected with a linked storno movement, never by editing or deleting.
- **Devices carry their state, movements record every transition.** Each device movement stores the state before and after; the test suite replays the complete ledger after every test and checks that the state chains, stock changes and device counts agree.
- **Storno unwinds history step by step:** a movement can be reversed once, a reversal cannot be reversed, and a device movement only while it is the device's latest — so every intermediate state stays consistent. Reversing the movement that created a device voids it (history kept, serial number free again).
- **Import preview = import:** the preview runs the complete import inside a transaction that is rolled back, so it shows exactly what will happen (new / updated / unchanged / faulty, with row numbers and reasons); the import applies all valid rows in one transaction.
- **Backups are consistent single files** (WAL checkpoint + `VACUUM INTO`); a restore validates the file and its schema version, saves the current data first and migrates older backups.
- **Customer integrity:** every stock record, device and movement belongs to exactly one customer; there are no transfers between customers.
- **Offline and private by design:** no internet permission in release builds, Android cloud auto-backup disabled, no accounts, no analytics. Backups are explicit file exports.
- **Case-insensitive uniqueness** (names, article numbers) is checked Unicode-aware in the services (SQLite's `NOCASE` only folds ASCII) and backed by unique indexes.

## Tech stack

.NET 10 · .NET MAUI (Android, min. API 26) · CommunityToolkit.Mvvm / .Maui · EF Core 10 + SQLite · MiniExcel · xUnit

## Getting started

Prerequisites: .NET SDK 10.0.300+, the `maui-android` workload, JDK 21 and the Android SDK.

```bash
dotnet tool restore                                             # dotnet-ef
dotnet test tests/KassenLager.Tests                             # unit tests
dotnet build src/KassenLager.App -f net10.0-android             # debug build
dotnet build src/KassenLager.App -f net10.0-android -t:Run      # deploy to a USB-connected device
dotnet publish src/KassenLager.App -f net10.0-android -c Release  # trimmed release APK
```

Adding a migration:

```bash
dotnet ef migrations add <Name> --project src/KassenLager.Data --startup-project src/KassenLager.Data --output-dir Migrations
```

## Roadmap

1. ✅ Solution structure, database, seed data, master data management, settings
2. ✅ Movements, stock and devices, loans to branches and returns, storno, search, dashboard
3. ✅ Excel import (template, validation, preview), exports, backup and restore
4. Reorder suggestions, orders, goods receipt from orders
5. Stock count and count report
6. Camera scanning for barcodes and serial numbers, polish

## Third-party assets

- [Material Icons](https://github.com/google/material-design-icons) by Google — Apache License 2.0
- [Open Sans](https://fonts.google.com/specimen/Open+Sans) — SIL Open Font License 1.1

## License

© 2026 Özal Akdeniz. All rights reserved. The source is published for portfolio purposes; no license to use, copy or modify it is granted.
