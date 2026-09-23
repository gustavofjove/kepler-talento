import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { Breadcrumb, type BreadcrumbItem } from '../../../shared/components/breadcrumb';
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
import { candidateFullName } from '../candidate-name';
import { mailtoHref, telHref } from '../contact-links';
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
  const canReadList = usePermission('candidates.read');
  // The list is offered in every state, even while loading or after a failure; the name only
  // once the candidate has loaded.
  const trail: BreadcrumbItem[] = [
    {
      label: t('breadcrumb.candidates'),
      to: canReadList ? '/app/candidates' : undefined,
      testId: 'breadcrumb-candidates',
    },
  ];
  if (aggregate !== 'loading' && item)
    trail.push({ label: candidateFullName(item), current: true });

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
      <>
        <Breadcrumb items={trail} />
        <section className="panel">
          <p className="empty-state">{t('candidate.detail.loading')}</p>
        </section>
      </>
    );
  if (aggregate === 'error')
    return (
      <>
        <Breadcrumb items={trail} />
        <section className="panel" data-testid="candidate-detail-error">
          <h1>{t('candidate.detail.loadFailure')}</h1>
          <p className="empty-state">
            {candidateService.error?.message ?? t('candidate.detail.tryLater')}
          </p>
          <Link className="button" to="/app/candidates">
            {t('candidate.detail.back')}
          </Link>
        </section>
      </>
    );
  if (!item)
    return (
      <>
        <Breadcrumb items={trail} />
        <section className="panel">
          <h1>{t('candidate.detail.notFound')}</h1>
          <Link className="button" to="/app/candidates">
            {t('candidate.detail.back')}
          </Link>
        </section>
      </>
    );

  return (
    <section className="page">
      <Breadcrumb items={trail} />
      <div className="toolbar">
        <div className="page-header">
          <h1>
            {item.firstName} {item.lastName}
          </h1>
          <p className="muted contact-links">
            {item.email ? <a href={mailtoHref(item.email)}>{item.email}</a> : null}
            {item.email && item.phone ? ' · ' : null}
            {item.phone ? <a href={telHref(item.phone)}>{item.phone}</a> : null}
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
          <dl className="prop-list">
            <dt>{t('candidate.detail.status')}</dt>
            <dd>{item.status}</dd>
            <dt>{t('candidate.detail.availability')}</dt>
            <dd>{item.availability}</dd>
            <dt>{t('candidate.detail.location')}</dt>
            <dd>
              {item.location} {item.province}
            </dd>
            <dt>{t('candidate.detail.receivedAt')}</dt>
            <dd>{item.receivedAt || t('candidate.detail.pending')}</dd>
            <dt>{t('candidate.detail.reviewDueAt')}</dt>
            <dd>{item.reviewDueAt || t('candidate.detail.pending')}</dd>
          </dl>
          {item.notes ? <p>{item.notes}</p> : null}
        </article>
        <article className="panel">
          <h2>{t('candidate.detail.audit')}</h2>
          <dl className="prop-list">
            <dt>{t('candidate.detail.created')}</dt>
            <dd>{item.createdAt.slice(0, 19)}</dd>
            <dt>{t('candidate.detail.updated')}</dt>
            <dd>{item.updatedAt.slice(0, 19)}</dd>
            <dt>{t('candidate.detail.active')}</dt>
            <dd data-testid="candidate-active">
              {t(item.isActive ? 'candidate.detail.yes' : 'candidate.detail.no')}
            </dd>
          </dl>
        </article>
      </div>
      {/* Viewing only: every change is made on the edit page, whatever the viewer may do. */}
      <div className="grid two">
        <article className="panel">
          <CandidateLanguages candidateId={item.id} languages={item.languages} readOnly />
        </article>
        <article className="panel">
          <CandidatePrograms candidateId={item.id} programs={item.programs} readOnly />
        </article>
        <article className="panel">
          <CandidateEducation candidateId={item.id} education={item.education} readOnly />
        </article>
        <article className="panel">
          <CandidateExperience candidateId={item.id} experience={item.experience} readOnly />
        </article>
        <article className="panel">
          <CandidateSkills candidateId={item.id} skills={item.skills} readOnly />
        </article>
        <article className="panel">
          <CandidateTags candidateId={item.id} tags={item.tags} readOnly />
        </article>
        <article className="panel span-all">
          <CandidateNotes candidateId={item.id} initialNotes={item.customNotes} readOnly />
        </article>
        <article className="panel">
          <CandidateDocuments candidate={item} readOnly />
        </article>
      </div>
      <CandidateCvPreview candidate={item} />
    </section>
  );
}
