# Mau

Mau recommends Filipino main dishes from ingredients you type.
Type what you have, see the dishes you can cook, and open any dish for its ingredients and steps.
You can also create an account to save a pantry, favorite dishes, and keep a recent-search history.

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

## Accounts and saved data

Register at `/Account/Register` and log in at `/Account/Login`. Guests can still search and open dishes.
Signed-in users also get:

- **Pantry** (`/Profile/Pantry`): tick the ingredients you usually have. A search can count them as ingredients you already have.
- **Favorites** (`/Profile/Favorites`): dishes you saved from their details page.
- **History** (`/Profile/History`): recent searches, newest first, with delete and clear.

Pantry items, favorites, and history are stored in the same SQLite database as the catalog.

## Admin

The first admin is created at startup from user secrets, only when no user is in the `Admin` role yet.
Set the secrets with placeholders replaced by your own values (never commit them):

```powershell
dotnet user-secrets init --project src/Mau.Web
dotnet user-secrets set "Admin:Email" "your-email@example.com" --project src/Mau.Web
dotnet user-secrets set "Admin:Password" "YourPassw0rd" --project src/Mau.Web
```

The password must have at least 8 characters and contain a digit and a lowercase letter.
Restart the app, then log in with that email. If the email already has an account, that account
becomes admin and keeps its existing password. While an admin already exists, changing the
secrets does not create a second admin or change the password.

Admin pages:

- `/Admin` — dashboard with dish, ingredient and synonym counts.
- `/Admin/Ingredients` — list, filter, create, edit and delete ingredients with their synonyms.
- `/Admin/Dishes` — list, filter, create, edit and delete dishes with their ingredient rows and steps.

To reset the admin, stop the app and delete `src/Mau.Web/mau.db` (plus `mau.db-shm` and
`mau.db-wal` if they exist). The next start recreates the database and the admin from the current
secrets. Deleting the file also deletes all accounts.

## Tests

Run all tests from the repo root with `dotnet test`. To run one test class, use the filtered command in the table above.

## Database

The app creates its SQLite database at `src/Mau.Web/mau.db` on first start, applies migrations, and seeds the catalog automatically. The file is git-ignored and also holds the user accounts and their saved data. Never run `dotnet ef database update`.

To reset the data, stop the app and delete `src/Mau.Web/mau.db` (plus `mau.db-shm` and `mau.db-wal` if they exist). Deleting the file also deletes all accounts. The next start recreates, migrates, and seeds it.
