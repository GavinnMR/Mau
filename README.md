# Mau

Mau recommends Filipino main dishes from the ingredients you type.

## Features

- Ingredient search with live hints and chips in English and Filipino
- Excluding ingredients
- Dish details
- Registration and login
- Pantry
- Favorites
- Search history
- An admin area for managing dishes and ingredients

## Run it

Requires the .NET 10 SDK.

```powershell
dotnet run --project src/Mau.Web --launch-profile https
```

Open the https address it prints. If the browser warns about the certificate, run:

```powershell
dotnet dev-certs https --trust
```

The SQLite database is created automatically at `src/Mau.Web/mau.db` with 14 sample dishes.
To reset it, stop the app and delete that file; this also removes all accounts.

## Try the admin pages

Set the admin credentials with your own values:

```powershell
dotnet user-secrets init --project src/Mau.Web
dotnet user-secrets set "Admin:Email" "your-email@example.com" --project src/Mau.Web
dotnet user-secrets set "Admin:Password" "YourPassw0rd" --project src/Mau.Web
```

Then log in with that email and open `/Admin`.

## Run the tests

```powershell
dotnet test
```
