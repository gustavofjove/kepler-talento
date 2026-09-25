import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { formatDate } from '../../../core/i18n/format';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useRowLink } from '../../../shared/components/row-link';
import { PositionStageSelect } from '../../positions/components/position-stage-select';
import {
  isStaleLinkError,
  sortCandidatePositions,
  upsertCandidatePosition,
} from '../../positions/position-candidates.logic';
import type { CandidatePosition, PositionCandidateStage } from '../../positions/position.models';
import '../../positions/positions.css';
import { PositionPicker } from './position-picker';

interface CandidatePositionsPanelProps {
  candidateId: string;
  candidateIsActive: boolean;
}

type PanelStatus = 'loading' | 'ready' | 'error';

/**
 * «Posiciones»: every position the candidate is linked to, open ones first (KTL-30). Actions apply
 * immediately, so this panel takes no part in the page's per-panel edit mode (KTL-29).
 */
export function CandidatePositionsPanel({
  candidateId,
  candidateIsActive,
}: CandidatePositionsPanelProps) {
  const { t } = useTranslation();
  const { positionService, confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const canManage = usePermission('positions.manage');
  const [links, setLinks] = useState<CandidatePosition[]>([]);
  const [status, setStatus] = useState<PanelStatus>('loading');
  const [picking, setPicking] = useState(false);
  const rowLink = useRowLink();
  const generation = useRef(0);

  const reload = useCallback(async (): Promise<void> => {
    const current = ++generation.current;
    setStatus('loading');
    try {
      const loaded = await positionService.listForCandidate(candidateId);
      if (current !== generation.current) return;
      setLinks(loaded);
      setStatus('ready');
    } catch {
      if (current === generation.current) setStatus('error');
    }
  }, [candidateId, positionService]);

  useEffect(() => {
    void reload();
  }, [reload]);

  const fail = (error: unknown, fallbackKey: string): void => {
    notifyError(error, t(fallbackKey));
    if (isStaleLinkError(error)) void reload();
  };

  const changeStage = async (
    link: CandidatePosition,
    stage: PositionCandidateStage,
  ): Promise<void> => {
    try {
      const next = await positionService.changeStage(
        link.positionId,
        candidateId,
        stage,
        link.version,
      );
      setLinks((current) =>
        upsertCandidatePosition(current, {
          ...link,
          stage: next.stage,
          updatedAtUtc: next.updatedAtUtc,
          version: next.version,
        }),
      );
    } catch (error) {
      fail(error, 'candidate.profile.positions.stageError');
    }
  };

  const remove = async (link: CandidatePosition): Promise<void> => {
    const confirmed = await confirmDialogService.confirm({
      title: t('candidate.profile.positions.removeTitle'),
      message: t('candidate.profile.positions.removeMessage', { title: link.title }),
      confirmText: t('candidate.profile.positions.removeConfirm'),
      cancelText: t('candidate.profile.positions.cancel'),
      danger: true,
    });
    if (!confirmed) return;
    try {
      await positionService.removeCandidate(link.positionId, candidateId);
      setLinks((current) => current.filter((item) => item.positionId !== link.positionId));
    } catch (error) {
      fail(error, 'candidate.profile.positions.removeError');
    }
  };

  const linkedIds = useMemo(() => new Set(links.map((link) => link.positionId)), [links]);
  const sorted = useMemo(() => sortCandidatePositions(links), [links]);

  return (
    <article className="panel position-section" data-testid="candidate-positions">
      <div className="position-actions">
        <h2>{t('candidate.profile.positions.title')}</h2>
        {canManage && candidateIsActive ? (
          <button
            className="button secondary"
            type="button"
            name="addToPosition"
            data-testid="candidate-positions-add"
            onClick={() => setPicking(true)}
          >
            {t('candidate.profile.positions.add')}
          </button>
        ) : null}
      </div>
      {status === 'loading' && links.length === 0 ? (
        <p role="status">{t('candidate.profile.positions.loading')}</p>
      ) : status === 'error' ? (
        <p role="alert">{t('candidate.profile.positions.error')}</p>
      ) : links.length === 0 ? (
        <p className="muted" data-testid="candidate-positions-empty">
          {t('candidate.profile.positions.empty')}
        </p>
      ) : (
        <div className="table-wrap">
          <table className="position-links-table">
            <thead>
              <tr>
                <th>{t('candidate.profile.positions.column.position')}</th>
                <th>{t('candidate.profile.positions.column.status')}</th>
                <th>{t('candidate.profile.positions.column.stage')}</th>
                <th>{t('candidate.profile.positions.column.addedAt')}</th>
                <th>{canManage ? t('candidate.profile.positions.column.actions') : null}</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((link) => {
                const open = link.positionStatus === 'open';
                const editable = canManage && open;
                return (
                  // The whole row opens the position; the title is its keyboard link.
                  <tr
                    key={link.positionId}
                    className={open ? 'row-link-row' : 'row-link-row position-row-past'}
                    data-testid="candidate-position-row"
                    onClick={rowLink(`/app/positions/${link.positionId}`)}
                    onAuxClick={rowLink(`/app/positions/${link.positionId}`)}
                  >
                    <td>
                      <Link className="row-link" to={`/app/positions/${link.positionId}`}>
                        {link.title}
                      </Link>
                    </td>
                    <td>
                      <span className="badge">{t(`positions.status.${link.positionStatus}`)}</span>
                    </td>
                    <td>
                      {editable ? (
                        <PositionStageSelect
                          value={link.stage}
                          label={t('candidate.profile.positions.stageLabel', { title: link.title })}
                          onChange={(stage) => void changeStage(link, stage)}
                        />
                      ) : (
                        <span className="badge">{t(`positions.stage.${link.stage}`)}</span>
                      )}
                    </td>
                    <td>{formatDate(link.addedAtUtc, { dateStyle: 'medium' })}</td>
                    <td>
                      <div className="form-actions">
                        {editable ? (
                          <button
                            className="button ghost small"
                            type="button"
                            name="removeFromPosition"
                            data-testid="remove-from-position"
                            aria-label={t('candidate.profile.positions.removeLabel', {
                              title: link.title,
                            })}
                            onClick={() => void remove(link)}
                          >
                            {t('candidate.profile.positions.remove')}
                          </button>
                        ) : null}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {picking ? (
        <PositionPicker
          linkedIds={linkedIds}
          onSelect={async (positionId) => {
            await positionService.addCandidate(positionId, candidateId);
            await reload();
          }}
          onClose={() => setPicking(false)}
        />
      ) : null}
    </article>
  );
}
