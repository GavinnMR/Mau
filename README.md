# Mau

Mau recommends Filipino main dishes from ingredients you type.
Type what you have, see the dishes you can cook, and open any dish for its ingredients and steps.

## Prerequisites

- .NET SDK 10: `dotnet --version` must print `10.x`.
- EF Core tools: `dotnet tool install --global dotnet-ef --version 10.*`
- HTTPS development certificate: `dotnet dev-certs https --trust`

## Commands

Run these from the repo root.

| Task | Command |
|---|---|
| Build | `dotnet build` |
| Test all | `dotnet test` |
| Test one class | `dotnet test --filter "FullyQualifiedName~SearchServiceTests"` |
| Run app | `dotnet run --project src/Mau.Web` |
| Add migration | `dotnet ef migrations add <Name> --project src/Mau.Data --startup-project src/Mau.Web --output-dir Migrations` |

## Running the app

Run from the repo root:

```powershell
dotnet run --project src/Mau.Web
```

Then open http://localhost:5278. To use HTTPS instead:

```powershell
dotnet run --project src/Mau.Web --launch-profile https
```

and open https://localhost:7070.

## Tests

Run all tests from the repo root with `dotnet test`. To run one test class, use the filtered command in the table above.

## Database

The app creates its SQLite database at `src/Mau.Web/mau.db` on first start, applies migrations, and seeds the catalog automatically. The file is git-ignored. Never run `dotnet ef database update`.

To reset the data, stop the app and delete `src/Mau.Web/mau.db` (plus `mau.db-shm` and `mau.db-wal` if they exist). The next start recreates, migrates, and seeds it.
