@AGENTS.md

## Claude Code specifics

The rules above are shared with Codex. Put anything that applies to both tools in
`AGENTS.md`; this section is only for Claude Code behaviour.

- Slash commands: `/opsx:new`, `/opsx:continue`, `/opsx:ff`, `/opsx:apply`, `/opsx:verify`,
  `/opsx:sync`, `/opsx:archive` for the OpenSpec workflow, and `/enrich-us KTL-<n>` to turn
  a ticket brief into a complete user story before starting a change.
- Edit files with the Edit/Write tools, never through PowerShell content pipelines (see
  Environment gotchas).
- Personal settings go in `.claude/settings.local.json` (ignored by git), not in this file.
