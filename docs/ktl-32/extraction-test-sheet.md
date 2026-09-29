# KTL-32: Extraction reliability test sheet

KTL-32 exists mainly to find out whether rule-based extraction is reliable enough on real CVs, or
whether a later ticket needs OCR or a local model. This sheet explains how to measure it without
any CV or value leaving the operator's machine.

## 1. Assemble the corpus

1. Create `backend/Tests/CvCorpus/`. Git ignores the whole folder; check that
   `git status` does not list it before adding files.
2. Copy in 30–50 representative CVs as `.pdf` or `.docx`: different templates, one and two
   columns, compound given names, foreign phone numbers, scanned PDFs. Use short neutral names
   (`01.pdf`, `02.docx`).
3. Write `expected.json` with the true values, one object per file. Leave a field empty when the
   CV does not contain it:

   ```json
   [
     {
       "file": "01.pdf",
       "firstName": "…",
       "lastName": "…",
       "email": "…",
       "phone": "…",
       "location": "…",
       "province": "…"
     }
   ]
   ```

The CVs and `expected.json` are personal data. Never commit, share or attach them. Delete the
folder when the measurement is done.

## 2. Run it

From the repository root (Docker is not needed for this test):

```sh
dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter "Category=CvCorpus" --logger "console;verbosity=detailed"
```

The test runs the same readers and rules as the API, prints a table and writes it to
`backend/Tests/CvCorpus/report.md`. The table has counts only. The malware scan is skipped because
these are files the operator already holds locally.

Comparison ignores case, accents and hyphens. Phones are compared by digits, ignoring a leading
`34`.

## 3. Read the table

| Column                     | Meaning                                                          |
| -------------------------- | ---------------------------------------------------------------- |
| Expected                   | CVs where the field has a true value                             |
| Correct / Wrong            | A suggestion was made and it matched / did not match             |
| Missing                    | The field has a true value but nothing was suggested             |
| Spurious                   | Something was suggested for a field the CV does not contain      |
| High / Low correct / wrong | The same, split by confidence. `high wrong` should be close to 0 |

Also note the "no text layer" count: those CVs are the case for OCR.

## 4. Results

Fill in after each run. Counts only.

| Date      | Files | No text | Unreadable | Field     | Expected | Correct | Wrong | Missing | High wrong |
| --------- | ----: | ------: | ---------: | --------- | -------: | ------: | ----: | ------: | ---------: |
| _pending_ |       |         |            | firstName |          |         |       |         |            |
|           |       |         |            | lastName  |          |         |       |         |            |
|           |       |         |            | email     |          |         |       |         |            |
|           |       |         |            | phone     |          |         |       |         |            |
|           |       |         |            | location  |          |         |       |         |            |
|           |       |         |            | province  |          |         |       |         |            |

No real corpus was available when KTL-32 was implemented. The harness was checked against a
synthetic two-file corpus, and the numbers above are for the operator's first real run.

## 5. Decide

The pass mark is a product decision, taken on these numbers (design open question). A reasonable
starting point: e-mail and phone ≥ 90 % correct, name ≥ 70 % correct with `high wrong` near 0,
and a no-text share low enough that OCR is not the bigger win.
