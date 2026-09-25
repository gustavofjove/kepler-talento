import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { usePermission } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { Breadcrumb, type BreadcrumbItem } from '../../../shared/components/breadcrumb';
import { useCandidates } from '../use-candidates';
import { CandidateForm } from '../components/candidate-form';
import type { CandidateDraft } from '../models/candidate.models';

/**
 * The create page: the core record only. Once saved, the candidate opens on its own page,
 * where every other section is a panel edited in place (KTL-29).
 */
export function CandidateCreatePage() {
  const { t } = useTranslation();
  const candidateService = useCandidates();
  const canRead = usePermission('candidates.read');
  const notifyError = useErrorToast();
  const navigate = useNavigate();

  const trail: BreadcrumbItem[] = [
    {
      label: t('breadcrumb.candidates'),
      to: canRead ? '/app/candidates' : undefined,
      testId: 'breadcrumb-candidates',
    },
    { label: t('breadcrumb.newCandidate'), current: true },
  ];

  const save = async (draft: CandidateDraft): Promise<void> => {
    try {
      const created = await candidateService.create(draft);
      await navigate(`/app/candidates/${created.id}`);
    } catch (error) {
      notifyError(error, t('candidate.edit.saveFailure'));
    }
  };

  return (
    <section className="page">
      <Breadcrumb items={trail} />
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('candidate.edit.titleNew')}</h1>
          <p className="muted" data-testid="candidate-create-hint">
            {t('candidate.edit.newHint')}
          </p>
        </div>
      </div>
      <article className="panel">
        <h2>{t('candidate.edit.mainData')}</h2>
        <CandidateForm onSave={(draft) => void save(draft)} />
      </article>
    </section>
  );
}
