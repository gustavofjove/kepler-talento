import {
  reviewUrgency,
  showReception,
} from '../../src/app/features/candidates/components/candidate-main-panel.logic';
import { localDay } from '../../src/app/core/i18n/format';

const now = new Date(2026, 8, 30, 12);
const offsetDay = (days: number): string => {
  const date = new Date(now);
  date.setDate(date.getDate() + days);
  return localDay(date);
};

describe('candidate main panel dates', () => {
  it.each([
    [-1, 'overdue'],
    [0, 'dueSoon'],
    [30, 'dueSoon'],
    [31, null],
  ] as const)('classifies a review %i calendar days away', (days, expected) => {
    expect(reviewUrgency(offsetDay(days), now)).toBe(expected);
  });

  it('leaves empty and invalid review dates unmarked', () => {
    expect(reviewUrgency('', now)).toBeNull();
    expect(reviewUrgency('2026-02-30', now)).toBeNull();
  });

  it('hides a received day only when it matches the viewer local creation day', () => {
    const createdAt = '2026-09-30T01:00:00Z';
    const createdDay = localDay(new Date(createdAt));
    expect(showReception(createdDay, createdAt)).toBe(false);
    expect(showReception('2026-09-29', createdAt)).toBe(createdDay !== '2026-09-29');
    expect(showReception('', createdAt)).toBe(true);
  });

  it.skipIf(Intl.DateTimeFormat().resolvedOptions().timeZone !== 'America/Los_Angeles')(
    'uses the western local day for a UTC creation timestamp',
    () => {
      expect(localDay(new Date('2026-09-30T01:00:00Z'))).toBe('2026-09-29');
      expect(showReception('2026-09-29', '2026-09-30T01:00:00Z')).toBe(false);
      expect(reviewUrgency('2026-09-29', new Date('2026-09-30T01:00:00Z'))).toBe('dueSoon');
    },
  );
});
