# KTL-16 deployment and recovery runbook

## Deploy

1. Configure the Entra authority, audience, client id, exposed API scope, and bootstrap administrator email/display name. Keep `DevelopmentActor:Enabled=false` and `Authentication:DevelopmentIssuer:Enabled=false` in Production.
2. Back up PostgreSQL using the KTL-5 operator runbook.
3. Run the migrator entry point as `ktl_migrator`. It creates `ADM_Roles` and `ADM_Users`, seeds five system roles, grants runtime `SELECT`, `INSERT`, and `UPDATE`, and refuses to finish if the installation has no active administrator.
4. Deploy the authenticated API, then the SPA.
5. Sign in with the configured bootstrap email. Confirm `GET /api/me`, user administration, role administration, and immediate permission revocation with the same token.

The API never migrates during normal startup. Do not enable the synthetic development actor to recover Production.

## Recover an administrator

Use this only when no active user holds an active role granting `users.manage`. Connect as `ktl_migrator`, record the incident/change reference, and run this transaction with the intended administrator's normalized email:

```sql
BEGIN;

UPDATE "ADM_Roles"
SET "IsActive" = TRUE, "UpdatedAtUtc" = NOW()
WHERE "Name" = 'rrhh_admin';

UPDATE "ADM_Users"
SET "RoleName" = 'rrhh_admin', "IsActive" = TRUE, "UpdatedAtUtc" = NOW()
WHERE lower("Email") = lower('<administrator-email>');

COMMIT;
```

Require exactly one updated user row. If none or more than one is reported, `ROLLBACK`, verify the address and unique index, and investigate rather than broadening the statement. Have that person sign in and verify `/api/me`; the existing external subject remains linked, or a bootstrap row links on first sign-in.

## Roll back

1. Roll back the SPA first. A previous SPA cannot authenticate against the new API, so do not leave mixed versions serving users.
2. Roll back the API image.
3. Only if the whole KTL-16 schema must be removed, run the EF migration rollback as `ktl_migrator` after a backup. Its `Down` drops `ADM_Users` and `ADM_Roles`, including role assignments.

Identity remains owned by Entra, but application role assignments are lost when the tables are dropped. A later roll-forward will JIT-provision returning subjects unless restored from backup.
