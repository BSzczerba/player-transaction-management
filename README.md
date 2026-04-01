# Player Transaction Management API

Enterprise-grade REST API for managing financial transactions on gaming platforms. Built with **ASP.NET Core 8**, **Entity Framework Core**, and **SQL Server**, following **Clean Architecture** principles.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![Build](https://github.com/YOUR_USERNAME/player-transaction-management/actions/workflows/ci.yml/badge.svg)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Authentication & Authorization** — JWT Bearer tokens, role-based access (Player, Operator, Administrator, ComplianceOfficer), refresh tokens, account activation
- **Transaction Management** — Deposits & withdrawals with auto-approval rules, daily limits, optimistic concurrency
- **AML / KYC Compliance** — Automatic AML flagging (velocity, single-amount, daily-volume rules), player risk profiling, compliance officer workflow
- **Audit Trail** — Permanent, tamper-proof audit log for every state-changing operation
- **Notifications** — In-app notification system; compliance officers notified on AML flag
- **Payment Gateway Mock** — Realistic failure rates per payment method type, simulated latency, gateway reference IDs
- **Reports & Analytics** — Financial summaries, player activity, payment method breakdown, CSV exports (up to 10 000 rows)
- **Admin Panel** — Player limits, account status, role management, KYC verification
- **Rate Limiting** — Auth endpoints: 10 req/min; API: 100 req/min
- **Health Check** — `GET /health` (no auth required)

---

## Architecture

Clean Architecture with four layers (dependencies flow inward):

```
┌──────────────────────────────────────────────────────────┐
│  API Layer          Controllers, Middleware, Program.cs   │
│  (player-transaction-management/)                         │
├──────────────────────────────────────────────────────────┤
│  Application Layer  Services, DTOs, Validators, Mappings  │
│  (Application/)                                           │
├──────────────────────────────────────────────────────────┤
│  Infrastructure     EF Core, Repositories, Password Hash  │
│  (Infrastructure/)                                        │
├──────────────────────────────────────────────────────────┤
│  Domain Layer       Entities, Enums (no dependencies)     │
│  (Domain/)                                                │
└──────────────────────────────────────────────────────────┘
```

**Key patterns:**
- Repository + Unit of Work
- Service layer with explicit DB transactions
- AutoMapper (single `MappingProfile`)
- FluentValidation (all validators in one file)
- Global exception handler — controllers never use try/catch
- Soft deletes with EF Core global query filters
- Optimistic concurrency (`RowVersion`) on `Player` and `Transaction`

---

## Tech Stack

| Component | Technology |
|---|---|
| Framework | ASP.NET Core 8.0 |
| ORM | Entity Framework Core 8 + SQL Server |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`) |
| Password hashing | BCrypt.Net-Next |
| Mapping | AutoMapper 12 |
| Validation | FluentValidation 12 |
| Logging | Serilog (console + rolling file) |
| API docs | Swashbuckle / Swagger |
| Testing | xUnit + Moq + FluentAssertions |
| Containerization | Docker + Docker Compose |
| CI/CD | GitHub Actions |

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server on `localhost:1433` (credentials from `appsettings.json`), **or** Docker

### Option A — Run locally

```bash
# 1. Clone
git clone https://github.com/YOUR_USERNAME/player-transaction-management.git
cd player-transaction-management

# 2. (Optional) override settings — never commit this file
cp player-transaction-management/appsettings.json \
   player-transaction-management/appsettings.Development.json
# Edit appsettings.Development.json with your local SQL Server credentials

# 3. Install EF tools if needed
dotnet tool install --global dotnet-ef

# 4. Apply migrations
dotnet ef database update --project Infrastructure \
  --startup-project player-transaction-management

# 5. Run (Swagger available at http://localhost:5235/swagger)
dotnet run --project player-transaction-management
```

In **Development** mode, migrations are applied and the database is seeded automatically on startup.

### Option B — Docker Compose (recommended)

```bash
# Build and start API + SQL Server
docker compose up --build

# API:     http://localhost:5235
# Swagger: http://localhost:5235/swagger
```

The `api` container waits for SQL Server to pass its health check before starting. Migrations and seeding run automatically (the container uses `ASPNETCORE_ENVIRONMENT=Development`).

**Stop and clean up:**
```bash
docker compose down        # stop containers (data volume preserved)
docker compose down -v     # stop + delete volume (fresh DB on next up)
```

---

## Running Tests

```bash
# All unit tests
dotnet test Tests/Tests.csproj --verbosity normal

# With code coverage report
dotnet test Tests/Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults
```

The test suite (`Tests/Services/TransactionServiceTests.cs`) covers 20 scenarios:

| Category | Tests |
|---|---|
| Deposit auto-approve | amount < 100, not AML-flagged |
| Deposit manual approval | amount ≥ 100 |
| AML detection | velocity (5+ in 24h), single amount > 10K, daily volume > 20K |
| Daily limit enforcement | deposit and withdrawal limits |
| Account rules | suspended account, KYC not verified |
| Payment method rules | min/max amount |
| Approve flow | gateway success → Completed, gateway failure → Failed |
| Approve guards | own transaction, non-pending status |
| Reject flow | happy path, non-pending guard |
| Notifications | AML flag notifies all ComplianceOfficers |

---

## API Reference

### Authentication (`/api/auth`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/register` | Public | Register new player account |
| POST | `/login` | Public | Login, returns JWT + refresh token |
| POST | `/refresh` | Public | Refresh access token |
| POST | `/activate` | Public | Activate account via token |
| GET | `/me` | Bearer | Current user profile |

Rate limited: **10 requests/minute**.

### Transactions (`/api/transactions`)

| Method | Endpoint | Role | Description |
|---|---|---|---|
| POST | `/deposit` | Player | Create deposit |
| POST | `/withdrawal` | Player | Create withdrawal (KYC required) |
| GET | `/my` | Player | Own transaction history |
| GET | `/{id}` | Player/Operator/Admin | Get by ID (404 for other player's txn) |
| GET | `/pending` | Operator, Admin | Pending transactions |
| GET | `/flagged` | Operator, Admin, ComplianceOfficer | AML-flagged transactions |
| PUT | `/{id}/approve` | Operator, Admin | Approve → payment gateway |
| PUT | `/{id}/reject` | Operator, Admin | Reject with reason |

### Players (`/api/players`)

| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/me` | Player | Own profile |
| PUT | `/me` | Player | Update own profile |
| GET | `/` | Admin | All players (paged) |
| GET | `/{id}` | Admin | Player by ID |

### Compliance (`/api/compliance`)

| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/summary` | ComplianceOfficer, Admin | AML dashboard |
| GET | `/flagged` | ComplianceOfficer, Admin | Flagged transactions |
| GET | `/players/{id}/risk` | ComplianceOfficer, Admin | Player risk profile |
| POST | `/flagged/{id}/clear` | ComplianceOfficer, Admin | Clear AML flag |

### Admin (`/api/admin`)

| Method | Endpoint | Role | Description |
|---|---|---|---|
| PUT | `/players/{id}/limits` | Admin | Update daily deposit/withdrawal limits |
| POST | `/players/{id}/suspend` | Admin | Suspend account |
| POST | `/players/{id}/activate` | Admin | Activate account |
| POST | `/players/{id}/close` | Admin | Close account |
| PUT | `/players/{id}/role` | Admin | Change user role |
| POST | `/players/{id}/kyc` | Admin | Set KYC verification |
| GET | `/audit-logs` | Admin | All audit logs (filtered, paged) |
| GET | `/players/{id}/audit-logs` | Admin | Audit logs per player |

### Reports (`/api/reports`)

| Method | Endpoint | Role | Description |
|---|---|---|---|
| GET | `/financial-summary` | Admin, Operator | Deposits/withdrawals/net flow + period breakdown |
| GET | `/players` | Admin | Player activity report |
| GET | `/payment-methods` | Admin, Operator | Per-method stats |
| GET | `/export/transactions` | Admin, Operator | CSV export (max 10 000 rows) |
| GET | `/export/players` | Admin | Full player list CSV |

### Other

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/notifications` | Bearer | Own notifications |
| GET | `/api/notifications/unread` | Bearer | Unread notifications |
| GET | `/api/notifications/unread/count` | Bearer | Unread count |
| POST | `/api/notifications/{id}/read` | Bearer | Mark as read |
| POST | `/api/notifications/read-all` | Bearer | Mark all as read |
| GET | `/api/paymentmethods` | Bearer | Active payment methods |
| GET | `/api/paymentmethods/{id}` | Bearer | Payment method by ID |
| GET | `/health` | Public | Health check (DB connectivity) |

---

## Business Rules

| Rule | Value |
|---|---|
| Deposit auto-approve threshold | < 100 (and not AML-flagged) |
| Deposits requiring manual approval | ≥ 100, or AML-flagged |
| Withdrawals | Always manual approval + KYC required |
| AML flag — velocity | 5+ transactions in rolling 24h window |
| AML flag — single amount | > 10 000 |
| AML flag — daily volume | > 20 000 |
| Default daily deposit limit | 10 000 |
| Default daily withdrawal limit | 5 000 |
| Soft deletes | All entities (except `AuditLog` — permanent) |
| Concurrent modification | HTTP 409 (`DbUpdateConcurrencyException`) |
| IDOR protection | `GET /transactions/{id}` returns 404 for another player's txn |

---

## Configuration

All settings are in `appsettings.json`. For local development, override in `appsettings.Development.json` (git-ignored).

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=PlayerTransactionDB;..."
  },
  "JwtSettings": {
    "SecretKey": "...",
    "Issuer": "PlayerTransactionAPI",
    "Audience": "PlayerTransactionClient",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  }
}
```

For Docker, settings are passed as environment variables using `__` as the section separator (e.g. `JwtSettings__SecretKey`).

---

## CI/CD

GitHub Actions pipeline (`.github/workflows/ci.yml`):

1. **Build & Test** — runs on every push and PR to `main`
   - `dotnet restore` → `dotnet build` → `dotnet test`
   - Uploads test results and code coverage as artifacts

2. **Docker Build & Push** — runs only on push to `main`
   - Builds Docker image and pushes to Docker Hub
   - Tags: `latest` + commit SHA

To enable Docker Hub push, add repository secrets:
- `DOCKERHUB_USERNAME`
- `DOCKERHUB_TOKEN`

---

## Seeded Test Accounts

After first startup (Development mode), the database is seeded with:

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@test.com` | `Admin123!` |
| Operator | `operator@test.com` | `Admin123!` |
| ComplianceOfficer | `compliance@test.com` | `Admin123!` |
| Player | `player@test.com` | `Admin123!` |

> **Note:** These credentials are for development/demo purposes only.

---

## License

MIT
