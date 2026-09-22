# KTL-15 position management

## HTTP contract

The API exposes `GET /api/positions`, `GET /api/positions/{id}`, `POST /api/positions`, and
`PUT /api/positions/{id}`. There is no delete route. Lists default to open positions, page 1,
25 rows, and `updatedAt desc`; accepted status filters are `open`, `closed`, and `all`.
List rows deliberately omit descriptions, requirements, and candidate-derived facts.

Writes accept title, description HTML, location, and the complete candidate-search filter value.
Updates also require `status` and the last-read `version`. Stable failures use `position.*` codes,
including `position.not_found`, `position.title.conflict`, and
`position.concurrency.conflict`.

## Lifecycle and rich text

New positions are open. Closing or reopening preserves requirements and does not alter candidates
or presets. Description HTML is canonicalized server-side with this allowlist only: `p`, `br`,
`strong`, `em`, `ul`, `ol`, and `li`. No attributes, CSS, links, images, embedded content, SVG,
MathML, or scripts are retained.

Requirements are a copy of candidate-search filters with schema version 1. Applying or creating a
preset copies filters; no preset identifier is stored on a position.

## Permissions and live matching

`positions.read` permits list/detail reads and `positions.manage` permits create/update. Neither
implies the other or any candidate, preset, catalog, or document permission. Live matches are
requested through the existing candidate search only when the browser actor also has
`candidates.read`; positions never store candidate membership, counts, or snapshots.

Seeded roles receive `positions.read`; `rrhh_admin` and `rrhh_user` additionally receive
`positions.manage`. Custom roles are untouched.

## Troubleshooting

- A title conflict compares trimmed titles without case or accents.
- A 409 concurrency problem means the record must be reloaded before saving.
- Invalid stored requirements fail closed instead of becoming an empty search.
- Run migrations only through the explicit `--migrate` entry point before deploying the API.
