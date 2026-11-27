-- =============================================================================
-- READ-ONLY SQL login for the Skoruba Admin UI (evaluation phase)
-- =============================================================================
-- Purpose : let the Admin UI BROWSE the IdentityServer configuration (Clients, secrets list,
--           API resources/scopes, grants) while the database itself refuses every write.
--           Edit / Add / Delete buttons in the UI then fail with a permission error instead of
--           changing anything. Switch the UI to a write-capable login only when you are ready.
-- Run     : by you / a DBA, once per environment, against that environment's SQL Server.
--           NOT run by any tool in this repo.
-- Replace : <<StrongPassword>>  and  <<IdentityServerDbName>>
--             Dev     IdentityServerDev
--             Test    IdentityServerDB_DBTest
--             Staging IdentityServerDB_DBStaging_New
--             Prod    identityserverdb
-- Idempotent: YES
-- =============================================================================

USE [master];
GO
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'idp_admin_ro')
    CREATE LOGIN [idp_admin_ro] WITH PASSWORD = N'<<StrongPassword>>', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
GO

USE [<<IdentityServerDbName>>];
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'idp_admin_ro')
    CREATE USER [idp_admin_ro] FOR LOGIN [idp_admin_ro];
GO
ALTER ROLE db_datareader ADD MEMBER [idp_admin_ro];
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::dbo TO [idp_admin_ro];
GO

-- Verify: expect db_datareader only, and the three DENYs
SELECT dp.name AS [user], r.name AS [role]
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members m ON m.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
WHERE dp.name = N'idp_admin_ro';
GO
-- Then run the UI with it:
--   .\Start-AdminLocal.ps1 -ConfigConnectionString "Data Source=<server>;Initial Catalog=<<IdentityServerDbName>>;User ID=idp_admin_ro;Password=<pw>;TrustServerCertificate=True;Max Pool Size=100"
