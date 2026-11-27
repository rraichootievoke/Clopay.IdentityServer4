# Deploying the IDP Admin UI to IIS

One IIS site per environment, in-process ASP.NET Core 8. It **replaces the old Identity Express UI site** and is deployed over that site's folder.

## How the servers look today (Staging example, server `MAS-API-03S`)

| IIS site | Role | Touch it? |
|---|---|---|
| `staging-iam.clopay.com` | the Clopay IDP (IdentityServer4) | **No** |
| `staging-apigateway.clopay.com` | Ocelot gateway | **No** |
| `AdminAPI` | Identity Express API (`...\AdminUI\Api`) | **No** - the new UI does not use it |
| **`AdminUI`** (`https staging-iamui.clopay.com:443`) | old Identity Express UI (`C:\Program Files\Rock Solid Knowledge\AdminUI\Ui`) | **Yes - this is the one we replace** |

Confirm the folder in IIS Manager: *AdminUI -> Basic Settings -> Physical path*. Other environments use the same shape with their own host names
(`dev-iamui`, `test-iamui`, `prod-iamui`).

## 1. Before the first deployment (once per environment)

1. **Server**: the ASP.NET Core **8** Hosting Bundle is installed (your .NET 8 APIs already need it). The `AdminUI` app pool: **.NET CLR version = No Managed Code**.
2. **Folder permissions**: give the app pool identity (`IIS AppPool\<pool name>`) **Modify** on the site's `Log` and `App_Data` folders (the deploy script creates them
   and never deletes them). Without `App_Data` the site fails to start with `500.30`.
3. **Sign-in client**: find the old UI's IDP client *before* replacing it - open `https://<env>-iamui.clopay.com/auth` in a private window and copy `client_id`,
   `redirect_uri`, `response_type`, `scope` from the address bar of the IDP page it redirects to. The client secret is the `AdminUIClientSecret` value in the old
   `web.config`. The redirect URI must be `https://<env>-iamui.clopay.com/signin-oidc` (the IDP client's own setting).
4. **SQL login**: ask the DBA for the read-only login (`_designs/Create-ReadOnly-Login.sql`) for that environment's IDP database.

## 2. Build

On a machine with the .NET 8 (or newer) SDK:

```powershell
.\deploy\Publish-IIS.ps1 -Environment Staging       # Development | Test | Staging | Production
```

It runs the unit tests, publishes, and writes `artifacts\<Environment>\IDPAdminUI` (+ a zip). Only that environment's settings file is in the package.

## 3. Settings to supply

Anything still shown as `<PLACEHOLDER>` must be supplied; the site refuses to start otherwise (the error names each missing setting). Set them as **app pool
environment variables** - unlike `web.config` they survive re-deploys:

```bat
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/applicationPools ^
  /+"[name='AdminUI'].environmentVariables.[name='AdminConfiguration__ClientId',value='<client id>']" /commit:apphost
```

| Variable | Value |
|---|---|
| `AdminConfiguration__ClientId` | the IDP client id from step 1.3 |
| `AdminConfiguration__ClientSecret` | its secret |
| `AdminConfiguration__AllowedAdmins__0` (`__1`, ...) | user names that may use the UI (see the README: first sign-in shows your values) |
| `ConnectionStrings__ConfigurationDbConnection` and `ConnectionStrings__PersistedGrantDbConnection` | only if you do not want the SQL login in the settings file (Production) |
| `AdminConfiguration__ReadOnlyMode` | leave **unset** (= true, view-only) until you are ready for changes |

## 4. Deploy

Copy `artifacts\<Env>\IDPAdminUI` to the server, then on the server (as administrator):

```powershell
# preview - changes nothing
.\Deploy-IIS.ps1 -PackagePath .\IDPAdminUI -TargetPath 'C:\Program Files\Rock Solid Knowledge\AdminUI\Ui' -AllowPlaceholders -WhatIf

# deploy: zips the old folder into ...\AdminUI\_backups, takes the site offline, replaces the files, brings it back
.\Deploy-IIS.ps1 -PackagePath .\IDPAdminUI -TargetPath 'C:\Program Files\Rock Solid Knowledge\AdminUI\Ui' -AllowPlaceholders -Clean `
    -CheckUrl https://staging-iamui.clopay.com/health
```

* `-Clean` makes the folder identical to the package (except `Log`, `logs`, `App_Data`), which is what you want when replacing a different application so old DLLs cannot conflict.
  Without it, files that are not in the package are kept.
* `-AllowPlaceholders` is needed while placeholders remain in the file (their values come from the environment variables above).
* The old `web.config` (with `UiUrl`, `AuthorityUrl`, `ApiUrl`, `AdminUIClientSecret`) is replaced; it stays in the backup zip.

## 5. Check, enable changes, roll back

1. `https://<env>-iamui.clopay.com/health` returns `Healthy` (also confirms the IDP database answers). `https://<env>-iamui.clopay.com/` redirects to the IDP sign-in.
2. Sign in. "Access denied" lists the values to add to `AllowedAdmins`.
3. Everything is **view-only** (gold *Read-only* badge in the header). To allow changes in that environment: set `AdminConfiguration__ReadOnlyMode=false` on the app pool and recycle it
   - and only then switch the SQL login to one that can write.
4. Logs: `Log\idpadminui-*.log` (application), `Log\audit-*.log` (every change: who, when, what).
5. Roll back: put `app_offline.htm` in the folder, extract the zip from `_backups\` over it, delete `app_offline.htm`.

## Order and approvals

Development -> Test -> Staging -> Production. Production only with a read-only login and explicit approval; its settings are placeholders until the DBA supplies the values.
