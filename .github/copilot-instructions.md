# Copilot Instructions

## Project Overview

**Chores** is a self-hosted household chore tracker — a lightweight web app deployable on Raspberry Pi or Docker. It is not a task/reminder app; it is a **history tracker** that records when each chore was last completed and shows how close or overdue it is relative to its configured repetition schedule.

## Architecture

**Stack:** ASP.NET Core Razor Pages · Entity Framework Core · SQLite · xUnit

Single-project solution — all pages, models, data access, and services live in one ASP.NET Core project.

**Authentication:** FIDO2 only (no passwords, no email). Users register and sign in with passkeys. Household members can be invited by login name; if a login name doesn't exist, the app returns no error (prevents account discovery).

**Core domain concepts:**
- **Chore** — has a name and a repetition schedule: daily, twice a week, every two days, weekly, bi-weekly, or monthly.
- **CompletionRecord** — records the date/time a chore was marked done and which user did it.
- **Schedule adherence** — the UI derives and displays how overdue or on-time each chore is; there is no push notification or email.

**UI flow:**
1. Home page lists all chores with their schedule status (on time / overdue).
2. Tapping a chore shows a confirmation screen ("Mark as completed?").
3. Confirming records a `CompletionRecord` with the current timestamp and user.
4. Separate pages for managing chores (add/edit) and household members.

**Data:** SQLite file stored in the app's data directory; EF Core migrations manage the schema.

## Build, Test & Lint

```sh
# Build
dotnet build

# Run (development)
dotnet run

# Test (all)
dotnet test

# Test (single test by name)
dotnet test --filter "FullyQualifiedName~YourTestName"

# EF Core migrations
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

## Key Conventions

- **No passwords anywhere** — authentication is FIDO2-only; never add password fields or email-based flows.
- **No account discovery** — inviting a user by login name must not reveal whether the name exists or not if the user is not found.
- **Schedules are an enum** — the repetition options (daily, twice a week, every two days, weekly, bi-weekly, monthly) are a fixed set; treat them as a closed enum, not free-form text.
- **Completion is append-only** — `CompletionRecord` rows are never edited or deleted; schedule adherence is always derived from the latest record.
- **Self-hosting constraints** — avoid external service dependencies (no SMTP, no cloud auth, no telemetry). The app must run offline on a local network.
- **Instruction files** — domain- or technology-specific Copilot guidance lives in `.github/instructions/` as `*.instructions.md` files with YAML frontmatter (`description` + `applyTo` glob). See `.github/instructions/instructions.instructions.md` for the authoring guide.
