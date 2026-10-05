import { useTranslation } from 'react-i18next';
import { StatusChip } from '../../../shared/components/status-chip';
import type { CandidateAvailabilityState } from '../models/candidate.models';
import {
  availabilityElapsed,
  availabilityLabel,
  availabilityTone,
} from './candidate-availability.logic';
import './availability-cell.css';

interface AvailabilityCellProps {
  state: CandidateAvailabilityState;
  checkedOn: string | null;
  testIdPrefix: string;
}

export function AvailabilityCell({ state, checkedOn, testIdPrefix }: AvailabilityCellProps) {
  const { t } = useTranslation();
  const elapsed = availabilityElapsed(state, checkedOn);
  return (
    <span className="availability-cell" data-testid={`${testIdPrefix}-cell`}>
      <StatusChip tone={availabilityTone(state)} testId={`${testIdPrefix}-chip`}>
        {availabilityLabel(state, t)}
      </StatusChip>
      {elapsed ? (
        <span className="availability-cell__elapsed" data-testid={`${testIdPrefix}-elapsed`}>
          {elapsed}
        </span>
      ) : null}
    </span>
  );
}
