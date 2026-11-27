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

Needs the .NET 6+ SDK, Windows PowerShell, and network access to the SQL server of the environment you pick
(or LocalDB if you use `-UseLocalDb`).

```powershell
.\Start-AdminLocal.ps1                          # Test IDP database; admin database = IdentityServerAdminDB on the same server
.\Start-AdminLocal.ps1 -Environment Development
.\Start-AdminLocal.ps1 -ConfigConnectionString '<read-only connection string>'   # recommended for the IDP database
.\Start-AdminLocal.ps1 -AdminConnectionString '<admin database connection string>'
.\Start-AdminLocal.ps1 -UseLocalDb              # admin database on LocalDB instead of SQL Server
.\Start-AdminLocal.ps1 -Stop
```

The script rebuilds, creates the admin database tables on the first run (it asks before writing to a shared server), then starts the
STS and the Admin. Open <https://localhost:44303> and sign in as **`admin`** with the password in
`src/Clopay.IdentityServer4.Admin/identitydata.json` (`Pa$$word123` - note the **two** `$`). **On a shared SQL server, change that
password after the first sign-in.**

The IDP database is never migrated or seeded. If the admin database does not exist yet, the SQL login needs permission to create
it - otherwise ask a DBA to create an empty `IdentityServerAdminDB`.

Or run from an IDE: start **STS** and **Admin** with `ASPNETCORE_ENVIRONMENT=Local` (the default launch profile; uses LocalDB).

## Configuration

Each app has `appsettings.json` (neutral defaults) plus one file per environment, chosen by `ASPNETCORE_ENVIRONMENT`:

| Environment | Meaning | IDP database (from `cAppSettings`) | Admin URL |
|---|---|---|---|
| `Local` | your machine, LocalDB, seed allowed | LocalDB `ClopayAdminLocal` | `https://localhost:44303` |
| `Development` | shared dev tier | `10.103.3.4` / `IdentityServerDev` | `https://dev-iamui.clopay.com` |
| `Test` | QA | `10.105.3.4` / `IdentityServerDB_DBTest` | `https://test-iamui.clopay.com` |
| `Staging` | pre-production | `MAS-SQL-01T` / `IdentityServerDB_DBStaging_New` | `https://staging-iamui.clopay.com` |
| `Production` | live - **placeholders only** | `<PROD_SQL_SERVER>` / `<PROD_IDP_DATABASE>` | `https://<PROD_ADMIN_HOST>` |

The sign-in service (STS) for each environment is the Admin host name with `-sts` (for example `dev-iamui-sts.clopay.com`).
**These host names are proposals - confirm them before deploying.**

**Production is a placeholder.** There are no production values in `cAppSettings`, so `appsettings.Production.json` (Admin and STS) contains
`<PROD_...>` markers and the app will not start until every one is replaced (environment variables / Key Vault). Get the SQL server,
IDP database name, SQL login and host names from the DBA / platform team, start with a read-only login, and get explicit approval.

Each non-Local environment also has two one-time seed overlays in the Admin project, used only to bootstrap `IdentityServerAdminDB`:
`identityserverdata.<Env>.json` (the sign-in client with that environment's redirect URLs) and `identitydata.<Env>.json` (the first admin
account, with **no default password** - supply `IdentityData__Users__0__Password`; the client secret comes from
`IdentityServerData__Clients__0__ClientSecrets__0__Value`).

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
