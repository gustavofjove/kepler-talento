# Contract: Export And Import

## Export Contract

### Purpose

Allow authorized RRHH users to export search results for internal work while
protecting candidate personal data and document storage details.

### Formats

- CSV
- XLSX if project dependencies and corporate pattern support it during planning
  or implementation

### Default Field Set

- First name
- Last name
- Phone
- Email when permitted
- Candidate status
- Location
- Province
- Reception date
- Last update date
- CV available indicator

### Forbidden Fields

- Internal storage paths
- Service credentials
- Permanent private document URLs
- Raw audit payloads
- Unrequested personal data outside the selected field set

### Required Events

Each export records requester, timestamp, filters, field set, row count, status,
and format.

## Import Contract

### Purpose

Load depurated data from Access or CSV exports into the normalized candidate
model.

### Accepted Source Classes

- Candidate master data CSV
- Catalog CSVs
- Candidate-language relations CSV
- Candidate-program relations CSV
- Education CSV
- Experience CSV
- Skill CSV
- Document metadata CSV

### Processing Order

1. Validate file structure.
2. Normalize catalog values.
3. Load catalogs.
4. Load candidates.
5. Load candidate relations.
6. Load document metadata if available.
7. Produce summary and row-level errors.
8. Validate representative candidates with RRHH.

### Error Handling

- Invalid rows are recorded with row number, source file, field, and reason.
- Valid rows may be loaded while invalid rows are reported.
- Dry-run mode validates without writing business records.
- Imports do not create public document links.

## Traceability

US6, US7; FR-019, FR-020, FR-021, FR-022, FR-023, SC-006, SC-007, SC-008.
