import type { APIRequestContext } from '@playwright/test';

export const SEARCH_CANDIDATE = { firstName: 'Laura', lastName: 'Garcia' };

interface CandidateSummary {
  id: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  version: number;
  primaryDocumentId: string | null;
}

/**
 * Ensures the candidate the search, export and accessibility flows look for exists, with
 * the principal CV those flows filter on.
 *
 * These specs used to rely on `demo-1`, a candidate the browser seeded into
 * `localStorage` when it found the key empty. KTL-8 removed that seed deliberately: with
 * the API, an empty database means an empty list, and a fabricated row in a table of
 * personal data is a liability rather than a convenience. The specs that need a candidate
 * to find therefore create one, through the same API the application uses.
 *
 * Every step is idempotent and checked independently, because these specs share one
 * database with each other and with every previous run — including runs of older versions
 * of this helper, which is why the CV is ensured separately from the candidate.
 *
 * **Exactly one active match must survive.** Five specs call this, Playwright runs them in
 * parallel workers, and a plain check-then-create races: two workers both find nothing and
 * both create, leaving two Laura Garcias. The specs then fail on `text=Laura Garcia`
 * resolving to two elements, which reads as a search bug and is not one. Two things prevent
 * that: `globalSetup` calls this once before any worker starts, closing the window, and the
 * duplicate collapse below repairs a database an earlier run already polluted.
 */
export async function ensureSearchCandidate(request: APIRequestContext): Promise<void> {
  const candidate = await ensureExactlyOneActive(request);
  if (!candidate || candidate.primaryDocumentId) {
    return;
  }

  await request.post(`/api/candidates/${candidate.id}/documents`, {
    multipart: {
      file: {
        name: 'cv_laura_garcia.pdf',
        mimeType: 'application/pdf',
        buffer: Buffer.from('%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF'),
      },
      documentType: 'CV',
      isPrimary: 'true',
    },
  });
}

async function ensureExactlyOneActive(
  request: APIRequestContext,
): Promise<CandidateSummary | undefined> {
  const active = await findActive(request);
  if (active.length === 0) {
    return create(request);
  }

  // Keep the oldest so the surviving row is the one earlier runs attached a CV to, and
  // retire the rest through the same logical-removal route the application uses. Nothing
  // is physically deleted: that is the product's rule for candidate data, and it holds for
  // test data too.
  for (const duplicate of active.slice(1)) {
    await request.put(`/api/candidates/${duplicate.id}/active`, {
      data: { isActive: false, version: duplicate.version },
    });
  }
  return active[0];
}

async function findActive(request: APIRequestContext): Promise<CandidateSummary[]> {
  const response = await request.get('/api/candidates?includeInactive=true');
  if (!response.ok()) {
    // Treating "could not check" as "does not exist" would create a duplicate every time
    // the API hiccups, so an unreadable list yields nothing to retire and nothing to keep.
    return [];
  }
  const candidates = (await response.json()) as CandidateSummary[];
  return candidates
    .filter(
      (candidate) =>
        candidate.firstName === SEARCH_CANDIDATE.firstName &&
        candidate.lastName === SEARCH_CANDIDATE.lastName &&
        candidate.isActive,
    )
    .sort((left, right) => left.id.localeCompare(right.id));
}

async function create(request: APIRequestContext): Promise<CandidateSummary | undefined> {
  const response = await request.post('/api/candidates', {
    data: {
      ...SEARCH_CANDIDATE,
      phone: '+34 600 100 200',
      email: 'laura.garcia@example.invalid',
      location: 'Madrid',
      province: 'Madrid',
      country: 'España',
      availability: 'Inmediata',
      status: 'available',
      source: 'LinkedIn',
      notes: 'Perfil administrativo con experiencia internacional.',
      receivedAt: '2026-05-10',
      consentAt: '2026-05-10',
      reviewDueAt: '2027-05-10',
    },
  });
  return response.ok() ? ((await response.json()) as CandidateSummary) : undefined;
}
