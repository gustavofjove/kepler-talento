import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useCandidate } from '../use-candidates';
import { CandidateForm } from '../components/candidate-form';
import { CandidateDocuments } from '../components/candidate-documents';
import { CandidateEducation } from '../components/candidate-education';
import { CandidateExperience } from '../components/candidate-experience';
import { CandidateLanguages } from '../components/candidate-languages';
import { CandidateNotes } from '../components/candidate-notes';
import { CandidatePrograms } from '../components/candidate-programs';
import { CandidateSkills } from '../components/candidate-skills';
import { CandidateTags } from '../components/candidate-tags';
import type { CandidateDraft } from '../models/candidate.models';

export function CandidateEditPage() {
  const { t } = useTranslation();
  // The '' default reproduces Angular's @Input('id') default on /candidates/new,
  // which is what selects the heading below.
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const { toastService } = useServices();
  const candidateService = useCandidate(candidateId);
  const canUpdate = usePermission('candidates.update');
  const notifyError = useErrorToast();
  const navigate = useNavigate();

  const candidate = candidateId ? candidateService.find(candidateId) : undefined;
  // The form snapshots its draft from `candidate` on first render, so it must not be
  // rendered until the aggregate has arrived — otherwise it initialises empty and never
  // picks the candidate up.
  const aggregate = candidateId ? candidateService.aggregateStatus(candidateId) : 'loaded';
  const isLoading = aggregate === 'loading';
  const hasFailed = aggregate === 'error';
  const isMissing = aggregate === 'missing';

  const save = async (draft: CandidateDraft): Promise<void> => {
    try {
      // The version travels with the loaded aggregate inside the service, so a form
      // opened before someone else's edit is refused rather than overwriting it — while
      // the sections below, which absorb each write, never make this save conflict.
      if (candidateId) {
        await candidateService.update(candidateId, draft);
        toastService.show(t('candidate.edit.saved'), 'success');
        return;
      }
      const created = await candidateService.create(draft);
      // A creator who may not update would be refused the edit route; send them to the
      // read-only page instead.
      await navigate(
        canUpdate ? `/app/candidates/${created.id}/edit` : `/app/candidates/${created.id}`,
      );
    } catch (error) {
      notifyError(error, t('candidate.edit.saveFailure'));
    }
  };

  const body = isLoading ? (
    <div className="panel">
      <p className="empty-state">{t('candidate.detail.loading')}</p>
    </div>
  ) : hasFailed ? (
    <div className="panel">
      <p className="empty-state" data-testid="candidate-edit-error">
        {candidateService.error?.message ?? t('candidate.edit.loadFailure')}
      </p>
    </div>
  ) : isMissing ? (
    <div className="panel">
      <p className="empty-state">{t('candidate.profile.validation.notFound')}</p>
    </div>
  ) : (
    <>
      <article className="panel">
        <h2>{t('candidate.edit.mainData')}</h2>
        {/* key remounts the form when navigating between new and edit. */}
        <CandidateForm key={candidateId || 'new'} candidate={candidate} onSave={save} />
      </article>
      {candidate ? (
        <div className="grid two">
          <article className="panel">
            <CandidateLanguages candidateId={candidate.id} languages={candidate.languages} />
          </article>
          <article className="panel">
            <CandidatePrograms candidateId={candidate.id} programs={candidate.programs} />
          </article>
          <article className="panel">
            <CandidateEducation candidateId={candidate.id} education={candidate.education} />
          </article>
          <article className="panel">
            <CandidateExperience candidateId={candidate.id} experience={candidate.experience} />
          </article>
          <article className="panel">
            <CandidateSkills candidateId={candidate.id} skills={candidate.skills} />
          </article>
          <article className="panel">
            <CandidateTags candidateId={candidate.id} tags={candidate.tags} />
          </article>
          <article className="panel span-all">
            <CandidateNotes candidateId={candidate.id} initialNotes={candidate.customNotes} />
          </article>
          <article className="panel">
            <CandidateDocuments candidate={candidate} />
          </article>
        </div>
      ) : null}
    </>
  );

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t(candidateId ? 'candidate.edit.titleEdit' : 'candidate.edit.titleNew')}</h1>
          <p className="muted" data-testid="candidate-edit-hint">
            {t(candidateId ? 'candidate.edit.saveHint' : 'candidate.edit.newHint')}
          </p>
        </div>
        {candidateId ? (
          <div className="toolbar">
            <Link
              className="button secondary"
              to={`/app/candidates/${candidateId}`}
              data-testid="candidate-edit-view"
            >
              {t('candidate.edit.backToDetail')}
            </Link>
          </div>
        ) : null}
      </div>
      {body}
    </section>
  );
}
