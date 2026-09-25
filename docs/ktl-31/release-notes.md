# KTL-31: Consistent tables

Every list now opens a record the same way, and all tables share one look.

**Click a row to open it.** In these lists, a click anywhere on a row opens its record:

- in Candidatos, the row opens the candidate page;
- in Posiciones, the row opens the position page;
- in Presets, the row opens the preset's edit page.

The same already applied to the advanced search results and to the two position candidate tables
added in KTL-30.

Links and controls inside a row keep working as before: the e-mail, dropdowns, «Eliminar» and the
selection checkbox in Candidatos never open the record. Ctrl/⌘-click or middle-click opens the
record in a new tab. The name or title is still a real link, so keyboard users can Tab to it and
press Enter.

**Buttons removed.** Every button that only opened the row's record is gone:

- «Abrir» in Candidatos;
- in Presets, the eye button and its criteria dialog, and «Editar».

**Preset criteria are shown inline.** Each preset now shows its search criteria on a full-width
line directly below its name and dates, as one row of chips like the ones in Búsqueda. A preset
without criteria shows «Sin filtros aplicados.». The presets table now sits in a panel like the
other lists.

**Criteria chips everywhere.** The read-only criteria summary now draws its values as chips on the
search page and the position page as well; before, they showed as plain text.
The dialog also showed the creation date, which the list no longer shows; the update and last-used
dates remain.

**Phones are plain text.** Candidate phones are no longer `tel:` links, in the candidate list and
on the candidate page header. E-mails remain mail links.

**One table style.** Candidatos, Posiciones, Presets, Catálogos, Usuarios, the search results and
both position candidate tables centre their cells vertically. Dropdowns, inputs and buttons inside
those tables use the compact 28px height, so one row never has controls of different heights.
Forms, dialogs and toolbars are unchanged.

**Catálogos: click a row to edit it.** A catalog value has no page of its own, so clicking its row
opens the inline editor directly, and «Editar» is gone. Keyboard users Tab to the value's name and
press Enter. While one value is being edited, clicks on other rows do nothing. «Subir» and «Bajar»
are now up and down arrow buttons. On a phone, the Catálogos page no longer scrolls sideways: the
family selector now shrinks to the available width.

**Usuarios** receives only the new style. Its rows are edited in place and have no page of their
own, so clicking a row does nothing.

**Not included (future work).**

- A user detail page, which would give Usuarios rows a destination.
- Merging the view and edit pages of positions and presets.

**Permissions and data.** Unchanged. A row opens only a page the user could already reach, behind
the same route guards: `candidates.read` for a candidate, `positions.read` for a position and
`presets.manage` for a preset. No new data is displayed: the preset criteria were already in the
list response. There is no backend or database change, and rollback is a normal revert of the SPA
build.
