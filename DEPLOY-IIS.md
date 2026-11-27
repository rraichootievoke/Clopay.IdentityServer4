# Deploying to IIS

Two sites are needed per environment: the **Admin** site (the screens) and the **STS** site (admin sign-in).
Both are ASP.NET Core 6 apps hosted in-process by IIS.

> **Check what is in the target folder first.** `Deploy-IIS.ps1` replaces files in place. Never point it at the folder of the
> Clopay IDP or any app still in use. Each of the two sites needs its **own** folder.

## 1. One-time server setup

1. Install the **ASP.NET Core 6.0 Hosting Bundle** (runtime 6.0.x). Run `iisreset` afterwards. The SDK is not needed on the server.
2. Create an app pool per site: **.NET CLR version = No Managed Code**, Integrated pipeline. Give the two sites separate pools.
3. Create the two sites with HTTPS bindings (suggested host names: `<env>-iamui.clopay.com` and `<env>-iamui-sts.clopay.com`).
4. Grant each app pool identity (`IIS AppPool\<pool name>`) **Modify** on its site folder - logs go to `Log\`, and the STS writes
   `tempkey.jwk` in non-production environments.
5. The admin database (`IdentityServerAdminDB`) must exist and be set up. Run `Start-AdminLocal.ps1 -Environment <Env>` once from a
   developer machine - it creates the tables and the `admin` user. **Change that password after the first sign-in**, and
   in the Admin go to *Clients -> `clopay_identity_admin` -> Rotate secret* to replace the default sign-in secret (step 3 below).
6. Production only: install the STS signing certificate in `LocalMachine\My`, give the STS app pool identity read access to its private
   key, and set its thumbprint (see `appsettings.Production.json`).

## 2. Build

On a machine with the .NET SDK (6 or newer) and this repo:

```powershell
.\deploy\Publish-IIS.ps1 -Environment Test        # Development | Test | Staging | Production
```

It runs the unit tests, publishes both apps and zips them into `artifacts\<Environment>\`. Each package contains only that
environment's settings (no other environments' SQL logins, no `Local` file, no seed file with a default password).

## 3. Settings that must be supplied

Anything still shown as `<PLACEHOLDER>` in the package must be provided. Set them as **app pool environment variables** - unlike
`web.config` they survive re-deploys:

```bat
%windir%\system32\inetsrv\appcmd.exe set config -section:system.applicationHost/applicationPools ^
  /+"[name='ClopayIamAdmin'].environmentVariables.[name='AdminConfiguration__ClientSecret',value='<secret>']" /commit:apphost
```

| Site | Variable | Value |
|---|---|---|
| Admin | `AdminConfiguration__ClientSecret` | the secret of the `clopay_identity_admin` client (see 1.5) |
| Both (Production) | `ConnectionStrings__...` | SQL connection strings, if not stored in the settings file |
| STS (Production) | `CertificateConfiguration__SigningCertificateThumbprint` | thumbprint from step 1.6 |

The sign-in client's redirect URLs must include the Admin site's real URL (`https://<host>/signin-oidc`,
`/signout-callback-oidc`); edit that client in the Admin if the host names differ from the proposals.

## 4. Deploy

Copy the package folders to the server (or run from a share), then on the server:

```powershell
# preview first - changes nothing
.\Deploy-IIS.ps1 -PackagePath .\Admin -TargetPath D:\Sites\iamui -AllowPlaceholders -WhatIf

# deploy: backs up the old files to D:\Sites\_backups, takes the site offline, copies, brings it back
.\Deploy-IIS.ps1 -PackagePath .\Admin -TargetPath D:\Sites\iamui     -AllowPlaceholders -Clean -CheckUrl https://test-iamui.clopay.com/
.\Deploy-IIS.ps1 -PackagePath .\STS   -TargetPath D:\Sites\iamui-sts -AllowPlaceholders -Clean -CheckUrl https://test-iamui-sts.clopay.com/.well-known/openid-configuration
```

* Without `-Clean`, old files that are not in the package stay (safest for a first look); with `-Clean` the folder is made identical to the
  package, except `Log`, `logs` and `App_Data`. Use `-Clean` when replacing a different application so old DLLs cannot conflict.
* Deploy the **STS first**, then the Admin.
* `-AllowPlaceholders` is only needed while placeholders remain in the settings (they are expected to come from environment variables).

## 5. Check and roll back

* STS: `https://<sts host>/.well-known/openid-configuration` returns JSON. Admin: `https://<admin host>/` redirects to the sign-in page.
* If it fails: look in the site's `Log\` folder and the Windows Event Log (IIS AspNetCore Module). To roll back, put `app_offline.htm` in the
  folder and extract the zip from `_backups\` over it, then delete `app_offline.htm`.
