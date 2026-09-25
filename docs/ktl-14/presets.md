# KTL-14 shared search presets

Saved searches are one organization-wide library. Administrators curate it in **Admin ›
Presets**; everyone who can search candidates applies it from **Búsqueda**. This document
supersedes the saved-search sections of [`docs/ktl-10/search.md`](../ktl-10/search.md); the
search contract itself is unchanged.

## Endpoints

| Method | Route                                  | Capability        | Result                                               |
| ------ | -------------------------------------- | ----------------- | ---------------------------------------------------- |
| GET    | `/api/search-presets`                  | `candidates.read` | every preset, ordered by folded name                 |
| GET    | `/api/search-presets/{id}`             | `candidates.read` | one preset; `404` if absent                          |
| POST   | `/api/search-presets`                  | `presets.manage`  | `201`; `400` validation; `409` name conflict         |
| PUT    | `/api/search-presets/{id}`             | `presets.manage`  | `200`; `400`; `404`; `409` name or version conflict  |
| DELETE | `/api/search-presets/{id}?version={n}` | `presets.manage`  | `204`; `400` bad version; `404`; `409` stale version |
| POST   | `/api/search-presets/{id}/use`         | `candidates.read` | `200` with the filters; records the last use         |

`presets.manage` corresponds to the frontend permission `manage_presets`, granted by default to
`rrhh_admin` and `system_admin`. `candidates.read` corresponds to `view_candidates`.

The two capabilities are independent. Managing does not imply reading: an actor holding only
`presets.manage` can create, change and delete presets but cannot list, retrieve or apply them,
so its Admin › Presets list shows a load error. The default administrative roles hold both.

Every route checks authentication and its own capability **before** the request is validated or
dispatched, so an unauthorized caller gets the same `403` whether its body is valid or not and
learns nothing about whether a preset exists. The route guard and hidden navigation entries in
the frontend are a convenience, not the control.

## Contract

Request bodies:

```json
POST { "name": "Java senior", "filters": { "…": "…" } }
PUT  { "name": "Java senior", "filters": { "…": "…" }, "version": 3 }
```

`filters` is the KTL-10 `SearchFilters` value and is normalized and validated exactly as a search
request is. Response:

```json
{
  "id": "01a09f9a-…",
  "name": "Java senior",
  "filters": { "…": "…" },
  "createdAt": "2026-09-14T11:08:28Z",
  "updatedAt": "2026-09-14T11:08:28Z",
  "lastUsedAt": null,
  "version": 1
}
```

No actor identity is ever returned: nothing records who wrote or used a preset.

| Code                                 | Status | Meaning                                                  |
| ------------------------------------ | ------ | -------------------------------------------------------- |
| `search_preset.name.required`        | 400    | blank name after trimming                                |
| `search_preset.name.too_long`        | 400    | more than 120 characters                                 |
| `search_preset.version.invalid`      | 400    | missing, zero or unparseable version on PUT or DELETE    |
| `search_preset.not_found`            | 404    | no preset with that identifier                           |
| `search_preset.name.conflict`        | 409    | another preset already has the name (case/accent folded) |
| `search_preset.concurrency.conflict` | 409    | the preset changed or was deleted since it was read      |

The two `409`s are distinguishable by code; the frontend shows a different message for each.

## Shared-library semantics

- **Names** are unique across the whole library, compared after trimming, lower-casing and
  folding accents (`CatalogName.Normalize`). `Inglés B2` and `ingles b2` are the same name. KTL-10
  folded case only, because a name was one person's private label; a shared vocabulary needs
  the catalog rule.
- **Concurrency.** Update and delete must send the `version` the caller read; a stale one is
  refused and nothing is stored. The version starts at 1 and advances only when the name or
  filters change.
- **Applying is not editing.** `POST …/use` records `lastUsedAt` and leaves `updatedAt` and
  `version` untouched, so a recruiter applying a preset never makes an administrator's open edit
  stale.

### Why an integer version rather than `xmin`

Catalog items, candidates and documents use PostgreSQL's `xmin` as their row version. `xmin`
changes on every update of the row — including recording a use — which would turn every apply
into a concurrency conflict for whoever is editing the preset. Presets therefore carry an
application-maintained `Version` column configured as an EF concurrency token. Every content
change goes through `SearchPreset.Update`, which is the only place it is incremented. Moving
last-use tracking to a separate table was the alternative; it would have added a table, a join
and a grant to preserve a convention.

