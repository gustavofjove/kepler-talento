## [original]

Attach the CV used by «Rellenar desde un CV» to the new candidate on save

Follow-up to KTL-32, which left this as an open question. Today a recruiter picks a CV on «Nuevo candidato» (`/app/candidates/new`) to pre-fill the form, saves, and then has to upload the same file again from the candidate page. The draft endpoint (`POST /api/candidates/draft-from-document`) scans, reads and forgets the file by design, and the hint already says «El CV no se adjunta al candidato», but users still expect the document to stay.

Proposal:

- The create page keeps the last `File` successfully read by the CV picker in memory (browser only). Picking another CV replaces it. A CV that could not be read (refused, timed out, not clean) is not kept.
- Under the picker, a checkbox «Adjuntar este CV al candidato», ticked by default, shown only once a CV has been read and only when the user has `documents.upload`. Hiding it is a courtesy; the API stays the control.
- When the form is saved and the candidate is created, and the box is ticked, the page uploads the kept file through the **existing** `POST /api/candidates/{candidateId}/documents` (`DocumentService.upload`, `documentType: 'CV'`, `isPrimary: true`), then opens the candidate page as today. The upload follows the normal document path: quarantine, ClamAV scan to `Clean`, permission check, private storage under an opaque key. The candidate page already shows the pending scan state.
- **No backend change** and no new endpoint. The draft endpoint keeps forgetting the file; the server never holds a CV before a candidate exists. The file is scanned twice (once to read, once to store), which is accepted.
- Partial failure: if the candidate is created but the upload fails (or the user lacks `documents.upload` and the API refuses), the candidate is **not** rolled back. The page still navigates to the candidate and shows a toast explaining the CV was not attached and can be uploaded from the candidate page.
- The picker hint text changes to reflect the new behaviour (the CV is attached if the box is ticked). Copy lives in `es.json`.
- Nothing about the file or its name is logged.

Acceptance in short: with the box ticked, saving creates the candidate and its primary CV document in one user action; with it unticked, behaviour matches KTL-32; a failed attach never loses the created candidate; users without `documents.upload` never see the box.

Out of scope: retaining the file server-side between draft and save, attaching CVs when editing an existing candidate, bulk creation from CVs, and changes to the extraction itself.
