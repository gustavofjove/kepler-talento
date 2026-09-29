# KTL-32: CV draft contract

`POST /api/candidates/draft-from-document` reads a CV and returns suggested values for the
«Nuevo candidato» form. It stores nothing. See the requirements in
`openspec/changes/ktl-32-candidate-draft-from-cv/specs/candidate-cv-draft/spec.md` (and in
`openspec/specs/candidate-cv-draft/spec.md` once archived).

## Authorization

- Requires `candidates.create`. No new permission exists.
- The permission is checked before the request body is read, then again in the handler. An
  unauthenticated caller gets `401`, a caller without the permission gets `403`, and in both cases
  nothing is buffered, scanned or parsed.
- `documents.upload`, `candidates.read` and `candidates.update` do not grant access.

## Request

`multipart/form-data` with a single file part named `file`.

- Formats: **PDF** and **DOCX** only. The extension and the detected content must agree; the
  same inspector as document upload rejects macro-enabled and encrypted Office files.
- Size: at most 20 MB (the document limit). The request body cap is 21 MB, as for uploads.
- The body is read with a streaming multipart reader into an in-memory buffer. It never reaches
  the disk, the quarantine or the available storage.

## Processing order

1. Permission check.
2. Concurrency slot (`CvDraft:MaxConcurrent`, no queue). Parser work also holds its own slot
   until it really stops, so a parse abandoned at the time budget still counts.
3. Size, multipart and empty-file checks.
4. Extension allowlist, then content inspection (magic bytes and container structure only).
5. ClamAV scan of the in-memory bytes. Nothing is parsed unless the verdict is `Clean`.
6. Text extraction, bounded by `CvDraft:MaxPdfPages`, `CvDraft:MaxCharacters` and
   `CvDraft:TimeBudgetSeconds`.
7. Rule-based suggestions ([extraction-rules.md](extraction-rules.md)).

## Response `200 OK`

```json
{
  "draftId": "0199b5f4-3c2e-7a10-9d7e-6f3a2b1c0d4e",
  "outcome": "cv_draft.extracted",
  "fields": {
    "firstName": { "value": "Ana", "confidence": "high" },
    "lastName": { "value": "Ruiz Gil", "confidence": "high" },
    "email": { "value": "ana.ruiz@example.test", "confidence": "high" },
    "phone": { "value": "611 98 76 54", "confidence": "high" },
    "location": { "value": "Bilbao", "confidence": "high" },
    "province": { "value": "Bizkaia", "confidence": "high" }
  }
}
```

- `fields` holds only the keys with a suggestion, drawn from `firstName`, `lastName`, `email`,
  `phone`, `location`, `province`.
- `confidence` is `high` when a second signal corroborates the value, otherwise `low`.
- `outcome` is `cv_draft.extracted`, or `cv_draft.no_text` with empty `fields` when the file has
  no text layer (for example a scanned PDF).
- `Cache-Control: no-store`. No filename, text, storage key or path is ever returned.

## Refusals

Problem responses follow the usual contract (`code`, Spanish `detail`, `correlationId`). For
`400`, the specific code is in `errors[0].code`.

| Status | Code                           | When                                                                                    |
| ------ | ------------------------------ | --------------------------------------------------------------------------------------- |
| 400    | `cv_draft.file.missing`        | Not multipart, malformed multipart, or no `file` part                                   |
| 400    | `cv_draft.file.empty`          | The file is empty                                                                       |
| 400    | `cv_draft.size.exceeded`       | Over 20 MB                                                                              |
| 400    | `cv_draft.format.unsupported`  | Not `.pdf` or `.docx`                                                                   |
| 400    | `cv_draft.content.rejected`    | Content does not match the extension, or is a macro-enabled or encrypted DOCX           |
| 422    | `cv_draft.rejected`            | The scanner found malware. The message does not say so and no signature is disclosed    |
| 422    | `cv_draft.unreadable`          | The parser could not read the file (malformed PDF, encrypted or password-protected PDF) |
| 422    | `cv_draft.too_complex`         | Reading exceeded the time budget                                                        |
| 429    | `cv_draft.busy`                | All slots are in use. `Retry-After: 5`                                                  |
| 503    | `cv_draft.scanner_unavailable` | ClamAV errored, timed out or is unreachable. `Retry-After: 30`                          |

## Audit and logs

- Every attempt that reaches the handler records one `candidate.cv_draft.extracted` audit event:
  actor (internal user id), subject (the draft id), outcome (the outcome or refusal code) and
  correlation id. No candidate id, filename or value.
- The endpoint writes one log line per attempt: draft id, outcome code and elapsed milliseconds.
  Parser exceptions are never logged with their message, which can quote document content.

## Configuration

| Setting                     | Default | Range                                  |
| --------------------------- | ------- | -------------------------------------- |
| `CvDraft:MaxConcurrent`     | 4       | 1–32                                   |
| `CvDraft:MaxPdfPages`       | 5       | 1–50                                   |
| `CvDraft:MaxCharacters`     | 50000   | 1000–500000                            |
| `CvDraft:TimeBudgetSeconds` | 20      | 1–120 (the ClamAV timeout is separate) |

The API refuses to start with a value outside its range.
