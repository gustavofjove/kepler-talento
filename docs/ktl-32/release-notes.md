# KTL-32: Pre-fill a new candidate from a CV

«Nuevo candidato» can now read a CV and fill in the form for you.

**How it works.** Above the form, «Rellenar desde un CV» takes a PDF or DOCX of up to 20 MB. The
application reads the name, e-mail, phone, locality and province, and fills in **only the fields
that are still empty**. Anything already typed is never overwritten. Filled fields are
highlighted and marked «Sugerido del CV». Those the application is less sure of also say
«Revisar». Every value stays editable, and nothing is saved until you press «Guardar» as usual.

**What it does not do.** The CV is **not** attached to the new candidate. Upload it from the
candidate's page after saving, as before. Experience, education, skills and languages are not
read. A scanned CV without a text layer cannot be read; the page says so, and the form is filled
in by hand.

**Privacy.** The CV is checked by the antivirus before anything reads it. It is held in memory for
the request only: not stored, not logged, not audited. The audit trail records one
«CV leído para el alta de un candidato» event per attempt, with who did it and the outcome, and
nothing from the file.

**Permissions.** No new permission. The picker is part of the create page, which already requires
`candidates.create`, and the API checks the same permission.

**Reliability.** The extraction uses fixed rules, with no AI and no external service. How reliable
it is on real CVs is measured with [extraction-test-sheet.md](extraction-test-sheet.md) before
deciding whether a stronger approach is needed.

**Deployment.** No database migration. The API gains two NuGet packages, `PdfPig` and
`libphonenumber-csharp`, and an embedded list of Spanish municipalities. The optional `CvDraft`
settings have safe defaults. ClamAV must be reachable: while it is down, the picker says to try
again later, and creating a candidate by hand is unaffected.

Details: [cv-draft-contract.md](cv-draft-contract.md), [extraction-rules.md](extraction-rules.md).
