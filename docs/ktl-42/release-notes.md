# KTL-42: Attach the CV used to pre-fill a new candidate

The CV picked in «Rellenar desde un CV» can now be attached to the new candidate when it is saved,
so it no longer has to be uploaded a second time from the candidate's page.

**How it works.** Once a CV has been read, the picker shows «Adjuntar este CV al candidato»,
ticked by default. On «Guardar», the candidate is created first, then the same file is uploaded as
its primary CV. The candidate's page then shows the document as pending until the antivirus scan
has finished, exactly like a manual upload. Clear the box to save without the CV. If several CVs
are picked, only the last one read is attached. A CV the application refused is never offered, but
a scanned CV without a text layer is: it fills no fields and can still be attached.

**When attaching fails.** The candidate is kept. The page still opens it and warns «El candidato
se ha guardado, pero el CV no se ha podido adjuntar. Súbelo desde la ficha del candidato.» If the
candidate itself cannot be saved, nothing is uploaded and the CV stays selected for another try.

**Permissions.** No new permission. The box appears only for users who hold `documents.upload`;
without it the page behaves as in KTL-32. The API checks `documents.upload` before reading the
upload, as it always has.

**Privacy and storage.** The CV is held only in the page's memory until the page is left. It
reaches the server through the ordinary document upload: quarantine, ClamAV scan to `Clean`,
private storage under an opaque key, permission-checked download, and the usual upload audit. The
draft endpoint (`POST /api/candidates/draft-from-document`) is unchanged and still stores nothing,
so a CV that is attached is scanned twice, once to read it and once to store it.

**Deployment.** Frontend only. No API contract, database migration, grant, configuration or
dependency change.

Background: [KTL-32 release notes](../ktl-32/release-notes.md).
