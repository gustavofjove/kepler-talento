import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useServices } from '../../../core/di/services-context';
import { formatDate } from '../../../core/i18n/format';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useRowLink } from '../../../shared/components/row-link';
import { mailtoHref } from '../../candidates/contact-links';
import type { PositionCandidate } from '../position.models';
import type { PositionCandidatesState } from '../use-position-candidates';
import { PositionCandidatePicker } from './position-candidate-picker';
import { PositionStageSelect } from './position-stage-select';

interface PositionCandidatesPanelProps {
  state: PositionCandidatesState;
  /** Holds `positions.manage` and the position is open: links may change. */
  editable: boolean;
  closed: boolean;
}

/** «Candidatos de la posición»: the persistent links of one position (KTL-30). */
export function PositionCandidatesPanel({ state, editable, closed }: PositionCandidatesPanelProps) {
  const { t } = useTranslation();
  const { confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const [picking, setPicking] = useState(false);
  const rowLink = useRowLink();
  const { links, status } = state;

  const changeStage = async (
    link: PositionCandidate,
    stage: PositionCandidate['stage'],
  ): Promise<void> => {
    try {
      await state.changeStage(link, stage);
    } catch (error) {
      notifyError(error, t('positions.candidates.stageError'));
    }
  };

  const remove = async (link: PositionCandidate): Promise<void> => {
    const name = `${link.firstName} ${link.lastName}`;
    const confirmed = await confirmDialogService.confirm({
      title: t('positions.candidates.removeTitle'),
      message: t('positions.candidates.removeMessage', { name }),
      confirmText: t('positions.candidates.removeConfirm'),
      cancelText: t('positions.candidates.cancel'),
      danger: true,
    });
    if (!confirmed) return;
    try {
      await state.remove(link.candidateId);
    } catch (error) {
      notifyError(error, t('positions.candidates.removeError'));
    }
  };

  return (
    <div className="panel position-section" data-testid="position-candidates">
      <div className="position-actions">
        <h2>{t('positions.candidates.title')}</h2>
        {editable ? (
          <button
            className="button secondary"
            type="button"
            name="addPositionCandidate"
            data-testid="position-candidates-add"
            onClick={() => setPicking(true)}
          >
            {t('positions.candidates.add')}
          </button>
        ) : null}
      </div>
      {closed ? (
        <p className="muted" data-testid="position-candidates-closed">
          {t('positions.candidates.closedHint')}
        </p>
      ) : null}
      {status === 'loading' && links.length === 0 ? (
        <p role="status">{t('positions.candidates.loading')}</p>
      ) : status === 'error' ? (
        <p role="alert">{t('positions.candidates.error')}</p>
      ) : links.length === 0 ? (
        <p className="muted" data-testid="position-candidates-empty">
          {t('positions.candidates.empty')}
        </p>
      ) : (
        <div className="table-wrap">
          <table className="position-links-table">
            <thead>
              <tr>
                <th>{t('positions.candidates.column.candidate')}</th>
                <th>{t('positions.candidates.column.phone')}</th>
                <th>{t('positions.candidates.column.stage')}</th>
                <th>{t('positions.candidates.column.cv')}</th>
                <th>{t('positions.candidates.column.addedAt')}</th>
                <th>{editable ? t('positions.candidates.column.actions') : null}</th>
              </tr>
            </thead>
            <tbody>
              {links.map((link) => {
                const name = `${link.firstName} ${link.lastName}`;
                return (
                  // The whole row opens the candidate; the name is its keyboard link.
                  <tr
                    key={link.candidateId}
                    className="row-link-row"
                    data-testid="position-candidate-row"
                    onClick={rowLink(`/app/candidates/${link.candidateId}`)}
                    onAuxClick={rowLink(`/app/candidates/${link.candidateId}`)}
                  >
                    <td>
                      <Link className="row-link" to={`/app/candidates/${link.candidateId}`}>
                        {name}
                      </Link>{' '}
                      {link.candidateIsActive ? null : (
                        <span className="badge" data-testid="position-candidate-inactive">
                          {t('positions.candidates.inactive')}
                        </span>
                      )}
                      {link.email ? (
                        <div className="muted contact-links">
                          <a href={mailtoHref(link.email)}>{link.email}</a>
                        </div>
                      ) : null}
                    </td>
                    <td>{link.phone}</td>
                    <td>
                      {editable ? (
                        <PositionStageSelect
                          value={link.stage}
                          label={t('positions.candidates.stageLabel', { name })}
                          onChange={(stage) => void changeStage(link, stage)}
                        />
                      ) : (
                        <span className="badge">{t(`positions.stage.${link.stage}`)}</span>
                      )}
                    </td>
                    <td>
                      {t(
                        link.hasPrimaryCv
                          ? 'positions.candidates.cvAvailable'
                          : 'positions.candidates.cvPending',
                      )}
                    </td>
                    <td>{formatDate(link.addedAtUtc, { dateStyle: 'medium' })}</td>
                    <td>
                      <div className="form-actions">
                        {editable ? (
                          <button
                            className="button ghost small"
                            type="button"
                            name="removePositionCandidate"
                            data-testid="remove-from-position"
                            aria-label={t('positions.candidates.removeLabel', { name })}
                            onClick={() => void remove(link)}
                          >
                            {t('positions.candidates.remove')}
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
        <PositionCandidatePicker
          linkedIds={state.linkedIds}
          onSelect={async (candidateId) => {
            await state.add(candidateId);
          }}
          onClose={() => setPicking(false)}
        />
      ) : null}
    </div>
  );
}
