# Clopay IAM Admin

A small web UI for managing the **Clopay IdentityServer4** instance: clients, client secrets, API resources,
scopes, identity resources and active sessions (persisted grants).

Built on .NET 6 and IdentityServer4 (EF 4.1.2), in the Clopay look and trimmed down to what we use: **SQL Server only, English only, no themes.**

## What you can do

| Task | Where |
|---|---|
| Create a client in a few clicks (web app, SPA, service, legacy username/password) | **New client** (top right) |
| Generate a new client secret, with or without a grace period for the old one | **Clients → Rotate secret** |
| Edit every client setting, clone or delete a client | **Clients → Edit** |
| Manage API resources, scopes and identity resources | **APIs & Scopes** |
| Review / revoke refresh tokens and consents | **Active sessions** |
| See who changed what | **Logs → Audit log** |

Secrets are stored hashed (SHA-256). The plain text is shown **once**, right after it is generated - copy it then.
Rotating with a grace period leaves the old secret valid until the chosen date (7 days by default), so the app can be
updated without downtime; "Revoke immediately" is for leaked secrets.

> **Admin users / Roles** manage the people who can sign in to *this admin site*. They are not Clopay dealer users.

## Repository layout

```
src/
  Clopay.IdentityServer4.Admin            Admin site (host)            https://localhost:44303
  Clopay.IdentityServer4.STS.Identity     Sign-in service for admins   https://localhost:44310
  Clopay.IdentityServer4.Admin.UI         Screens (controllers, views, Clopay CSS in wwwroot/css/clopay.css)
  Clopay.IdentityServer4.Admin.BusinessLogic*   Services, DTOs, audit events
  Clopay.IdentityServer4.Admin.EntityFramework* Data access (SQL Server)
  Clopay.IdentityServer4.Shared*          Shared helpers and configuration
tests/                                     Unit tests
cAppSettings/                              Reference copies of the Clopay IDP settings (source of the values below)
_designs/                                  Handover notes and the read-only SQL login script
Start-AdminLocal.ps1                       One-command local launcher
```

## Run it locally

Needs the .NET 6+ SDK, SQL Server LocalDB, and (for the Clopay database) network access to the SQL server.

```powershell
.\Start-AdminLocal.ps1                                    # reads the Test IDP database
.\Start-AdminLocal.ps1 -ConfigConnectionString '<read-only connection string>'   # recommended
.\Start-AdminLocal.ps1 -Stop
```

Open <https://localhost:44303> and sign in with user **`admin`** and the seeded local password from
`src/Clopay.IdentityServer4.Admin/identitydata.json` (`Pa$$word123` - note the **two** `$`). That account exists only in
your throw-away LocalDB database.

Or run from an IDE: start **STS** and **Admin** with `ASPNETCORE_ENVIRONMENT=Local` (the default launch profile).
The first run of the Admin creates and seeds the LocalDB database.

## Configuration

Each app has `appsettings.json` (neutral defaults) plus one file per environment, chosen by `ASPNETCORE_ENVIRONMENT`:

| Environment | Meaning | IDP database (from `cAppSettings`) | Admin URL |
|---|---|---|---|
| `Local` | your machine, LocalDB, seed allowed | LocalDB `ClopayAdminLocal` | `https://localhost:44303` |
| `Development` | shared dev tier | `10.103.3.4` / `IdentityServerDev` | `https://dev-iamui.clopay.com` |
| `Test` | QA | `10.105.3.4` / `IdentityServerDB_DBTest` | `https://test-iamui.clopay.com` |
| `Staging` | pre-production | `MAS-SQL-01T` / `IdentityServerDB_DBStaging_New` | `https://staging-iamui.clopay.com` |
| `Production` | live | `10.109.3.5` / `identityserverdb` | `https://prod-iamui.clopay.com` |

The sign-in service (STS) for each environment is the Admin host name with `-sts` (for example `dev-iamui-sts.clopay.com`).
**These host names are proposals - confirm them before deploying.**

Two kinds of database are involved:

* **IDP database** (`ConfigurationDbConnection`, `PersistedGrantDbConnection`) - the real Clopay IdentityServer database. This is
  what you edit.
* **Admin database** `IdentityServerAdminDB` (the other four connection strings, and everything in the STS) - the admin tool's own
  users, audit log and data-protection keys. It is separate on purpose.

### Secrets

**No passwords are stored in git.** Connection strings contain `<DB_PASSWORD>` and the Admin sign-in client has
`<ADMIN_CLIENT_SECRET>`. Supply real values at deploy time with environment variables or Key Vault, for example:

```
ConnectionStrings__ConfigurationDbConnection=Data Source=...;Password=...
ConnectionStrings__PersistedGrantDbConnection=Data Source=...;Password=...
AdminConfiguration__ClientSecret=...
```

### Safety rules (read before changing anything)

1. **Never** set `SeedConfiguration:ApplySeed` or `DatabaseMigrationsConfiguration:ApplyDatabaseMigrations` to `true` while
   `ConfigurationDbConnection` points at the IDP database - the admin tool's migrations and sample clients would be written into the real
   tables. They are `false` in every environment file; only `Local` turns them on.
2. To create `IdentityServerAdminDB` once in a deployed environment, run the Admin once with all six connection strings pointing at
   that database and both flags `true`, then switch back (or let a DBA create it).
3. Start with a **read-only SQL login** (`_designs/Create-ReadOnly-Login.sql`) until the tool has been checked in that environment.
4. Start the **Admin before the STS** on a fresh database - the Admin creates the tables the STS uses.

## Build and test

```bash
dotnet build Clopay.IdentityServer4.Admin.sln
dotnet test  tests/Clopay.IdentityServer4.Admin.UnitTests
```

## Licence

MIT - see [LICENSE.md](LICENSE.md) (includes the notice for the open-source admin UI this project started from).
