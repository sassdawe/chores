# Chores

Chores is a self-hosted household chore tracker built with ASP.NET Core Razor Pages.  
It records when chores are completed and shows whether each chore is on time or overdue based on its schedule.

## Features

- Track chores with fixed schedules:
  - Daily
  - Twice a week
  - Every two days
  - Weekly
  - Bi-weekly
  - Monthly
- Mark chores as complete with timestamped history
- View overdue/on-time status on the dashboard
- Manage household members and labels
- Passwordless authentication with FIDO2/passkeys
- SQLite persistence (single-container friendly)

## Tech Stack

- ASP.NET Core (.NET 10)
- Razor Pages
- Entity Framework Core
- SQLite
- xUnit

## Run Locally

```sh
dotnet build
dotnet run --project Chores/Chores.csproj
```

By default, the app stores data in:

`Chores/data/chores.db`

## Test

```sh
dotnet test
```

## Deployment

See:

- `docs/deployment.md`

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
