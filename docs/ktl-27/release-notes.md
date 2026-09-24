# KTL-27: Candidate Competencias panel sharing the search rows

Skills, languages, programs and tags now look the same wherever they appear. Every page shows them
as one bordered row per family, in the order Habilidades, Idiomas, Programas, Etiquetas, with the
label on the same line as the chips. Search, saved presets, position requirements and both
candidate pages use the same layout component. The search skill row is now labelled `Habilidades`,
which is the only visible change on the search, preset and position pages.

On the candidate edit and detail pages the four families sit together in one `Competencias` panel.
Every panel on those pages now spans the full width, one below another:

- Edit: Datos principales, Competencias, Formación, Experiencia, Notas, Documentos.
- Detail: Datos principales, Auditoría, Competencias, Formación, Experiencia, Notas, Documentos,
  then the CV preview.

Adding a language, skill or program to a candidate now takes one step. The value is saved at once
with the lowest active level of its family, first by catalog order. Click the chip to change the
level, the certification of a language or the years of experience of a program. If a family has no
active level, its (+) is disabled and the panel says which family and why, so an entry is never
saved without a level.

For test authors, `tests/e2e/support/catalog-picker.ts` keeps its signatures. `addValue(page,
prefix, name, level)` now adds the value and then changes the level on the saved chip only when it
differs from the default. Section test ids (`candidate-languages` and so on) and picker test ids
are unchanged. The panel adds `candidate-competencies`.

This release changes only the frontend. The relation endpoints, permissions, PostgreSQL schema,
grants and private-document access remain unchanged.
