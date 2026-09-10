import { ApiTransport } from '../../src/app/core/http/api-transport';
import { CandidateSearchService } from '../../src/app/features/search/services/candidate-search.service';
import type { SearchFilters } from '../../src/app/features/search/models/search.models';

/**
 * The service is now a thin, honest API client: it serializes the filter contract, applies
 * the paging defaults and propagates the caller's cancellation signal.
 *
 * The filter *semantics* are no longer tested here, and deliberately so. They are SQL now,
 * and they are proven against a real PostgreSQL instance in
 * `backend/Tests/IntegrationTests/SearchApiTests.cs`, where every family, both modes and the
 * no-duplicates guarantee are checked against a preserved transcription of the browser
 * evaluator this service replaces. Re-asserting them here would test a mock.
 */
describe('CandidateSearchService', () => {
  const page = {
    items: [
      {
        candidateId: 'c-1',
        firstName: 'Ana',
        lastName: 'Duplicada',
        phone: '+34 600 000 001',
        email: 'ana@ejemplo.test',
        status: 'available',
        hasPrimaryCv: true,
        primaryCvDocumentId: 'd-1',
        updatedAt: '2026-03-01T09:01:00Z',
      },
    ],
    page: 1,
    pageSize: 25,
    totalCount: 42,
  };

  const respond = (body: unknown = page) =>
    vi.fn().mockResolvedValue(
      new Response(JSON.stringify(body), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );

  const build = (fetcher: ReturnType<typeof vi.fn>) =>
    new CandidateSearchService(new ApiTransport('/api', 1_000, fetcher));

  const filters = (overrides: Partial<SearchFilters> = {}): SearchFilters => ({
    text: '',
    statusValues: ['available'],
    skillCriteria: [],
    skillMode: 'ANY',
    languageCriteria: [],
    languageMode: 'ANY',
    programCriteria: [],
    programMode: 'ANY',
    hasCv: '',
    ...overrides,
  });

  const sentBody = (fetcher: ReturnType<typeof vi.fn>) =>
    JSON.parse(fetcher.mock.calls[0][1].body as string);

  it('posts the complete filter contract to the search route', async () => {
    const fetcher = respond();
    const wanted = filters({
      text: 'Marta',
      skillCriteria: [{ value: 'Java', level: 'Avanzado' }],
      skillMode: 'ALL',
      languageCriteria: [{ value: 'Inglés', level: '' }],
      programCriteria: [{ value: 'Excel', level: 'Alto' }],
      programMode: 'ALL',
      hasCv: 'yes',
    });

    await build(fetcher).search(wanted);

    expect(fetcher.mock.calls[0][0]).toBe('/api/candidates/search');
    expect(fetcher.mock.calls[0][1].method).toBe('POST');
    // POST rather than GET precisely so the search term does not travel in a URL that
    // proxies and access logs record.
    expect(fetcher.mock.calls[0][0]).not.toContain('Marta');
    expect(sentBody(fetcher).filters).toEqual(wanted);
  });

  it('applies the documented paging defaults and passes an explicit page through', async () => {
    const withDefaults = respond();
    await build(withDefaults).search(filters());
    expect(sentBody(withDefaults)).toMatchObject({ page: 1, pageSize: 25 });

    const withPage = respond();
    await build(withPage).search(filters(), { page: 3, pageSize: 100 });
    expect(sentBody(withPage)).toMatchObject({ page: 3, pageSize: 100 });
  });

  it('returns the server page envelope rather than a bare list', async () => {
    const result = await build(respond()).search(filters());

    // The caller must be able to tell "25 shown" from "42 matched"; a bare array cannot.
    expect(result.totalCount).toBe(42);
    expect(result.page).toBe(1);
    expect(result.pageSize).toBe(25);
    expect(result.items).toHaveLength(1);
  });

  it('returns only the minimal projection the server sends', async () => {
    const result = await build(respond()).search(filters());

    expect(Object.keys(result.items[0]).sort()).toEqual(
      [
        'candidateId',
        'email',
        'firstName',
        'hasPrimaryCv',
        'lastName',
        'phone',
        'primaryCvDocumentId',
        'status',
        'updatedAt',
      ].sort(),
    );
  });

  it('propagates the caller cancellation signal to the network request', async () => {
    const controller = new AbortController();
    const fetcher = vi.fn(
      (_input: unknown, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) =>
          init?.signal?.addEventListener('abort', () =>
            reject(new DOMException('Aborted', 'AbortError')),
          ),
        ),
    );

    const request = build(fetcher as ReturnType<typeof vi.fn>).search(filters(), {
      signal: controller.signal,
    });
    controller.abort();

    // Really aborted at the network, not merely ignored once it answers: that is the whole
    // point of handing the signal down rather than dropping a stale promise.
    await expect(request).rejects.toMatchObject({ code: 'CANCELLED' });
  });

  it('does not load candidates or their aggregates', async () => {
    const fetcher = respond();

    await build(fetcher).search(
      filters({ skillCriteria: [{ value: 'Java', level: '' }], skillMode: 'ALL' }),
    );

    // The old implementation issued one request per candidate to read the collections the
    // list endpoint omits. Exactly one request, to the search route, is the cutover.
    expect(fetcher).toHaveBeenCalledTimes(1);
    expect(fetcher.mock.calls[0][0]).toBe('/api/candidates/search');
  });

  it('offers empty filters with every status selected', () => {
    const empty = build(respond()).emptyFilters();

    expect(empty.text).toBe('');
    expect(empty.statusValues).toEqual(['new', 'available', 'in_process', 'hired', 'rejected']);
    expect(empty.skillCriteria).toEqual([]);
    expect(empty.hasCv).toBe('');
  });
});