### Physical delete

Presets are deleted physically. The no-destructive-delete rule protects candidates and catalog
items, whose history other data refers to. A preset is configuration referenced by nothing, and
not a personal-data record whose retention must be governed. Deletion still requires explicit
confirmation in the UI and the current version in the API. This is a documented exception; it
is not a precedent for candidate or catalog data.

## Data model

`ADM_SearchPresets` after the `ShareSearchPresets` migration:

| Column                | Shape                  | Note                                      |
| --------------------- | ---------------------- | ----------------------------------------- |
| `Id`                  | uuid, primary key      | opaque API identity                       |
| `Name`                | varchar(120), required | trimmed display name                      |
| `NormalizedName`      | varchar(120), required | folded uniqueness key                     |
| `Filters`             | jsonb, required        | the complete filter value                 |
| `FilterSchemaVersion` | integer, required      | currently 1                               |
| `CreatedAtUtc`        | timestamptz, required  | server-assigned                           |
| `UpdatedAtUtc`        | timestamptz, required  | advances on content changes only          |
| `LastUsedAtUtc`       | timestamptz, nullable  | last successful apply                     |
| `Version`             | integer, required      | optimistic-concurrency token, starts at 1 |

Constraints: `CK_ADM_SearchPresets_Name`, `_Filters`, `_FilterSchemaVersion`, `_Version`
(`Version >= 1`) and `_Timestamps` (`UpdatedAtUtc >= CreatedAtUtc`, and `LastUsedAtUtc`, when
present, `>= CreatedAtUtc`). `UX_ADM_SearchPresets_NormalizedName` is unique and is also the
listing index.

The runtime role keeps exactly `SELECT`, `INSERT`, `UPDATE` and `DELETE` on this one table, as
KTL-10 granted. KTL-14 changes no grant.

## Frontend

- **Admin › Presets** (`/app/admin/presets`, guarded by `manage_presets`): a list with name
  filter, sorting, pagination and delete, plus create (`/new`) and edit (`/:id/edit`) pages. There
  is no read-only page. Since KTL-31 each preset shows its criteria summary on a full-width line
  below its values, and a click on either line (or Enter on the name link) opens its edit page; the
  former eye button, criteria dialog and «Editar» button are gone
  ([KTL-31 release notes](../ktl-31/release-notes.md)). The editor warns that presets are visible
  to everyone with search access and must not contain personal data.
- **Búsqueda** only applies presets. It has no save, rename or delete control; holders of
  `manage_presets` see a link to the administration section.
- **One editor, one summary.** `SearchCriteriaForm` and `SearchCriteriaSummary`
  (`src/app/features/search/components/`) are the only criteria editor and read-only summary, and
  `SearchCriteriaDialog` is the one modal that shows a filter set; Búsqueda and positions are meant
  to reuse it with their own actions.
  The search page and the preset pages render the same components, so a change to either
  reaches every screen.

## Privacy

Preset names and free-text filters can contain personal data, and they are now visible to every
holder of `view_candidates` rather than to one owner. Mitigations: no actor identity is stored or
returned; names and filters are masked in logs by `PersonalDataRedactionEnricher`; the editor
warns administrators not to include personal data; and the existing private presets were
discarded rather than published (see below).

## Migration

`ShareSearchPresets` runs through `--migrate` under the migration role. In order, in one
transaction:

1. **Deletes every existing preset.** KTL-10 presets were private to their owners; their names
   and filters may hold search terms nobody meant to share, and names collide between owners.
2. Drops `OwnerId`, its check and the owner-scoped unique index.
3. Adds `Version` (default 1) with its check, and the library-wide unique index.
4. Relaxes the timestamp check so `LastUsedAtUtc` may be later than `UpdatedAtUtc`.

Grants are untouched. Re-running is a no-op through EF's migration history.

## Rollback

1. Restore the previous frontend and API binaries first. The KTL-10 build expects `OwnerId`, so
   it cannot run against the shared schema.
2. Then run the migration's down step, which empties the table and restores the owner-scoped
   shape. Shared presets have no owner to give back and are lost; **the private presets deleted
   by the up step cannot be recovered.**
3. If a record of the shared presets is wanted before rolling back, export `ADM_SearchPresets`
   under the migration role first.
4. Do not broaden grants or re-enable browser preset storage as a rollback shortcut.
