import { useTranslation } from 'react-i18next';
import { POSITION_CANDIDATE_STAGES, type PositionCandidateStage } from '../position.models';

interface PositionStageSelectProps {
  value: PositionCandidateStage;
  /** The accessible name, which names the row it belongs to (e.g. «Estado de Ana García»). */
  label: string;
  disabled?: boolean;
  onChange: (stage: PositionCandidateStage) => void;
}

/** A native, labelled stage selector shared by the position and candidate pages (KTL-30). */
export function PositionStageSelect({
  value,
  label,
  disabled,
  onChange,
}: PositionStageSelectProps) {
  const { t } = useTranslation();
  return (
    <select
      className="position-stage-select"
      name="stage"
      data-testid="position-candidate-stage"
      aria-label={label}
      value={value}
      disabled={disabled}
      onChange={(event) => onChange(event.target.value as PositionCandidateStage)}
    >
      {POSITION_CANDIDATE_STAGES.map((stage) => (
        <option key={stage} value={stage}>
          {t(`positions.stage.${stage}`)}
        </option>
      ))}
    </select>
  );
}
