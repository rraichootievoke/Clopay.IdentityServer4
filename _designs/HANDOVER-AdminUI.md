# Handover — Clopay IdentityServer Admin UI (IAM Admin)

Repo: `D:\Source\repos\clopay\Clopay.IdentityServer.Admin` · branch `feature/clopaychanges` · upstream open-source IdentityServer4 admin UI v2.1.0 (.NET 6, IS4 EF 4.1.2).
Written for the next Claude Code session. **No secrets in this file** — it only says where they live.

## 1. Goal and decision
The old admin UI (`APISecurity/Clopay.IdentityServer/src/ui/AdminUi`) is the commercial, closed-source *Identity Express Manager*; it no longer works and cannot be rebuilt from source. The user chose to **replace it with an open-source admin UI**, still on IdentityServer4 / older .NET, to manage **clients, secrets, API resources, scopes, identity resources and persisted grants** of the Clopay IDP. A from-scratch UI was considered and rejected (the open-source UI already matches the IDP's IS4 4.1.2 schema).
**Standing instruction from the user: do not disturb the existing databases. Start read-only; only after it looks right will editing be enabled.**

## 2. Status (2026-11-27)
**Done and verified locally — nothing deployed anywhere:**
- Admin and STS projects restore and build (`dotnet build -c Release`, SDK 9 targeting net6.0; ~35–45 s each).
- Logged in through the admin tool's STS and browsed the **real Test IDP database** (`IdentityServerDB_DBTest`) in headless Chrome, GET-only: Clients page = **76 clients** (matches SQL), search + open `CCAApp`, its Basics and Secrets pages render (secret values hidden), API resources 5 / scopes 4 / identity resources 6, persisted grants page loads.
- Test DB re-checked after the run: unchanged (same row counts, same 25 `__EFMigrationsHistory` rows, no `clopay%` rows).
- Schema compatibility: the IDP `Clients` table (see `APISecurity/Clopay.Databases/IdentityServerDb/dbo/Tables/Clients.sql`) matches the IS4 4.1.x EF model column-for-column. The DB also holds Identity Express extension tables (`Extended*`, SAML/WS-Fed, `Keys`, `AuditEntries`…) which the admin UI ignores.

**Not done:** any deployment (Dev/Test/Staging/Prod), real hostnames, real secrets, the read-only SQL login (script written, not run), cleaning committed secrets, committing anything.

## 3. How to run it locally
```powershell
cd D:\Source\repos\clopay\Clopay.IdentityServer.Admin
.\Start-AdminLocal.ps1                 # Test IDP DB, ~20 s after the first build
.\Start-AdminLocal.ps1 -ConfigConnectionString '<read-only conn string>'   # recommended
.\Start-AdminLocal.ps1 -Stop           # stop both apps
.\Start-AdminLocal.ps1 -Stop -Reset    # also drop the local LocalDB database
```
- UI: `https://localhost:44303` (redirects to the STS login on `https://localhost:44310`). The .NET dev certificate warning is expected.
- **Local login:** user `admin`, password = the admin tool's published default in `src/Clopay.IdentityServer4.Admin/identitydata.json` (it has **two** `$` characters). Local only. The old Identity Express login does not exist in this tool.
- The script reads the IDP connection string from `APISecurity/Clopay.IdentityServer/src/api/AdminApi/appsettings.<Env>.json` (key `IdentityServerDB`), keeps the tool's own data in a throwaway LocalDB `ClopayAdminLocal`, builds if `bin\Release` is missing, and refuses Production unless `-AllowProduction`. Logs/PIDs: `%TEMP%\ClopayAdminLocal`.
- It overrides settings with environment variables only (nothing is written into the repo): the six `ConnectionStrings__*`, `DatabaseMigrationsConfiguration__ApplyDatabaseMigrations=false`, `SeedConfiguration__ApplySeed=false`, the `AdminConfiguration__*` URLs and client secret.

## 4. Architecture rules learned (read before changing anything)
1. **The Admin app owns migrations and seed for all six EF contexts.** The STS does *not* migrate. On a fresh database start the **Admin first**, then the STS (the STS fails with SQL error 4060 otherwise).
2. The two apps use **different databases for different jobs**: the STS and the Admin's own users/logs/audit/data-protection live in a small *admin* database; only the Admin's `ConfigurationDbConnection` and `PersistedGrantDbConnection` point at the **real IDP database**.
3. **Never run the Admin with `ApplyDatabaseMigrations` or `ApplySeed` = true while its `ConfigurationDbConnection` points at the IDP DB.** the admin tool's migrations would collide with the existing tables and its seed would insert sample clients (`clopay_identity_admin`, …) into the real client table. The committed `appsettings.json` has both set to `true` with Test credentials — that is why the earlier attempt never worked, and why it must not be run as-is.
4. First run on an empty admin DB is the only time migrations/seed should be on, and then with **all six** connection strings on the admin DB (what `Start-AdminLocal.ps1` does against LocalDB).
5. the admin tool's **Users/Roles pages manage the admin tool's own users, not Clopay dealer users** (the IDP user store is the Identity Express schema, `IdentityExpress.Identity`). Do not point the Identity context at the IDP user tables.
6. The Admin authenticates through the STS (client `clopay_identity_admin`, role claim `ClopayIdentityAdminAdministrator`). Using the Clopay IDP as the login authority was rejected: it is circular (can't fix the IDP if it is down) and would need an IDP code change/deploy to emit the admin role.

## 5. Files
| Path | State |
|---|---|
| `Start-AdminLocal.ps1` | **new, untracked** — the launcher above |
| `_designs/Create-ReadOnly-Login.sql` | **new, untracked, NOT run** — creates `idp_admin_ro` (`db_datareader` + DENY INSERT/UPDATE/DELETE/ALTER/EXECUTE on `dbo`). Placeholders `<<StrongPassword>>`, `<<IdentityServerDbName>>` (Dev `IdentityServerDev`, Test `IdentityServerDB_DBTest`, Staging `IdentityServerDB_DBStaging_New`, Prod `identityserverdb`) |
| `_designs/HANDOVER-AdminUI.md` | this file |
| `src/Clopay.IdentityServer4.Admin/appsettings*.json`, `src/Clopay.IdentityServer4.STS.Identity/appsettings*.json` | Clopay's earlier edits (committed in `f8076bc0`) — see §6 |
Pre-existing, not from this work: modified files under `tests/…UnitTests/` and an untracked `tests/…/coverage/` folder.

## 6. Problems found in the existing configuration
- **Committed secret:** the Test DB password is in `src/Clopay.IdentityServer4.Admin/appsettings.json` and `src/Clopay.IdentityServer4.STS.Identity/appsettings.json` (HEAD `f8076bc0`). The repo's only remote is `upstream` (GitHub the admin UI) — do **not** push; strip the values (use environment variables / user-secrets / the launcher) and rewrite history first if it ever goes to Azure DevOps.
- Every per-environment file (`appsettings.{Development,Test,Staging,Production}.json`, both apps) still has `https://TODO-<Env>-iamadmin.clopay.com` / `…-iamadmin-sts.clopay.com` hostnames, and the Admin files have a placeholder `ClientSecret` (`<ADM…>`). With a placeholder secret the login fails with `invalid_client`; with a TODO hostname the Admin returns 500 (OIDC discovery cannot resolve the STS). The launcher overrides all of these for local use only.
- `SeedConfiguration.ApplySeed` and `DatabaseMigrationsConfiguration.ApplyDatabaseMigrations` are `true` in the committed `appsettings.json` (see §4.3).
- `IdentityServerAdminDB` (the intended admin DB) does not exist on the Test SQL server; nothing was created there.

## 6a. Related security items outside this repo (the user should know)
- `APISecurity/Clopay.IdentityServer/src/ui/AdminUi/web.config` contains a plaintext `AdminUIClientSecret` for Production — remove/rotate once the old UI is retired.
- The Identity Express default admin account (published vendor default, `info@rocksolidknowledge.com`) probably exists in the IDP user database. After the new UI is in use, verify and disable it or change its password. (The user pasted its credentials in chat; they are deliberately not repeated here.)

## 7. Next steps (suggested order) and open decisions
1. **User to confirm the local login works** (`admin` + the seed password, §3). If it fails: `-Stop -Reset`, run again.
2. **Read-only first:** the user (or a DBA) runs `_designs/Create-ReadOnly-Login.sql` per environment, then runs the launcher with `-ConfigConnectionString` for that login. Add/Edit/Delete buttons are still visible but the database refuses the writes.
3. **Deployment design (needs the user's decisions):**
   - Hostnames — suggested `<env>-iamadmin.clopay.com` (Admin) and `<env>-iamadmin-sts.clopay.com` (STS); Prod likely drops the prefix (user to confirm).
   - Two IIS sites per environment + the .NET 6 ASP.NET Core Hosting Bundle on the server (the admin UI is net6.0; the IDP is netcoreapp3.1).
   - A small admin DB per env (`IdentityServerAdminDB`), created by a DBA or by a one-time bootstrap with all connection strings on that DB (rule §4.4), then migrations/seed switched **off**.
   - A real admin user per env created through a custom seed (`identitydata.json` + `identityserverdata.json` need per-env redirect URIs and **no default password**), changed at first login.
   - Secrets (client secret, connection strings) supplied via IIS environment variables / Key Vault, never in git.
   - Start with **Dev**, then Test, Staging, and Production last (Production only with a read-only login and explicit approval).
   - Deploy with the existing `Tools/src/scripts/ManualDeployment` tool (add presets for the two sites; **exclude `App_Data`/`logs` from cleanup** so the STS signing key and key ring survive — same lesson as the IDP: STS uses `tempkey.jwk` in dev, configure a real signing key for deployed envs).
4. Clean the committed secrets and the `true`/`true` flags in `appsettings.json` (§6), then commit with the user's go-ahead.
5. Optional later: Clopay branding (`PageTitle`, favicon), enabling `Admin.Api` only if needed, CI pipeline (`pipelines/` is empty).

## 8. Ground rules and tooling notes
- **Never apply SQL to a shared DB yourself** — write the script and hand it over. Read-only `SELECT` checks against Test were used to verify and are acceptable. Do not commit or push unless asked; Production needs explicit confirmation.
- Windows PowerShell **5.1** (no `-SkipCertificateCheck`; use `curl.exe -k`; `&` does not wait for GUI executables). The Bash tool mangles backslashes in heredocs — write scripts with the Write tool.
- Installed: SDKs 9.0 and 10.0; ASP.NET Core runtimes 3.1, 5.0, 6.0, 8.0, 9.0, 10.0; LocalDB `MSSQLLocalDB`; Chrome and Node 24.
- **How UI work was verified:** headless Chrome started with `--remote-debugging-port`, driven from Node 24 using the global `WebSocket` and the DevTools protocol (navigate, fill the login form, read tables, screenshots). Stop the apps/browser and delete temp profiles afterwards (`-Stop -Reset`).
