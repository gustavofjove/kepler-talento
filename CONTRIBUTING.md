# Contributing

This repository follows a Spec-Driven Development workflow.

## Workflow

1. Review the active specification in `specs/001-gestion-cvs-rrhh/spec.md`.
2. Check the technical plan in `specs/001-gestion-cvs-rrhh/plan.md`.
3. Select tasks from `specs/001-gestion-cvs-rrhh/tasks.md`.
4. Implement in a feature branch.
5. Open a pull request using the repository template.

## Quality Gates

Changes that affect behavior, security, data model, or user flows must update
the relevant Spec Kit artifact before implementation.

Expected validation, once implementation exists:

- unit tests;
- E2E tests;
- SQL/RLS checks;
- Storage-policy checks;
- lint and format checks.

## Data Safety

Do not commit Access databases, environment files, service-role keys, exported
candidate data, CV files, or private storage paths.
