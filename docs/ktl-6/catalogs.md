# KTL-6 catalogs

Business catalogs are owned by the API. The browser is no longer the system of record for
catalog vocabulary, and the SPA has no local fallback vocabulary.

## Endpoints

| Method | Route                                           | Capability        |
| ------ | ----------------------------------------------- | ----------------- |
| GET    | `/api/catalogs/{family}?includeInactive={bool}` | `catalogs.read`   |
| POST   | `/api/catalogs/{family}`                        | `catalogs.manage` |
| PUT    | `/api/catalogs/{family}/{id}`                   | `catalogs.manage` |
| PUT    | `/api/catalogs/{family}/order`                  | `catalogs.manage` |
| PUT    | `/api/catalogs/{family}/{id}/active`            | `catalogs.manage` |

There is deliberately **no `DELETE`**. A catalog value is retired by being deactivated, so
existing candidate records keep their meaning. The runtime database role is granted
`SELECT, INSERT, UPDATE` on `CAT_CatalogItems` and no `DELETE`, so the rule also holds at
the database.

`{family}` is one of the nine business families: `language`, `program`, `skill`,
`language_level`, `program_level`, `skill_level`, `education_type`, `education_status`,
`sector`. Any other value is rejected with `catalog.family.invalid`.

## Authorization

`catalogs.read` and `catalogs.manage` are enforced by the API for every operation, before
any data is read or written. Both map onto the single existing frontend `manage_catalogs`
permission, which gates navigation only — hiding the UI is not the control. As in KTL-5,
production still fails closed without a real actor; authentication remains deferred.

## Error codes

| Code                           | Status | Spanish message                                                                    |
| ------------------------------ | ------ | ---------------------------------------------------------------------------------- |
| `catalog.family.invalid`       | 400    | `La familia de catálogo no es válida.`                                             |
| `catalog.name.required`        | 400    | `El nombre es obligatorio.`                                                        |
| `catalog.name.duplicate`       | 400    | `Ya existe un valor con ese nombre.`                                               |
| `catalog.reorder.incomplete`   | 400    | `El nuevo orden debe incluir todos los valores de la familia.`                     |
| `catalog.not_found`            | 404    | `No se encontró el elemento del catálogo.`                                         |
| `catalog.concurrency.conflict` | 409    | `El valor ha cambiado desde que se cargó. Vuelva a cargarlo e inténtelo de nuevo.` |

## Name uniqueness

Within a family, names are unique once trimmed, case-folded, and compared without accents,
so `Inglés`, `ingles`, and `  INGLÉS  ` collide. The comparison form is stored in
`NameNormalized` and carries a unique index, so concurrent requests cannot both succeed.
Codes are likewise unique per family and are derived from the name when not supplied.

The accent fold is an explicit character table rather than Unicode decomposition: the API
image runs with `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`, where `string.Normalize` is a
silent no-op. Do not replace it with `NormalizationForm.FormD` unless ICU is added to the
runtime image.

## Concurrency

Writes carry the `version` the caller read. A write against a stale version is rejected
with `catalog.concurrency.conflict` and the stored value is not overwritten. Reordering is
submitted as the family's complete ordered id list and applied in one transaction, so
concurrent reorders cannot interleave into a corrupt order.

## Auditing

`catalog.created`, `catalog.updated`, `catalog.reordered`, and `catalog.activation_changed`
are written to `AUD_Events` in the same transaction as the change, with the acting actor,
the affected subject, and the request correlation id. Reads are not audited — catalog
values are business reference vocabulary, not personal data.

## Seeding

The nine families are populated by an explicit deployment-time seed that runs with
`--migrate`. It seeds a family only when that family holds no rows at all, so it is safe to
re-run and does not resurrect values an administrator deliberately retired. The application
never creates catalog values implicitly at runtime.

To reset a development environment's catalogs to the defaults, delete the rows and re-run
the migrator:

```powershell
'DELETE FROM "CAT_CatalogItems";' | docker compose exec -T -e PGPASSWORD=$pw postgres psql -U ktl_migrator -d kepler_talento
docker compose up -d --force-recreate migrator api
```

## Migration note

The `rrhh-catalogs` `localStorage` key is abandoned, not migrated. Catalog values were
per-browser and seeded from the same defaults, so the deployment seed is the new shared
baseline. Any per-browser edits a user made before this release are not carried over.

## Frontend contract

`CatalogService` exposes `status` (`idle` / `loading` / `loaded` / `error`) and `error`
alongside its data. `list()` and `activeNames()` stay synchronous and return `[]` while
loading, so every consuming component must branch on `status` rather than present an empty
option list as a complete result. `useCatalogStatus()` and `<CatalogStatusNotice />` carry
that branch. When the API is unreachable the screens report the failure and offer no
options; there is no local default vocabulary.

The transitional in-use pre-check in `CatalogService.toggleActive` was removed by KTL-8,
which moved candidate relations server-side. Deactivating a value candidates reference now
succeeds: the value stops being offered for new selections while the records that already
reference it keep resolving it. See `docs/ktl-8/candidates.md`.
