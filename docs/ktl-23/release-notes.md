# KTL-23 release notes

Frontend-only. No endpoint, contract, migration, grant or role changes.

- **Sub-pages show a breadcrumb trail above their heading.** Each trail is a navigation landmark
  named «Ruta de navegación»:

  | Page                     | Trail                                          |
  | ------------------------ | ---------------------------------------------- |
  | Candidate detail         | Candidatos › _full name_                       |
  | Candidate creation       | Candidatos › Nuevo candidato                   |
  | Candidate edit           | Candidatos › _full name_ › Editar              |
  | Position detail          | Posiciones › _title_                           |
  | Position creation / edit | Posiciones › Nueva posición / _title_ › Editar |
  | Preset creation / edit   | Admin › Presets › Nuevo preset / _preset name_ |

  The last segment is the current page and is not a link. «Admin» is plain text, because the Admin
  navigation parent has no destination.

- **Top-level pages have no trail.** This covers the Dashboard, the candidate, position and preset
  lists, Búsqueda, Catálogos, Usuarios, Roles, Importación and Auditoría. The primary navigation
  already marks them.
- **«Ver candidato» is gone from the candidate edit page.** The candidate's name in the trail
  links to the profile instead and keeps the `data-testid="candidate-edit-view"` the button had.
  The position form keeps its «Cancelar» button.
- **Links follow permissions.** A segment is a link only when the viewer may open its destination:
  `candidates.read` for «Candidatos» and a candidate's name, `positions.read` for «Posiciones» and
  a position's title, `presets.manage` for «Presets». Otherwise it is plain text. This is a
  convenience: the route guards and the API still enforce access.
- **Record labels show the saved value.** Editing a title or name does not change the trail until
  it is saved. While the record loads, and after a failed load, only the parent segments are shown.
- **List links do not restore filters.** «Candidatos» always opens `/app/candidates`, with no
  filters, sort or page. The browser's Back button still returns to the filtered view, because the
  list keeps its state in the URL.
- **Stable selectors for tests.** The trail carries `data-testid="breadcrumb"`. The list segments
  carry `breadcrumb-candidates`, `breadcrumb-positions` and `breadcrumb-presets`, and the position
  title on the edit form carries `breadcrumb-position`.
- **Personal data.** The candidate's name in the trail is the one already shown in the page heading.
  It is not copied into the URL, the document title or any log.
