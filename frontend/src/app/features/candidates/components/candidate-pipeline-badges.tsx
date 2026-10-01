import { useTranslation } from 'react-i18next';
import type { CandidatePosition } from '../../positions/position.models';
import { pipelineBadges } from './candidate-pipeline-badges.logic';
import './candidate-pipeline-badges.css';

/** «En proceso» / «Contratado» beside the name, as text (KTL-36). Renders nothing without one. */
export function CandidatePipelineBadges({ links }: { links: readonly CandidatePosition[] }) {
  const { t } = useTranslation();
  const badges = pipelineBadges(links);
  if (!badges.length) return null;
  return (
    <span className="candidate-pipeline-badges" data-testid="candidate-pipeline-badges">
      {badges.map((badge) => (
        <span className="badge" key={badge} data-testid={`candidate-pipeline-${badge}`}>
          {t(`candidate.pipeline.${badge}`)}
        </span>
      ))}
    </span>
  );
}
