# Player Transaction Management API

Hey! This is a REST API I built for managing transactions on a gaming platform, deposits and withdrawals for players, plus all the compliance stuff that comes with handling real money (AML checks, KYC, audit logs, that kind of thing). I wrote it with ASP.NET Core 10, EF Core 10 and SQL Server, and organized it using a Clean Architecture style split into layers.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Build](https://github.com/BSzczerba/player-transaction-management/actions/workflows/ci.yml/badge.svg)
![License](https://img.shields.io/badge/license-MIT-green)

---

## What it does

I tried to cover most of the things you'd expect from a real transaction system, not just a basic CRUD app. Here's what's in there:

* Login and registration with JWT tokens, refresh tokens and account activation. There are a few roles too: Player, Operator, Administrator and ComplianceOfficer, each with different permissions.
* Deposits and withdrawals, with some auto-approval logic for small deposits and daily limits per player.
* AML detection (flags suspicious activity, like too many transactions in a short time or a huge single amount) and a KYC verification flag on players.
* An audit log that tracks every important action in the system, so nothing gets deleted for real, it's all soft deletes.
* An in-app notification system, mostly used to ping compliance officers when something gets flagged.
* A mocked payment gateway that simulates delays and random failures so it feels realistic, since there's no real payment provider hooked up here.
* Some reporting endpoints and CSV exports for admins and operators.
* An admin panel (well, admin endpoints, no UI) to manage player limits, suspend accounts, change roles, etc.
* Rate limiting on the auth endpoints so people can't hammer login/register.
* A simple health check endpoint too.

---

## Architecture (short version)

I split the project into a few layers, going API -> Application -> Infrastructure -> Domain, with the idea that the Domain layer (entities, enums) doesn't depend on anything else. The general idea is:

* Controllers just handle HTTP stuff and call services, no business logic there.
* Services in the Application layer hold the actual business logic (like the AML rules, approval flow, etc).
* Infrastructure has the EF Core stuff, repositories, and things like password hashing.
* Domain is just the entities and enums, nothing fancy.

I also used the Repository + Unit of Work pattern, AutoMapper for mapping entities to DTOs, and FluentValidation for input validation. There's a global exception handler too, so controllers don't need try/catch everywhere, exceptions just get mapped to the right HTTP status code automatically.

---

## Tech stack

* ASP.NET Core 10
* Entity Framework Core 10 + SQL Server
* JWT for authentication
* BCrypt for password hashing
* AutoMapper
* FluentValidation
* Serilog for logging
* Swagger/Swashbuckle for API docs
* xUnit + Moq + FluentAssertions for tests
* Docker + Docker Compose
* GitHub Actions for CI/CD

---

## How to run it

You'll need the .NET 10 SDK, and either a local SQL Server instance or Docker (Docker is way easier).

### Running it locally

Clone the repo, then open it in Visual Studio, or just use the CLI:

```bash
git clone https://github.com/YOUR_USERNAME/player-transaction-management.git
cd player-transaction-management
```

If you need to override the connection string or JWT settings for your machine, copy `appsettings.json` into `appsettings.Development.json` and edit that one instead (it's git-ignored, so your local secrets won't get committed by accident).

You'll also need the EF tools if you don't already have them:

```bash
dotnet tool install --global dotnet-ef
```

Then run the migrations to get the database set up:

```bash
dotnet ef database update --project Infrastructure --startup-project player-transaction-management
```

And finally just odpalić the project:

```bash
dotnet run --project player-transaction-management
```

Swagger UI should pop up at something like `http://localhost:5235/swagger` once it's running. In Development mode the app also seeds the database automatically on startup, so you'll already have some test accounts and data to play with.

### Running it with Docker (probably the easier option)

```bash
cp .env.example .env
```

Then open `.env` and fill in your own values for the SQL Server password and JWT secret (there's a comment in the file explaining what's expected).

```bash
docker compose up --build
```

That spins up both the API and a SQL Server container, and the API waits until the DB is actually ready before starting up. Migrations and seeding happen automatically here too.

To stop everything:

```bash
docker compose down
```

Add `-v` if you also want to wipe the database volume and start fresh next time.

---

## Running the tests

```bash
dotnet test Tests/Tests.csproj
```

There's a decent chunk of unit tests (around 40) covering the transaction service specifically, things like the auto-approval rules, AML flagging, daily limits, the approve/reject flow, and account/KYC checks. I used Moq to fake out the dependencies and FluentAssertions because the assertions read a lot nicer than plain xUnit asserts.

---

## API overview

Quick rundown of the main endpoint groups, not every single detail but enough to get the idea:

* `/api/auth` – register, login, refresh token, activate account, get current user. Public except for `/me`.
* `/api/transactions` – deposit and withdraw (players only), view your own transactions, and for operators/admins there's approving, rejecting and listing pending or flagged transactions.
* `/api/players` – players can view/edit their own profile, admins can list everyone.
* `/api/compliance` – for compliance officers and admins, AML summary, flagged transactions, and a per-player risk profile with a computed AML score.
* `/api/admin` – admin-only stuff like updating player limits, suspending/activating accounts, changing roles, KYC verification, and browsing audit logs.
* `/api/reports` – financial summaries, player activity, payment method stats, and CSV exports.
* `/api/notifications` and `/api/paymentmethods` – smaller endpoints for the logged-in user's own notifications and available payment methods.
* `/health` – just a basic health check, no auth needed.

The easiest way to explore all of this is just running the project and poking around in Swagger, it's a lot clearer than reading a table here.

---

## Business rules worth knowing

Some of the logic that drives the whole thing:

* Small deposits get auto-approved through the (mocked) payment gateway, bigger ones need a human (operator/admin) to approve them manually.
* Withdrawals always need manual approval, and the player has to be KYC verified first.
* If a player does a bunch of transactions in a short window, or one really big transaction, or their daily volume gets too high, the transaction gets flagged for AML review and compliance officers get notified.
* Players also get an AML risk score out of 100, built from a handful of signals (KYC status, how often they get flagged, transaction velocity, biggest single amount, daily volume). Higher score = higher risk.
* Everything uses soft deletes, nothing actually gets removed from the DB except the audit log is untouchable on purpose, it's meant to be a permanent record.
* If two people try to update the same record at the same time, you'll get a 409 conflict instead of silently overwriting data (optimistic concurrency).
* If you try to fetch a transaction that isn't yours, you get a 404 instead of a 403, so people can't even tell if the ID belongs to someone else.

---

## Configuration

Most of the config lives in `appsettings.json` (connection string, JWT settings, that sort of thing). For local dev just override what you need in `appsettings.Development.json` instead of touching the main file. When running through Docker, the same settings get passed in as environment variables instead.

---

## CI/CD

There's a GitHub Actions workflow that runs the build and tests on every push/PR to main, and if it's a push to main it also builds and pushes a Docker image. Wanted to have a proper pipeline instead of testing everything by hand.

---

## Test accounts

Once you run it in Development mode, the database gets seeded with a few test accounts (admin, operator, compliance officer, and a regular player) so you don't have to register manually just to try things out. These are dummy accounts, not meant for anything beyond local testing.

---

## What I'd do differently now

Looking back at this after working on it for a while, a few things I'd approach differently if I started over:

* Use CQRS with MediatR instead of the Services + Repository/Unit of Work combo. The service layer works fine, but for something this transaction-heavy, splitting reads and writes into separate commands/queries would probably make the intent of each operation clearer.
* Write actual integration tests against a real (containerized) database using Testcontainers, instead of relying only on unit tests with mocked repositories. Unit tests caught a lot, but they don't tell you if the EF configurations or the transaction handling actually work against SQL Server.
* Focus on fewer features but go deeper on them. I added a lot of breadth (reports, notifications, compliance scoring, admin tools) and in hindsight I'd rather have picked two or three of those and really hardened them instead. I'm actually in the process of trimming things down now.

## What's intentionally missing

Some things aren't in the API on purpose, either because they were out of scope for a solo backend project or because I ran out of time before I wanted to ship this:

* No frontend. This was originally planned with a Nuxt.js/Vue client, but I decided to keep the scope to the backend and just expose everything through Swagger.
* No real email sending. Notifications are stored in the database and exposed through the API, but there's no actual email provider wired up, that was on the original plan but got cut.
* No PDF/Excel export, only CSV. Good enough to prove the reporting logic works without pulling in a whole reporting library.
* No Kubernetes manifests, it just runs via Docker Compose. K8s was on the original roadmap but felt like overkill for a project this size.
* No real payment gateway integration, it's mocked. Wiring up an actual provider (Stripe, PayPal, whatever) wasn't the point here, the interesting part was the business logic around approvals and AML, not the payment processing itself.
* No API versioning yet. Fine for now since there's only one client (Swagger/whoever's testing it), but it'd need to be added before any real frontend depended on it.

---

## License

MIT
