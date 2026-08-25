# Backup, restore y rollback

El procedimiento vigente de KTL-5 está en
[`ktl-5/operator-runbook.md`](ktl-5/operator-runbook.md). Sustituye el antiguo procedimiento
basado exclusivamente en snapshots Supabase para la nueva plataforma ASP.NET
Core/PostgreSQL/almacenamiento privado.

Las rutas heredadas que todavía usan Supabase no se modifican en KTL-5 y deben conservar
su backup existente hasta que su cambio de migración y corte quede validado.
