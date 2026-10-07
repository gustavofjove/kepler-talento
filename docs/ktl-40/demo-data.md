# KTL-40 demo data

`scripts/seed-demo-data.js` loads a small, fixed, **fabricated** dataset into a local development
stack, so the home page («Inicio») and the position and search screens show realistic content.
It writes through the public API only, so validation, field encryption, permission checks and the
audit trail all apply as for data entered by hand.

```sh
cd frontend
npm run seed:demo                                    # http://localhost:4200 (or KTL_API_BASE)
npm run seed:demo -- --base-url http://localhost:4300
npm run seed:demo -- --no-cvs                        # skip the synthetic CV uploads
npm run seed:demo -- --remove                        # withdraw the dataset
```

The Compose stack must be running in Development (`docker compose up`).

## What it creates

| Records    | Content                                                                                                                                                                                                                                            |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Candidates | 24 with Spanish names and e-mails `nombre.apellido@demo.kepler-talento.local`. 22 stay active: 7 checked available and 5 unavailable (some with a «hasta» date), checks spread over the last 4 weeks, the rest unchecked. 2 are removed logically. |
| CVs        | A synthetic one-page PDF, uploaded as the primary CV, for 13 candidates. ClamAV scans them as usual; until it does they stay quarantined.                                                                                                          |
| Positions  | 7 open and 1 closed, with 26 candidate links spread over every stage. «Diseñador UX/UI» is open with no candidates.                                                                                                                                |
| Presets    | 4 shared presets. Three are used once, in a fixed order, on the run that creates them; «No disponibles con CV» is never used.                                                                                                                      |

The tables live in `scripts/seed-demo-data.lib.js`. `tests/unit/seed-demo-data.lib.spec.ts` checks
their invariants: the guard, distinct e-mails in the reserved domain, every stage present, and no
13-digit number anywhere.

## Re-running and removing

- **Idempotent.** Demo candidates are recognised by their e-mail domain, positions by their titles
  and presets by their names. A second run creates nothing and does not move the presets' last use.
- **`--remove`** removes the demo candidates logically, closes the demo positions and deletes the
  demo presets. It never deletes rows directly, and touches nothing outside the dataset.
- **Seeding after `--remove`** reactivates the candidates and reopens the positions meant to be
  active and open, and recreates the presets.
- **The e2e suite leaves it alone.** The Playwright teardown purges records carrying a 13-digit
  `Date.now()` marker; the dataset carries none.

The output is counts only; it never prints a name, an e-mail or an id.

## Guards

- The target must be `localhost`, `127.0.0.1` or `::1`; anything else is refused before a request
  is sent.
- It signs in only through `POST /api/dev/token` as the development administrator. That endpoint is
  never mapped in Production, so the script cannot write anywhere else.
- It needs no database credential, and it is not part of any image, migration or container
  start-up.

All of it is fabricated personal data. Never run it against a database that holds real candidates.
