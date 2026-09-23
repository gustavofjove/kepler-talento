# KTL-22 release notes

Frontend-only. No endpoint, contract, migration, grant or role changes.

- **The candidate detail page is read-only for every user.** It shows the core record,
  languages, programs, education, experience, skills, tags, custom notes and documents, with
  document download and the CV preview. It no longer offers add, remove, edit, retire, upload,
  set-primary or document-removal controls, whatever the viewer's permissions. The «Editar» link
  and «Baja lógica» / «Alta lógica» stay, for users with `candidates.update`.
- **All editing happens on the edit page** (`/app/candidates/:id/edit`): the core form under
  «Datos principales», then every section above, including tags, custom notes and document
  upload.
- **There are two save behaviours, and the page says so.** The core record is saved with
  «Guardar». Every other section saves each change immediately, as before. Saving the core record
  now stays on the edit page and confirms with «Datos principales guardados.», instead of
  navigating to the detail page. A section change followed by a core save does not conflict;
  someone else's concurrent edit is still refused with the Spanish conflict message.
- **Creating a candidate continues on its edit page.** `/app/candidates/new` shows only the core
  form and a note that the other sections become available after saving. After the first save
  the user lands on `/app/candidates/:id/edit`, or on the detail page if their role has
  `candidates.create` but not `candidates.update`.
- **Permission pairing.** Document upload is only reachable on the edit page, so in the SPA it
  requires both `documents.upload` and `candidates.update`. No seeded role is affected; see
  `docs/ktl-16/authentication-and-authorization.md`.
- The copy of `candidate-form.tsx`, `candidate-edit-page.tsx` and `candidate-documents.tsx` moved
  to `es.json` (`candidate.form.*`, `candidate.edit.*`, `candidate.profile.documents.*`) with the
  same Spanish text, and the three files left `LEGACY_HARDCODED_COPY`. The edit page's old
  subtitle («Los cambios quedan preparados para auditoría y RLS.») was replaced by the
  save-behaviour hint.
