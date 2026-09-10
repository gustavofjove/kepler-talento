# KTL-7 Access export procedure

The migration tool does **not** read `BBDD CVs.accdb`. It reads an export set an operator
produces from Access on Windows. The ACE/OLEDB provider that opens `.accdb` is
Windows-only and cannot be installed in the Linux containers CI runs in, so keeping it out
of the tool means continuous integration exercises the same reader production does.

The cost is one manual step, described here.

> **The export set is unprotected personal data.** It contains candidate names, contact
> details, notes, consent metadata and CV binaries in the clear. Produce it in a protected
> location, keep it there, and destroy it once a run reconciles — see
> [Destroying the export set](#destroying-the-export-set).

## Export set layout

```text
<export-root>/
  candidates.csv
  languages.csv
  programs.csv
  education.csv
  experience.csv
  skills.csv
  documents.csv
  files/            # CV binaries, referenced by documents.csv RelativePath
```

All seven CSV files must be present. A file with only a header row is valid and means the
entity contributed no rows; a missing file is a structural failure.

## File format

| Property      | Required value                                                                          |
| ------------- | --------------------------------------------------------------------------------------- |
| Encoding      | UTF-8. A byte-order mark is accepted and stripped.                                      |
| Delimiter     | Comma. RFC 4180 quoting: `"` doubles inside a quoted field.                             |
| Line ending   | `LF` or `CRLF`.                                                                         |
| Header        | Required, first line, exact column names from the tables below.                         |
| Column order  | Free. Columns are matched by name.                                                      |
| Extra columns | Rejected, so a renamed column is caught rather than silently dropped.                   |
| Dates         | `YYYY-MM-DD`. An empty field means the date is absent.                                  |
| Booleans      | `true` or `false`, lowercase.                                                           |
| Integers      | Digits only, no thousands separator.                                                    |
| Absent values | Empty field. The literal strings `NULL` and `#N/A` are **not** absent and are rejected. |

### Source keys

Every row carries a `SourceKey`: a stable identifier for that row in Access, unique within
its file. It is what makes a re-run correct rather than duplicate, and it is how the
reconciliation report cites a row without naming the person.

If an Access table has no natural key, add an AutoNumber column and export it. Do not
generate keys at export time from a row's position — a re-export after an edit would
renumber and the migration would treat corrected rows as new ones.

Child rows reference their candidate by `CandidateSourceKey`, matching a `SourceKey` in
`candidates.csv`.

## Columns

### `candidates.csv`

| Column         | Notes                                                                    |
| -------------- | ------------------------------------------------------------------------ |
| `SourceKey`    | Required, unique.                                                        |
| `FirstName`    | Required, non-empty.                                                     |
| `LastName`     | Required, non-empty.                                                     |
| `Phone`        | May be empty.                                                            |
| `Email`        | May be empty. Rejected if present and not a valid address.               |
| `Location`     | May be empty.                                                            |
| `Province`     | May be empty.                                                            |
| `Country`      | May be empty.                                                            |
| `Availability` | May be empty.                                                            |
| `Status`       | One of `new`, `available`, `in_process`, `hired`, `rejected`. See below. |
| `Source`       | May be empty.                                                            |
| `Notes`        | May be empty.                                                            |
| `ReceivedAt`   | Date. May be empty.                                                      |
| `ConsentAt`    | Date. **A row with no establishable consent date is rejected.**          |
| `ReviewDueAt`  | Date. May be empty.                                                      |
| `IsActive`     | Boolean. `false` carries the source's logical-removal state across.      |
| `DeletedAt`    | Date. Required when `IsActive` is `false`, empty when it is `true`.      |

`Status` is exported as one of the five codes, not as the Spanish label Access stores.
Map it in the export query, for example:

```sql
SWITCH(
  [Estado] = "Nuevo",       "new",
  [Estado] = "Disponible",  "available",
  [Estado] = "En proceso",  "in_process",
  [Estado] = "Contratado",  "hired",
  [Estado] = "Descartado",  "rejected"
) AS Status
```

An unrecognised status is rejected with `status.unknown` rather than being guessed.

### `languages.csv`

`SourceKey`, `CandidateSourceKey`, `Language`, `Level`, `Certification`, `Notes`

### `programs.csv`

`SourceKey`, `CandidateSourceKey`, `Program`, `Level`, `YearsExperience`, `Notes`

### `education.csv`

`SourceKey`, `CandidateSourceKey`, `EducationType`, `Degree`, `Specialty`, `Institution`,
`Status`, `EndYear`, `Notes`

`Degree` and `Institution` are required and non-empty.

### `experience.csv`

`SourceKey`, `CandidateSourceKey`, `Company`, `Position`, `Sector`, `Functions`,
`StartDate`, `EndDate`, `YearsExperience`, `IsCurrent`, `Notes`

`Company` and `Position` are required and non-empty. `EndDate` must be empty when
`IsCurrent` is `true`, and not earlier than `StartDate` otherwise.

### `documents.csv`

| Column               | Notes                                                            |
| -------------------- | ---------------------------------------------------------------- |
| `SourceKey`          | Required, unique.                                                |
| `CandidateSourceKey` | Required.                                                        |
| `RelativePath`       | Path under `files/`, forward slashes, no `..` and not absolute.  |
| `DocumentType`       | Free text, e.g. `CV`, `Carta`, `Titulación`.                     |
| `OriginalFileName`   | The filename to show the user. Never used as a storage location. |
| `ContentType`        | Media type, e.g. `application/pdf`.                              |
| `Sha256`             | Lowercase hex SHA-256 of the file. Verified after storage.       |
| `IsPrimary`          | Boolean. At most one `true` per candidate.                       |

Compute hashes on Windows with:

```powershell
Get-ChildItem -Recurse -File .\files |
  ForEach-Object {
    [pscustomobject]@{
      RelativePath = (Resolve-Path -Relative $_.FullName) -replace '^\.\\','' -replace '\\','/'
      Sha256       = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    }
  } | Export-Csv -NoTypeInformation -Encoding utf8 .\hashes.csv
```

Join `hashes.csv` onto the document rows to fill `Sha256`. The hash is what proves the
binary that reached private storage is the binary Access held.

### `skills.csv`

`SourceKey`, `CandidateSourceKey`, `Skill`, `Level`, `Notes`

## Catalog values

`Language`, `Level`, `Program`, `Skill`, `EducationType`, `Status` (in `education.csv`)
and `Sector` are exported as the free text Access holds. The migration resolves them
against existing catalog entries; it never creates one. Anything it cannot resolve is
reported, and the rows carrying it are rejected until an operator decides what the value
means — see the migration runbook.

## Producing the export

1. Work on a **copy** of `BBDD CVs.accdb`, never the file HR uses.
2. Create one export query per entity, producing exactly the columns above with exactly
   those names. Apply the `Status` mapping and the date formatting (`Format([Fecha],
"yyyy-mm-dd")`) in the query, not by hand afterwards.
3. Export each query with **External Data → Text File → Delimited**, comma-delimited,
   _Include Field Names on First Row_ checked, and code page **UTF-8**. Save with a
   `.csv` extension.
4. Copy the CV binaries into `files/`, preserving the relative paths `documents.csv`
   names.
5. Produce `hashes.csv` with the PowerShell above and fill in `Sha256`.
6. Verify the set opens as UTF-8 and that accented characters (`ñ`, `á`, `ü`) survived.
   A mojibake check here costs a minute; catching it after the load costs a rollback.

## Destroying the export set

After a run reconciles and the reconciliation report has been reviewed and filed, delete
the export root, including `files/` and any intermediate `hashes.csv`, and empty the
recycle bin. The migration cannot do this for you: it never learns where the export came
from beyond the directory it was pointed at, and deleting an operator's files is not its
business.

The report's closing checklist repeats this step.
