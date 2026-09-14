# Contributing

This repository follows a spec-driven workflow based on [OpenSpec](openspec/installation.md).
The full rules for humans and coding agents alike are in [AGENTS.md](AGENTS.md); the
standing principles are in [openspec/config.yaml](openspec/config.yaml).

## Workflow

1. Describe the work as a ticket brief in `openspec/KTL-<n>.md` (optionally enrich it with
   `/enrich-us KTL-<n>`).
2. Create an OpenSpec change named `ktl-<n>-<slug>` (`/opsx:new` or `/opsx:ff`) and produce
   its proposal, delta specs, design and tasks.
3. Implement on a `feat/KTL-<n>` branch (`/opsx:apply`), then check the result against the
   artifacts (`/opsx:verify`).
4. Open a pull request against `main` using the repository template.
5. After merge, archive the change (`/opsx:archive`) so `openspec/specs/` reflects the
   delivered behavior.

The slash commands above are Claude Code's; in Codex use the equivalent skills
(`$enrich-us`, `$openspec-new-change`, `$openspec-ff-change`, `$openspec-apply-change`,
`$openspec-verify-change`, `$openspec-archive-change`).

Current requirements live in `openspec/specs/`. `specs/001-gestion-cvs-rrhh/` is the
historical design record of the original application and is not updated for new work.

## Quality gates

Changes that affect behavior, security, the data model or user flows must update the
relevant OpenSpec artifact before implementation. Before a pull request:

```sh
npm test               # frontend unit, integration and security specs
npm run test:backend   # xUnit (requires Docker for Testcontainers)
npm run e2e            # Playwright journeys affected by the change
npm run lint
npm run format:check
npm run security:rls
npm run security:storage
```

Slices that touch personal data, permissions, database grants or document storage must
include tests proving access fails closed for unauthenticated and unauthorized callers.

## Data safety

Do not commit Access databases, environment files, credentials, exported candidate data,
CV files, backups or private storage paths.
