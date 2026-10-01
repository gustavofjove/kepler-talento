import { formatElapsed } from '../../src/app/core/i18n/format';

describe('formatElapsed', () => {
  it('uses calendar days for today and yesterday', () => {
    const now = new Date('2026-10-01T12:00:00Z');
    expect(formatElapsed('2026-10-01', now)).toBe('hoy');
    expect(formatElapsed('2026-09-30', now)).toBe('ayer');
    expect(formatElapsed('2026-09-28', now)).toBe('hace 3 días');
  });

  it('does not move a stored day backward in a negative UTC offset', () => {
    const previousZone = process.env['TZ'];
    try {
      process.env['TZ'] = 'America/Los_Angeles';
      const now = new Date('2026-10-02T02:00:00Z'); // Local day is October 1.
      expect(formatElapsed('2026-10-01', now)).toBe('hoy');
      expect(formatElapsed('2026-09-30', now)).toBe('ayer');
    } finally {
      if (previousZone === undefined) delete process.env['TZ'];
      else process.env['TZ'] = previousZone;
    }
  });

  it('refuses an impossible calendar day', () => {
    expect(formatElapsed('2026-02-30', new Date('2026-03-01T12:00:00Z'))).toBe('');
  });
});
