# KTL-32: CV extraction rules

The rules are deterministic, local and free: no AI model, no OCR, no external service. They live
in `backend/Infrastructure/CvExtraction/RuleBasedCandidateDraftExtractor.cs` behind
`ICandidateDraftExtractor`, so a later ticket can replace them without touching the endpoint or
the SPA.

## Reading the text

- **PDF** (`PdfPig`): the first `MaxPdfPages` pages. Words are grouped into lines by baseline, top
  to bottom. A line is split again where the gap between two words is wider than six average
  character widths, so a sidebar does not fuse with the body. Each line keeps its largest point
  size.
- **DOCX** (`ZipArchive` + `XmlReader`, no Open XML SDK): header parts first, because many CV
  templates put the name and contact details there, then `word/document.xml`. One line per
  paragraph; `w:sz` gives the point size when present. DTDs are prohibited.
- A file with no letters in it (an image-only PDF) is `cv_draft.no_text`.

## Contact region

The first lines of page one: the first 30 % of them, and never fewer than 12. Names, places and
high-confidence phones come only from here. This is how an employer's address in the experience
section is kept from being suggested as the candidate's.

## Fields

| Field            | Rule                                                                                                                                                                                                                                                                                                                                                        | `high` when                                    |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| E-mail           | First address in document order                                                                                                                                                                                                                                                                                                                             | Always (it is syntactically exact)             |
| Phone            | First number `libphonenumber` finds as **valid**, Spain as default region. Spanish numbers in national format (`611 98 76 54`), others international (`+44 20 7946 0958`)                                                                                                                                                                                   | It is in the contact region                    |
| Name             | Contact-region lines of 2–6 words, all letters, none a heading, job title or label from the stop list, not made up entirely of place names. The largest point size wins, ties to the earliest line; without sizes, the earliest line                                                                                                                        | A name word appears in the e-mail's local part |
| First/last split | If the e-mail's first token is several name words run together (`juancarlos.perez`), that many words are the given name; if the e-mail spells out two given names (`maria.jose.lopez`) and there are four words, two. Otherwise Spanish convention: one given name for 2–3 words, two for 4 or more. Particles (`de`, `del`, `la`, …) stay with the surname | Same as name                                   |
| Province         | A Spanish postcode (`01000`–`52999`) in the contact region; its first two digits are the INE province code. Phone-like digit runs are removed first. Without a postcode, a province name in the contact region                                                                                                                                              | A postcode was found                           |
| Location         | A municipality on the postcode's line or the line either side, belonging to that province. Without a postcode, a municipality name that is unambiguous across Spain                                                                                                                                                                                         | The postcode's province agrees                 |

All matching is case- and accent-insensitive. Returned values keep the CV's own accents and case,
except a name written in capitals, which is returned in display case (`GARCÍA` → `García`).

Known weak spot: splitting given names from surnames when the e-mail does not help, for example
"Juan Carlos Pérez" with `contacto@…`. Such names are always `low` and marked «Revisar». The corpus
report quantifies it.

## Reference data

`backend/Infrastructure/CvExtraction/Resources/es-municipalities.csv` is the INE «Relación de
municipios y códigos por comunidades autónomas y provincias» at 1 January 2026 (8,132
municipalities), public data from the Instituto Nacional de Estadística (source: INE, www.ine.es).
Each row is the five-digit INE code (province + municipality) and the official name. The loader
expands INE's trailing articles (`Coruña, A` → `A Coruña`) and co-official names
(`Donostia/San Sebastián`), and adds a few everyday aliases (`Vitoria`, `Palma de Mallorca`,
`Castellón`). Province names and aliases are in `SpanishPlaces.cs`, spelled the way the
application's data already spells them (`Bizkaia`, `Gipuzkoa`, `Álava`).

To refresh it: download the new `diccionarioNN.xlsx` from INE (Relación de municipios y sus
códigos por provincias), export the `CPRO`+`CMUN` code and `NOMBRE` columns as `code;name`
(UTF-8, with a header line), replace the CSV, and run the `SpanishPlacesTests` unit tests.
