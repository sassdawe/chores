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
dotnet run --project /home/runner/work/chores/chores/Chores/Chores.csproj
```

By default, the app stores data in:

`/home/runner/work/chores/chores/Chores/data/chores.db`

## Test

```sh
dotnet test
```

## Deployment

See:

- `/home/runner/work/chores/chores/docs/deployment.md`

## License

No license file is currently included in this repository.
