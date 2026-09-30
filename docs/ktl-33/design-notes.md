# KTL-33 — Design notes: search parity over encrypted fields

Text search and the last-name sort move from PostgreSQL into the API when the fields they read are
encrypted. Users must see the same results in the same order, so the in-memory matcher and comparer
reproduce what the database does today. These are the measured facts they are built on.

## Database collation

Measured on the Compose database (`postgres:17.6-alpine`, 2026-09-29):

| Setting          | Value        |
| ---------------- | ------------ |
| `datcollate`     | `en_US.utf8` |
| `datctype`       | `en_US.utf8` |
| `datlocprovider` | `c` (libc)   |

Alpine uses musl, whose `strcoll` does not implement locale collation. `ORDER BY` therefore sorts by
code point, even though the locale is named `en_US.utf8`. Probe result, ascending:

```
%y | Alvarez | Bravo | De Soto | Nuñez | O'Brien | Zapata | _x | alvarez | de la Torre | nunez | Álvarez | Ávila | Ñúñez | álvarez
```

Upper case sorts before lower case, and accented letters sort after `z`. **The in-memory comparer is
`StringComparer.Ordinal`.** UTF-8 byte order and UTF-16 ordinal order agree for every character
outside the surrogate range, and candidate names do not use surrogate pairs in practice. The parity
test still covers the order on an accented, mixed-case dataset.

If the database image or provider ever changes (for example to ICU), this order changes too, and the
comparer must change with it. The parity test fails in that case.

## `ILIKE` case folding

| Probe                                 | PostgreSQL | .NET `OrdinalIgnoreCase` |
| ------------------------------------- | ---------- | ------------------------ |
| `'ÁNGEL MUÑOZ' ILIKE '%ángel muñoz%'` | true       | true                     |
| `'Ángel' ILIKE '%angel%'`             | false      | false                    |
| `'STRASSE' ILIKE '%straße%'`          | false      | false                    |
| `'İstanbul' ILIKE '%istanbul%'`       | true       | **false**                |

Matching is case-insensitive, accent-sensitive and character-by-character, with no expansions. The
only difference is U+0130 (`İ`), which musl lower-cases to `i` and .NET does not. The matcher folds
each character with `char.ToLowerInvariant` and maps U+0130 to `i` explicitly.

`%` and `_` are escaped today so that they match literally. The in-memory match is a literal
substring search, so they need no treatment.
