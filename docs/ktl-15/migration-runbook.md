# KTL-15 migration runbook

1. Confirm a recent PostgreSQL backup and record the current migration id.
2. Verify the migrator uses `ktl_migrator`; the API must continue to use `ktl_runtime`.
3. Stop application writes and run the Web entry point with `--migrate`.
4. Verify `OPS_Positions`, its check constraints, B-tree and trigram indexes, and that
   `ktl_runtime` has only `SELECT`, `INSERT`, and `UPDATE` on the table.
5. Verify every system role has `positions.read`, only `rrhh_admin` and `rrhh_user` have
   `positions.manage`, and custom roles are unchanged.
6. Start the application and smoke-test list, create, update, close, and reopen.

For rollback, deploy the previous application first. Do not drop `pg_trgm`. Do not run the Down
migration when `OPS_Positions` contains data unless the data owner explicitly approves the loss;
prefer keeping the forward-compatible table while the previous application ignores it.
