# Backup, Restore, And Rollback Runbook

## Scope

Operational runbook for staging/production backup, restore validation, and rollback of RRHH BBDD.

## Backup Procedure

1. Confirm no active schema migration is running.
2. Trigger managed PostgreSQL snapshot.
3. Export metadata for edge functions and environment variables.
4. Store backup references in release ticket (timestamp, actor, environment).

## Restore Validation

1. Restore snapshot into clean staging clone.
2. Run `npm run smoke:staging -- --strict` with clone environment variables.
3. Validate authentication, candidate list, search, import/export summaries.
4. Execute SQL checks:
   - `tests/security/rls-auth.sql`
   - `tests/security/storage-candidate-cvs.sql`

## Rollback Decision Criteria

Rollback when one or more occurs:

- Critical data corruption in candidate/profile/catalog entities.
- RLS/storage policies regress and expose unauthorized access.
- Edge functions fail contract checks or produce uncontrolled errors.

## Rollback Steps

1. Freeze writes in affected environment.
2. Re-deploy previous app artifact.
3. Restore previous database snapshot.
4. Re-deploy previous edge function bundle.
5. Re-run release gate checks:
   - `npm run build`
   - `npm test -- --runInBand`
   - `npm run test:integration`
   - `npm run security:rls`
   - `npm run security:storage`

## Post-Incident

1. Record timeline and impacted data scope.
2. Attach request_id/audit references from observability logs.
3. Create follow-up hardening tasks in backlog.
