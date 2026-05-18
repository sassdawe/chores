# Chores

Chores is a self-hosted chore tracker for homes, offices, garages, online estates, and other named spaces built with ASP.NET Core Razor Pages.  
It records when chores are completed and shows whether each chore is on time or overdue based on its schedule.

The app is designed to be a **tracker**, so it make it super easy to record when a chore is completed without making any disturbance. 

> [!NOTE]
> Chores MVP stands for: Most Valuable Partner in doing the chores. 

## Design rules:

1. I will not have my service end up on HIBP!
2. No password no problem.
3. No PII stored means no PII to leak.
4. Should run on cloud as good as in a homelab.

## Features

- Track chores with fixed schedules:
  - Daily
  - Twice a week
  - Every two days
  - Weekly
  - Bi-weekly
  - Monthly
  - Quarterly
  - Every 6 months
  - Yearly
  - Every 2 years
- Mark chores as complete with timestamped history
- View overdue/on-time status on the dashboard for all spaces or a selected space
- Create and rename multiple named spaces and manage chores, members, and labels for each one
- Space owners can invite members by login name, and invited users can accept invites from their profile without leaving their existing spaces
- Passwordless authentication with FIDO2/passkeys
- SQLite persistence (single-container friendly)

## Tech Stack

- ASP.NET Core (.NET 10)
- Razor Pages
- Entity Framework Core
- SQLite
- xUnit

## Demo instance

You can access a demo instance of the [Chores App here](https://chores-mvp.azurewebsites.net/Auth/Login).

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
