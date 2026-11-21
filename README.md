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

The app applies migrations and seeds its database at startup. Never run `dotnet ef database update`.
