import type { CandidateDocument } from '../../src/app/features/candidates/models/candidate.models';
import {
  isPreviewable,
  pickDefaultDocument,
  previewMessageKey,
} from '../../src/app/features/candidates/components/candidate-cv-preview.logic';

const document = (overrides: Partial<CandidateDocument> = {}): CandidateDocument => ({
  id: 'a',
  documentType: 'CV',
  originalFilename: 'cv.pdf',
  mimeType: 'application/pdf',
  sizeBytes: 10,
  isPrimary: false,
  uploadedAt: '2026-01-01T00:00:00Z',
  availabilityState: 'Available',
  ...overrides,
});

describe('candidate CV preview logic', () => {
  it('previews only available PDFs', () => {
    expect(isPreviewable(document())).toBe(true);
    expect(isPreviewable(document({ mimeType: 'application/msword' }))).toBe(false);
    expect(isPreviewable(document({ availabilityState: 'Pending' }))).toBe(false);
  });

  it('selects a previewable primary first', () => {
    const primary = document({ id: 'primary', isPrimary: true });
    expect(
      pickDefaultDocument([document({ id: 'newer', uploadedAt: '2027-01-01T00:00:00Z' }), primary]),
    ).toBe(primary);
  });

  it('selects the newest previewable document when the primary is not previewable', () => {
    const newest = document({ id: 'newest', uploadedAt: '2027-01-01T00:00:00Z' });
    expect(
      pickDefaultDocument([
        document({ id: 'primary', isPrimary: true, mimeType: 'application/msword' }),
        newest,
      ]),
    ).toBe(newest);
  });

  it('falls back to the primary and then undefined', () => {
    const primary = document({ isPrimary: true, availabilityState: 'Pending' });
    expect(pickDefaultDocument([primary])).toBe(primary);
    expect(pickDefaultDocument([])).toBeUndefined();
  });

  it('breaks equal-date ties by document id', () => {
    expect(pickDefaultDocument([document({ id: 'b' }), document({ id: 'a' })])?.id).toBe('a');
  });

  it.each([
    ['Pending', 'candidate.profile.preview.pending'],
    ['Error', 'candidate.profile.preview.error'],
    ['Refused', 'candidate.profile.preview.refused'],
    ['LegacyUnavailable', 'candidate.profile.preview.legacyUnavailable'],
  ] as const)('maps %s to its message', (availabilityState, key) => {
    expect(previewMessageKey(document({ availabilityState }))).toBe(key);
  });

  it('maps an available non-PDF to unsupported', () => {
    expect(previewMessageKey(document({ mimeType: 'text/plain' }))).toBe(
      'candidate.profile.preview.unsupported',
    );
  });
});
