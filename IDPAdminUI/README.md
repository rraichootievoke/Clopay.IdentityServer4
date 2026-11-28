# Clopay IDP Admin UI

A web UI for managing the **Clopay IdentityServer4 (IDP)**: clients, client secrets, API resources, API scopes, identity
resources and active sessions. It replaces the old, closed-source Identity Express Manager UI (`Clopay.IdentityServer/src/ui/AdminUi`).

* **.NET 8**, one project (`src/Clopay.IDPAdminUI`), ASP.NET Core MVC, Entity Framework Core on SQL Server.
* Reads and updates the **IdentityServer4 tables of the IDP database** (the same database the IDP uses, e.g. `IdentityServerDB_DBTest`).
* **Owns no database**: no migrations, no seed data, nothing is created. Logs and the audit trail are files; encryption keys are a folder.
* Administrators **sign in through the Clopay IDP itself** (OpenID Connect) - the same way the old UI worked. Only users on an allow-list get in.
* **Read-only switch** (`AdminConfiguration:ReadOnlyMode`, on by default in every environment) refuses every create / update / delete until you turn it off.

## What you can do

| Task | Where |
|---|---|
| Create a client from a template (web app, SPA, service, legacy username/password) with the Clopay defaults | **New client** |
| Generate a new client secret - old ones keep working for 1 / 7 / 30 days, or are revoked at once | **Clients -> Rotate secret** |
| Edit every client setting, clone or delete a client | **Clients -> Edit** |
| Manage API resources, scopes and identity resources | **APIs & Scopes** |
| Review / revoke refresh tokens and consents | **Active sessions** |

Secrets are stored hashed (SHA-256, base64 - the same format as your SQL scripts). The plain text is shown **once**, right after it is generated.

## Layout

```
IDPAdminUI/
  src/Clopay.IDPAdminUI/       the app (Program.cs: ConfigureServices / ConfigurePipeline)
    Areas/AdminUI/             controllers + Razor views
    Core/                      BusinessLogic (services, DTOs, mappers), Auditing, Authorization, Filters, Middleware
    Infrastructure/Database/   EF Core: IdentityServer4 contexts, repositories
    Configuration/             settings classes + start-up checks
    Resources/                 English resource files
    wwwroot/                   CSS (clopay.css), JS bundle, fonts, logo
  tests/Clopay.IDPAdminUI.UnitTests/
  deploy/                      Publish-IIS.ps1 (build the package), Deploy-IIS.ps1 (safe in-place deploy)
  _designs/                    design notes, migration notes, read-only SQL login script
```

## Settings (`appsettings.<Environment>.json`)

`Local`, `Development`, `Test`, `Staging`, `Production` - chosen by `ASPNETCORE_ENVIRONMENT`.

| Key | Meaning |
|---|---|
| `ConnectionStrings:ConfigurationDbConnection` / `PersistedGrantDbConnection` | The IDP database. Use a **read-only SQL login** until you are ready to allow changes. |
| `AdminConfiguration:IdentityServerBaseUrl` | The IDP that signs administrators in, e.g. `https://test-iam.clopay.com` |
| `AdminConfiguration:ClientId` / `ClientSecret` | The IDP client used for sign-in: `admin_ui` (default) and its secret |
| `AdminConfiguration:IdentityAdminRedirectUri` | `https://<env>-iamui.clopay.com/signin-oidc` |
| `AdminConfiguration:AllowedAdmins` | Who may use the site (matches `preferred_username`, `name`, `email` or `sub`). Empty = nobody. |
| `AdminConfiguration:ReadOnlyMode` | `true` = view-only. Set `false` (env var `AdminConfiguration__ReadOnlyMode=false`) to allow changes. |

Any key can be set with an environment variable (`AdminConfiguration__ClientSecret`, `ConnectionStrings__ConfigurationDbConnection`, ...).
The site refuses to start while a `<PLACEHOLDER>` is left in a required setting. **Production values are placeholders** - none were available.

### First sign-in and the allow-list

1. Sign in. If your account is not listed you see "Access denied" with the exact values (`preferred_username`, `sub`, ...) to add.
2. Add one of them to `AllowedAdmins` (file or `AdminConfiguration__AllowedAdmins__0=...`) and restart the site.

### The sign-in client (reused from the old UI)

The old UI signs in with IDP client **`admin_ui`** (code flow + PKCE, scopes `openid admin_api admin_ui_profile offline_access`, redirect URI
`https://<env>-iamui.clopay.com/signin-oidc` - confirmed on Test). The new UI uses the same client and asks only for `openid` + `admin_ui_profile`,
so **no IDP or database change is needed**. You only supply the client **secret**: it is the `AdminUIClientSecret` environment variable in the old
site's `web.config` (server: `...\AdminUI\Ui\web.config`) - set it as `AdminConfiguration__ClientSecret`, never commit it.

For Development / Staging / Production the client id should be the same; verify by opening `https://<env>-iamui.clopay.com/auth` in a private window and
reading `client_id` in the address bar of the IDP page (override with `AdminConfiguration__ClientId` if it differs).

## Run locally

```bash
cd src/Clopay.IDPAdminUI
dotnet user-secrets set "ConnectionStrings:ConfigurationDbConnection" "<read-only connection string>"
dotnet user-secrets set "ConnectionStrings:PersistedGrantDbConnection"  "<read-only connection string>"
dotnet user-secrets set "AdminConfiguration:ClientSecret" "<client secret>"
dotnet run          # ASPNETCORE_ENVIRONMENT=Local, https://localhost:44303
```

The IDP client must allow `https://localhost:44303/signin-oidc` (use a "LocalDev" client like your other apps).

## Build, test, deploy

```powershell
dotnet test   tests\Clopay.IDPAdminUI.UnitTests
.\deploy\Publish-IIS.ps1 -Environment Staging       # Development | Test | Staging | Production
```

See [DEPLOY-IIS.md](DEPLOY-IIS.md).

## Safety rules

* The IDP database is the only data this tool touches, and only through the screens. It never runs migrations or seeds.
* Start every environment read-only, with a read-only SQL login (`_designs/Create-ReadOnly-Login.sql`); switch writes on per environment when ready.
* Every change is written to `Log\audit-YYYYMMDD.log` (who, when, what - never secret values).
* After changing clients the IDP app pool may need a restart, as with your SQL-script process - check with the IDP team whether that is still needed.

## Licence

MIT - see [../LICENSE.md](../LICENSE.md).
