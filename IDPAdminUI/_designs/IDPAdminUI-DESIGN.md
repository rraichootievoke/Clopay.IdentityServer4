# IDP Admin UI - design and migration notes

Written following the `dotnet-migrator` procedure of the APISecurity repo (understand -> plan -> confirm -> execute -> verify).
Approved by the user on 2026-11-27.

## Context

* The IDP is `Clopay.IdentityServer/src/api/AdminApi` (IdentityServer4 4.1.2, .NET Core 3.1) - not changed by this project.
* The old admin UI (`src/ui/AdminUi`) is the Identity Express Manager: binaries only, no source, not maintainable.
* Per environment IIS hosts: `<env>-iam` (IDP), `<env>-iamui` (this UI), `AdminAPI` (Identity Express API - left alone), `<env>-apigateway`.
  Staging server `MAS-API-03S`: site **AdminUI** -> `C:\Program Files\Rock Solid Knowledge\AdminUI\Ui` (the `Api` folder next to it belongs to the AdminAPI site).
* Clients used to be created with hand-written SQL scripts (`idpDBChanges/_templates/App-PKCE-Client.sql`); this UI replaces that process.

## Decisions

| Topic | Decision |
|---|---|
| Framework | .NET 8 (`net8.0`) - the Clopay standard; single project ("Option A") |
| Sign-in | Through the Clopay IDP (OIDC), reusing the old UI's IDP client `admin_ui` (observed on Test: code + PKCE, scope `openid admin_api admin_ui_profile offline_access`; we request `openid admin_ui_profile`). No STS. The IDP sends no role claim, so authorization is an allow-list (`AllowedAdmins`) |
| Database | The IDP database only. No migrations, seed, admin tables, audit tables or data-protection tables |
| Logs / audit | Serilog files: `Log\idpadminui-*.log`, `Log\audit-*.log`. Data-protection keys: `App_Data\DataProtection-Keys` |
| Safety | `ReadOnlyMode` (default true everywhere) refuses every non-GET request; SQL read-only login recommended first |
| Removed | STS, Users/Roles, Audit-log and Error-log pages, migrations/seed, themes, other languages, Skoruba.AuditLogging package (replaced by a file audit logger), NWebsec (replaced by a small middleware), PostgreSQL/MySQL, Admin.Api |
| Namespaces | `Clopay.IDPAdminUI.*`; folders mirror them so embedded `.resx` names stay valid |

## Structure map (old project -> new folder)

| Old project | New location |
|---|---|
| Admin.UI (controllers, views, helpers, resources, wwwroot) | project root (`Areas/`, `Helpers/`, `Resources/`, `wwwroot/`) |
| Admin.BusinessLogic (+ .Shared) | `Core/BusinessLogic` (+ `/Shared`) |
| Admin.EntityFramework (+ .Extensions, .Shared) | `Infrastructure/Database` (+ `/Extensions`, `/Shared`) |
| Admin (host, Startup) | `Program.cs` (`ConfigureServices` / `ConfigurePipeline`, no `Startup.cs`) |
| STS.Identity, Admin.BusinessLogic.Identity, Admin.EntityFramework.Identity/SqlServer/Configuration, Shared, Shared.Configuration | removed |

## Route inventory (before/after check)

Every action below existed before the restructure or was added deliberately (`ClientNew`, `ClientSecretRotate`); the area route
`{controller=Home}/{action=Index}/{id?}` is unchanged. Removed on purpose: all `Identity/*` and `Log/*` routes.

| Controller | Action | Verb |
|---|---|---|
| Account | AccessDenied | GET |
| Account | Logout | GET |
| Configuration | Client | GET |
| Configuration | Client | POST |
| Configuration | ClientNew | GET |
| Configuration | ClientNew | POST |
| Configuration | ClientSecretRotate | GET |
| Configuration | ClientSecretRotate | POST |
| Configuration | ClientClone | GET |
| Configuration | ClientClone | POST |
| Configuration | ClientDelete | GET |
| Configuration | ClientDelete | POST |
| Configuration | ClientClaims | GET |
| Configuration | ClientProperties | GET |
| Configuration | ApiResourceProperties | GET |
| Configuration | ApiScopeProperties | GET |
| Configuration | ApiScopeProperties | POST |
| Configuration | ApiResourceProperties | POST |
| Configuration | IdentityResourceProperties | GET |
| Configuration | IdentityResourceProperties | POST |
| Configuration | ClientProperties | POST |
| Configuration | ClientClaims | POST |
| Configuration | ClientClaimDelete | GET |
| Configuration | ClientPropertyDelete | GET |
| Configuration | ApiResourcePropertyDelete | GET |
| Configuration | ApiScopePropertyDelete | GET |
| Configuration | ApiScopePropertyDelete | POST |
| Configuration | IdentityResourcePropertyDelete | GET |
| Configuration | ClientClaimDelete | POST |
| Configuration | ClientPropertyDelete | POST |
| Configuration | ApiResourcePropertyDelete | POST |
| Configuration | IdentityResourcePropertyDelete | POST |
| Configuration | ClientSecrets | GET |
| Configuration | ClientSecrets | POST |
| Configuration | ClientSecretDelete | GET |
| Configuration | ClientSecretDelete | POST |
| Configuration | SearchScopes | GET |
| Configuration | SearchApiScopes | GET |
| Configuration | SearchClaims | GET |
| Configuration | SearchSigningAlgorithms | GET |
| Configuration | SearchGrantTypes | GET |
| Configuration | Clients | GET |
| Configuration | IdentityResourceDelete | GET |
| Configuration | IdentityResourceDelete | POST |
| Configuration | IdentityResource | POST |
| Configuration | ApiResource | POST |
| Configuration | ApiResourceDelete | GET |
| Configuration | ApiResourceDelete | POST |
| Configuration | ApiResource | GET |
| Configuration | ApiSecrets | GET |
| Configuration | ApiSecrets | POST |
| Configuration | ApiScopes | GET |
| Configuration | ApiScope | GET |
| Configuration | ApiScope | POST |
| Configuration | ApiScopeDelete | GET |
| Configuration | ApiScopeDelete | POST |
| Configuration | ApiResources | GET |
| Configuration | IdentityResources | GET |
| Configuration | ApiSecretDelete | GET |
| Configuration | ApiSecretDelete | POST |
| Configuration | IdentityResource | GET |
| Grant | PersistedGrants | GET |
| Grant | PersistedGrantDelete | GET |
| Grant | PersistedGrantDelete | POST |
| Grant | PersistedGrantsDelete | POST |
| Grant | PersistedGrant | GET |
| Home | Index | GET |
| Home | Error | GET |

## Completion checklist (dotnet-migrator, adapted to a UI app)

| Item | Status |
|---|---|
| Target `net8.0`, single project | done |
| No `Startup.cs`; `Program` is an explicit `sealed class` with `ConfigureServices` / `ConfigurePipeline` | done |
| All four environment settings files + Local | done (Production = placeholders) |
| Routes of kept screens unchanged | done (see inventory) |
| Business logic unchanged (moved, namespaces updated) | done; additions: secret rotation, quick-create, file audit logger |
| Security headers via middleware (`OnStarting`) | done |
| Builds, unit tests pass, runs against Test | **pending - first build is on the user's machine** |

## Known risks

* Not compiled where it was written (no .NET SDK there): expect a few compile errors on the first `dotnet build`.
* `IdentityServer4.EntityFramework` 4.1.2 on EF Core 8: expected to work (it did on EF Core 6) but untested.
* The IDP client used for sign-in must exist for each environment with the right redirect URI; changing it is a one-time SQL script run by an operator.
