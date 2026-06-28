# Security & Data Requirements Checklist: Gestion de CVs para RRHH

**Purpose**: Validate security, privacy, and data-governance requirement quality before implementation
**Created**: 2026-06-28
**Feature**: [spec.md](../spec.md)

## Requirement Completeness

- [x] CHK001 Are authentication requirements defined for all protected candidate data? [Completeness, Spec FR-001]
- [x] CHK002 Are role distinctions documented for administration, RRHH operation, limited read, readonly, and technical administration? [Completeness, Spec FR-002]
- [x] CHK003 Are document privacy requirements defined for CV upload, metadata, opening, and export? [Completeness, Spec FR-010-FR-012]
- [x] CHK004 Are export data-minimization requirements documented? [Completeness, Spec FR-019-FR-020]
- [x] CHK005 Are Access/CSV migration boundaries documented so Access cannot become a runtime backend? [Completeness, Spec FR-022-FR-023]

## Requirement Clarity

- [x] CHK006 Is "secure CV opening" clarified as temporary or permission-checked access instead of a permanent URL? [Clarity, Spec FR-011]
- [x] CHK007 Are forbidden data exposures explicitly named, including storage paths and service credentials? [Clarity, Spec FR-012]
- [x] CHK008 Is logical deactivation distinguished from destructive deletion? [Clarity, Spec FR-003, Data Protection & Access]
- [x] CHK009 Are ANY and ALL search semantics explicitly defined for languages and programs? [Clarity, Spec FR-016]

## Requirement Consistency

- [x] CHK010 Are candidate document requirements consistent between user stories, functional requirements, and data protection rules? [Consistency, Spec US4, FR-010-FR-012]
- [x] CHK011 Are export requirements consistent with data protection rules and success criteria? [Consistency, Spec US6, FR-019-FR-020, SC-006]
- [x] CHK012 Are import requirements consistent with the assumption that Access is reference/migration-only? [Consistency, Spec US7, FR-022-FR-023]

## Acceptance Criteria Quality

- [x] CHK013 Are unauthorized access outcomes measurable and tied to acceptance criteria? [Acceptance Criteria, Spec SC-004]
- [x] CHK014 Are search correctness outcomes measurable for duplicate prevention and filter behavior? [Acceptance Criteria, Spec SC-002-SC-003]
- [x] CHK015 Are migration validation outcomes measurable with representative candidate checks? [Acceptance Criteria, Spec SC-007-SC-008]

## Scenario Coverage

- [x] CHK016 Are exception scenarios covered for unauthorized users, invalid files, duplicate-prone records, empty exports, and bad imports? [Coverage, Spec Edge Cases]
- [x] CHK017 Are recovery or review scenarios covered for overdue CV review dates and logical deactivation? [Coverage, Spec Edge Cases, Data Protection & Access]
- [x] CHK018 Are negative security scenarios covered for inactive users and invalid roles? [Coverage, Spec Edge Cases]

## Dependencies & Assumptions

- [x] CHK019 Are language, MVP scope, Access-source, and document-scope assumptions documented? [Assumption, Spec Assumptions]
- [x] CHK020 Are planning-level stack constraints separated from user-facing business requirements? [Consistency, Spec Assumptions, Plan Technical Context]
