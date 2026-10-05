import type { ChipTone } from '../../shared/components/status-chip';
import type { PositionStatus } from './position.models';

const tones: Record<PositionStatus, ChipTone> = { open: 'success', closed: 'neutral' };

export function positionStatusTone(status: PositionStatus): ChipTone {
  return tones[status];
}
