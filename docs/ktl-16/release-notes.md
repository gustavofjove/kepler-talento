# KTL-16 release notes

Kepler Talento now authenticates callers with bearer tokens and resolves users, roles, and effective permissions in the API. User and role administration is persisted in PostgreSQL, permission changes apply on the next request, and users and roles are retired by deactivation rather than deletion.

The former browser-local identity data is deliberately not migrated. Custom roles and role assignments stored in `rrhh-demo-profile`, `rrhh-admin-users`, or `rrhh-admin-roles` are lost on first load of this release. The five roles seeded in `ADM_Roles` and subsequent API administration are the source of truth.

Application-owned password and MFA screens were removed. MFA and interactive authentication belong to the configured identity provider. Local development uses the bounded signed development-token flow documented in the README.
