import { positionStatusTone } from '../../src/app/features/positions/position-status.logic';
import {
  availabilityElapsed,
  availabilityTone,
  canReconfirm,
  initialForm,
  isLapsed,
  reconfirmInput,
  toRequest,
  undoInput,
} from '../../src/app/features/candidates/components/candidate-availability.logic';
import { pipelineBadges } from '../../src/app/features/candidates/components/candidate-pipeline-badges.logic';
import type { CandidatePosition } from '../../src/app/features/positions/position.models';

const unknown = { state: 'unknown' as const, checkedOn: '', until: '', checkedByDisplayName: null };
const unavailable = {
  state: 'unavailable' as const,
  checkedOn: '2026-09-01',
  until: '2026-09-30',
  checkedByDisplayName: 'Ana',
};

describe('candidate availability and pipeline helpers', () => {
  it('shows unchecked candidates without elapsed time', () => {
    expect(availabilityElapsed('unknown', null)).toBeNull();
    expect(availabilityElapsed('unknown', '2026-09-01')).toBeNull();
    expect(availabilityElapsed('available', null)).toBeNull();
    expect(availabilityElapsed('available', '2026-09-01', new Date('2026-09-01T12:00:00Z'))).toBe(
      'hoy',
    );
    expect(availabilityElapsed('unavailable', '2026-03-01', new Date('2026-09-01T12:00:00Z'))).toBe(
      'hace 6 meses',
    );
    expect(availabilityTone('unknown')).toBe('neutral');
    expect(availabilityTone('available')).toBe('success');
    expect(availabilityTone('unavailable')).toBe('danger');
    expect(positionStatusTone('open')).toBe('success');
    expect(positionStatusTone('closed')).toBe('neutral');
  });

  it('requires a new check after an until date lapses', () => {
    expect(isLapsed(unavailable, '2026-10-01')).toBe(true);
    expect(canReconfirm(unavailable, '2026-10-01')).toBe(false);
    expect(canReconfirm(unavailable, '2026-09-30')).toBe(true);
    expect(canReconfirm(unknown, '2026-10-01')).toBe(false);
  });

  it('reconfirms with today and undoes with the previous date', () => {
    expect(reconfirmInput(unavailable, '2026-10-01')).toEqual({
      state: 'unavailable',
      checkedOn: '2026-10-01',
      until: '2026-09-30',
    });
    expect(undoInput(unavailable)).toEqual({
      state: 'unavailable',
      checkedOn: '2026-09-01',
      until: '2026-09-30',
    });
    expect(undoInput(unknown)).toEqual({ state: 'unknown', checkedOn: '', until: '' });
  });

  it('drops hidden dates when resetting to unknown', () => {
    expect(toRequest({ ...initialForm(unavailable, '2026-10-01'), state: 'unknown' })).toEqual({
      state: 'unknown',
      checkedOn: '',
      until: '',
    });
  });

  it('derives both badges from position links without storing either on the candidate', () => {
    const link = (
      stage: CandidatePosition['stage'],
      positionStatus: CandidatePosition['positionStatus'],
    ): CandidatePosition => ({
      positionId: stage,
      title: stage,
      stage,
      positionStatus,
      addedAtUtc: '',
      updatedAtUtc: '',
      version: 1,
    });
    expect(pipelineBadges([link('interview', 'open'), link('hired', 'closed')])).toEqual([
      'inProcess',
      'hired',
    ]);
    expect(pipelineBadges([link('interview', 'closed'), link('rejected', 'open')])).toEqual([]);
  });
});
