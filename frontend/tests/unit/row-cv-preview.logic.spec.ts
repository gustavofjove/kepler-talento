import {
  animatesClose,
  CV_CLOSE_MS,
  CV_SPLIT_MIN_WIDTH,
  isOpenRow,
  isOpenRowGone,
  openKey,
  panelId,
  placementFor,
  type OpenRowCv,
} from '../../src/app/features/candidates/components/row-cv-preview/row-cv-preview.logic';

describe('row CV preview logic (KTL-35)', () => {
  const open: OpenRowCv = { tableId: 'matches', candidateId: 'c-1', name: 'Ana García' };

  it('shows the CV beside the table from the KTL-28 split width, below its row otherwise', () => {
    expect(CV_SPLIT_MIN_WIDTH).toBe(1360);
    expect(placementFor(1360)).toBe('side');
    expect(placementFor(1876)).toBe('side');
    expect(placementFor(1359.5)).toBe('inline');
    expect(placementFor(390)).toBe('inline');
  });

  it('keys an open CV by table and candidate', () => {
    expect(openKey('matches', 'c-1')).not.toBe(openKey('linked', 'c-1'));
    expect(panelId(open)).toBe('row-cv-matches-c-1');
    expect(isOpenRow(open, 'matches', 'c-1')).toBe(true);
    // The same candidate in the position's other table is a different row.
    expect(isOpenRow(open, 'linked', 'c-1')).toBe(false);
    expect(isOpenRow(null, 'matches', 'c-1')).toBe(false);
  });

  it('animates hiding only when motion is welcome and can be detected', () => {
    const prefers = (reduce: boolean) => ({
      matchMedia: (query: string) =>
        ({ matches: reduce && query.includes('reduce') }) as MediaQueryList,
    });
    expect(CV_CLOSE_MS).toBeGreaterThan(0);
    expect(animatesClose(prefers(false))).toBe(true);
    expect(animatesClose(prefers(true))).toBe(false);
    expect(animatesClose(undefined)).toBe(false);
    expect(animatesClose({} as Window)).toBe(false);
  });

  it('detects when the open row leaves its own table only', () => {
    expect(isOpenRowGone(open, 'matches', ['c-2'])).toBe(true);
    expect(isOpenRowGone(open, 'matches', [])).toBe(true);
    expect(isOpenRowGone(open, 'matches', ['c-2', 'c-1'])).toBe(false);
    // Another table's rows say nothing about this one.
    expect(isOpenRowGone(open, 'linked', [])).toBe(false);
    expect(isOpenRowGone(null, 'matches', [])).toBe(false);
  });
});
