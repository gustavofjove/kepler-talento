import type { APIRequestContext } from '@playwright/test';

export const SEARCH_CANDIDATE = { firstName: 'Laura', lastName: 'Garcia' };

interface CandidateSummary {
  id: string;
  firstName: string;
  lastName: string;
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
 */
export async function ensureSearchCandidate(request: APIRequestContext): Promise<void> {
  const candidate = (await find(request)) ?? (await create(request));
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

async function find(request: APIRequestContext): Promise<CandidateSummary | undefined> {
  const response = await request.get('/api/candidates?includeInactive=true');
  if (!response.ok()) {
    return undefined;
  }
  const candidates = (await response.json()) as CandidateSummary[];
  return candidates.find(
    (candidate) =>
      candidate.firstName === SEARCH_CANDIDATE.firstName &&
      candidate.lastName === SEARCH_CANDIDATE.lastName,
  );
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
