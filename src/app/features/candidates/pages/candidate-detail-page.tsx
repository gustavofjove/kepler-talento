import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { CandidateCvPreview } from '../components/candidate-cv-preview';
import { CandidateDocuments } from '../components/candidate-documents';
import { CandidateEducation } from '../components/candidate-education';
import { CandidateExperience } from '../components/candidate-experience';
import { CandidateLanguages } from '../components/candidate-languages';
import { CandidatePrograms } from '../components/candidate-programs';
import { CandidateSkills } from '../components/candidate-skills';
import { CandidateTags } from '../components/candidate-tags';
import { CandidateNotes } from '../components/candidate-notes';
import { useCandidate } from '../use-candidates';

export function CandidateDetailPage() {
  const { t } = useTranslation();
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const { confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const candidateService = useCandidate(candidateId);
  const item = candidateService.find(candidateId);
  const canEdit = usePermission('candidates.update');
  const aggregate = candidateService.aggregateStatus(candidateId);

  const setActive = async (active: boolean): Promise<void> => {
    if (!item) return;
    const confirmed = await confirmDialogService.confirm({
      title: t(active ? 'candidate.detail.activateTitle' : 'candidate.detail.deactivateTitle'),
      message: t(
        active ? 'candidate.detail.activateMessage' : 'candidate.detail.deactivateMessage',
      ),
      confirmText: t(active ? 'candidate.detail.activate' : 'candidate.detail.deactivate'),
      cancelText: t('candidate.detail.cancel'),
      danger: !active,
    });
    if (!confirmed) return;
    try {
      if (active) await candidateService.reactivate(item.id);
      else await candidateService.deactivate(item.id);
    } catch (error) {
      notifyError(error, t('candidate.detail.stateFailure'));
    }
  };

  if (aggregate === 'loading')
    return (
      <section className="panel">
        <p className="empty-state">{t('candidate.detail.loading')}</p>
      </section>
    );
  if (aggregate === 'error')
    return (
      <section className="panel" data-testid="candidate-detail-error">
        <h1>{t('candidate.detail.loadFailure')}</h1>
        <p className="empty-state">
          {candidateService.error?.message ?? t('candidate.detail.tryLater')}
        </p>
        <Link className="button" to="/app/candidates">
          {t('candidate.detail.back')}
        </Link>
      </section>
    );
  if (!item)
    return (
      <section className="panel">
        <h1>{t('candidate.detail.notFound')}</h1>
        <Link className="button" to="/app/candidates">
          {t('candidate.detail.back')}
        </Link>
      </section>
    );

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>
            {item.firstName} {item.lastName}
          </h1>
          <p className="muted">
            {item.email} · {item.phone}
          </p>
        </div>
        <div className="toolbar">
          {canEdit ? (
            <>
              <Link className="button secondary" to={`/app/candidates/${item.id}/edit`}>
                {t('candidate.detail.edit')}
              </Link>
              <button
                className={item.isActive ? 'button danger' : 'button secondary'}
                type="button"
                onClick={() => void setActive(!item.isActive)}
              >
                {t(
                  item.isActive
                    ? 'candidate.detail.logicalDeactivate'
                    : 'candidate.detail.logicalActivate',
                )}
              </button>
            </>
          ) : null}
        </div>
      </div>
      <div className="grid two">
        <article className="panel">
          <h2>{t('candidate.detail.mainData')}</h2>
          <p>
            <strong>{t('candidate.detail.status')}:</strong> {item.status}
          </p>
          <p>
            <strong>{t('candidate.detail.availability')}:</strong> {item.availability}
          </p>
          <p>
            <strong>{t('candidate.detail.location')}:</strong> {item.location} {item.province}
          </p>
          <p>
            <strong>{t('candidate.detail.receivedAt')}:</strong>{' '}
            {item.receivedAt || t('candidate.detail.pending')}
          </p>
          <p>
            <strong>{t('candidate.detail.reviewDueAt')}:</strong>{' '}
            {item.reviewDueAt || t('candidate.detail.pending')}
          </p>
          <p>{item.notes}</p>
        </article>
        <article className="panel">
          <h2>{t('candidate.detail.audit')}</h2>
          <p>
            <strong>{t('candidate.detail.created')}:</strong> {item.createdAt.slice(0, 19)}
          </p>
          <p>
            <strong>{t('candidate.detail.updated')}:</strong> {item.updatedAt.slice(0, 19)}
          </p>
          <p>
            <strong>{t('candidate.detail.active')}:</strong>{' '}
            {t(item.isActive ? 'candidate.detail.yes' : 'candidate.detail.no')}
          </p>
        </article>
      </div>
      <div className="grid two">
        <article className="panel">
          <CandidateLanguages candidateId={item.id} languages={item.languages} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidatePrograms candidateId={item.id} programs={item.programs} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateEducation candidateId={item.id} education={item.education} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateExperience
            candidateId={item.id}
            experience={item.experience}
            canEdit={canEdit}
          />
        </article>
        <article className="panel">
          <CandidateSkills candidateId={item.id} skills={item.skills} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateTags candidateId={item.id} tags={item.tags} />
        </article>
        <article className="panel span-all">
          <CandidateNotes candidateId={item.id} initialNotes={item.customNotes} />
        </article>
        <article className="panel">
          <CandidateDocuments candidate={item} />
        </article>
      </div>
      <CandidateCvPreview candidate={item} />
    </section>
  );
}
