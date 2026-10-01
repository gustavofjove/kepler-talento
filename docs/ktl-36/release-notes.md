# KTL-36 release notes: candidate availability checks

Candidate status and the free-text availability field have been removed. A candidate now has a
single latest availability check: `unknown` (the default), `available`, or `unavailable`, with a
check date, an optional `until` date for `unavailable`, and the checker's display name. HR users
record or amend it on the candidate page; unrelated candidate edits do not refresh it. The page
also derives «En proceso» and «Contratado» badges from position links when the reader has
`positions.read`.

The candidate list and advanced search now filter by availability. Search accepts
`availabilityValues` and `availabilityCheckedFrom`; the sort field is
`availabilityCheckedOn`. Old list URLs with `sort=status` receive the sort-field validation
error. The export uses `disponibilidad` and `comprobado_el` instead of `estado`.

The migration rewrites saved presets and position requirements to filter schema version 2. It
discards any old status selection and makes availability unrestricted, so a previously restricted
saved search or position requirement can return more candidates. Other criteria, including
encrypted free text, are preserved. Review saved filters after deployment.

CSV import files must omit `status` and `availability`; files retaining either column are
refused with `import.column.unknown`. Imported candidates start `unknown`. Historical
`status.unknown` import outcomes remain readable, but new imports no longer produce that code.
The Access loader still accepts legacy status and availability columns and appends their contents
to candidate notes; loaded candidates start `unknown`.

The new `candidate.availability_checked` audit event records that a check happened without
recording the value or dates. Historical `candidate.status_changed` events remain readable.
