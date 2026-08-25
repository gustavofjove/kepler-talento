# KTL-5 validation baseline

Captured on 2026-08-25 immediately after creating `feat/KTL-5`, before runtime or
application implementation changes.

| Check                  | Result                       | Baseline observation                                                                                                                                   |
| ---------------------- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `git status --short`   | Expected planning files only | `openspec/KTL-5.md`, `openspec/kepler-talento-stack-blueprint.md`, and `openspec/changes/ktl-5-dotnet-infrastructure/` were untracked planning inputs. |
| `npm test`             | Failed: 136/137 tests passed | `candidate-relations.service.spec.ts` expected unaccented `valido`; the implementation returned the correct Spanish `válido`.                          |
| `npm run lint`         | Passed                       | ESLint returned no findings.                                                                                                                           |
| `npm run format:check` | Failed                       | Prettier reported 121 pre-existing files, including archived specifications and existing application source.                                           |

Final validation must distinguish new regressions from these captured baseline issues and
must not weaken Spanish-language assertions or reformat unrelated historical files merely
to hide baseline noise.
