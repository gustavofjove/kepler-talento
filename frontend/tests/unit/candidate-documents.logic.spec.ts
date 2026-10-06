import {
  canMarkPrimary,
  documentDetails,
  documentExplanationKey,
  documentStateChip,
  formatFileSize,
} from '../../src/app/features/candidates/components/candidate-documents.logic';

describe('document presentation', () => {
  it.each([
    ['Pending', 'pending', 'neutral', undefined],
    ['Refused', 'refused', 'danger', 'refused'],
    ['Error', 'error', 'danger', 'error'],
    ['LegacyUnavailable', 'legacy', 'neutral', 'legacy'],
    [undefined, 'legacy', 'neutral', 'legacy'],
    ['unknown', 'legacy', 'neutral', 'legacy'],
  ])('maps %s to its chip and explanation', (state, label, tone, explanation) => {
    expect(documentStateChip(state)).toEqual({
      labelKey: `candidate.profile.documents.state.${label}`,
      tone,
    });
    expect(documentExplanationKey(state)).toBe(
      explanation ? `candidate.profile.documents.explanation.${explanation}` : undefined,
    );
  });

  it('shows no chip or explanation for available documents', () => {
    expect(documentStateChip('Available')).toBeUndefined();
    expect(documentExplanationKey('Available')).toBeUndefined();
  });

  it('uses the extension or media subtype and only shows types other than CV', () => {
    const document = {
      originalFilename: 'resume.Pdf',
      mimeType: 'application/pdf',
      sizeBytes: 240_000,
      documentType: 'CV',
    };
    expect(documentDetails(document)).toEqual({
      format: 'PDF',
      sizeBytes: 240_000,
      type: undefined,
    });
    expect(
      documentDetails({ ...document, originalFilename: 'resume', documentType: 'Carta' }),
    ).toEqual({ format: 'PDF', sizeBytes: 240_000, type: 'Carta' });
  });

  it.each([
    [0, '1 kB'],
    [1, '1 kB'],
    [999, '1 kB'],
    [1000, '1 kB'],
    [1500, '2 kB'],
    [999_999, '1000 kB'],
    [1_000_000, '1,0 MB'],
    [1_250_000, '1,3 MB'],
  ])('formats %s bytes at the size boundaries', (bytes, expected) => {
    expect(formatFileSize(bytes as number)).toBe(expected);
  });

  it.each(['Available', 'Pending', 'Refused', 'Error', 'LegacyUnavailable', undefined] as const)(
    'allows primary designation only for available non-primary documents (%s)',
    (availabilityState) => {
      expect(canMarkPrimary({ availabilityState, isPrimary: false })).toBe(
        availabilityState === 'Available',
      );
      expect(canMarkPrimary({ availabilityState, isPrimary: true })).toBe(false);
    },
  );
});
