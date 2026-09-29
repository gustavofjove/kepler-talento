import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { errorText, TranslatableError } from '../../../core/i18n/translatable-error';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { Breadcrumb, type BreadcrumbItem } from '../../../shared/components/breadcrumb';
import { useCandidates } from '../use-candidates';
import { CandidateForm } from '../components/candidate-form';
import { CvDraftPicker, type CvDraftStatus } from '../components/cv-draft-picker';
import type { CandidateDraft } from '../models/candidate.models';
import type { CvDraftSuggestion } from '../models/candidate-draft.models';

/**
 * The create page: the core record only. Once saved, the candidate opens on its own page,
 * where every other section is a panel edited in place (KTL-29). A CV can pre-fill the empty
 * fields of the form; nothing is saved until the user submits it (KTL-32).
 */
export function CandidateCreatePage() {
  const { t } = useTranslation();
  const candidateService = useCandidates();
  const { candidateDraftService } = useServices();
  const canRead = usePermission('candidates.read');
  const notifyError = useErrorToast();
  const navigate = useNavigate();
  const [suggestion, setSuggestion] = useState<CvDraftSuggestion>();
  const [draftBusy, setDraftBusy] = useState(false);
  const [draftStatus, setDraftStatus] = useState<CvDraftStatus | null>(null);
  const pending = useRef<AbortController | null>(null);

  useEffect(() => () => pending.current?.abort(), []);

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

  const readCv = async (file: File): Promise<void> => {
    pending.current?.abort();
    const controller = new AbortController();
    pending.current = controller;
    setDraftBusy(true);
    setDraftStatus(null);
    try {
      const draft = await candidateDraftService.extract(file, controller.signal);
      if (draft.outcome === 'cv_draft.no_text') {
        setDraftStatus({ tone: 'info', text: t('candidate.cvDraft.noText') });
      } else {
        setSuggestion((current) => ({ nonce: (current?.nonce ?? 0) + 1, fields: draft.fields }));
      }
    } catch (error) {
      if (controller.signal.aborted) return;
      if (error instanceof TranslatableError) {
        setDraftStatus({ tone: 'error', text: errorText(error, t) });
      } else {
        const message = notifyError(error, t('candidate.cvDraft.error.generic'));
        setDraftStatus({ tone: 'error', text: message });
      }
    } finally {
      if (pending.current === controller) {
        pending.current = null;
        setDraftBusy(false);
      }
    }
  };

  const suggestionApplied = useCallback(
    (filled: number) =>
      setDraftStatus({
        tone: 'info',
        text:
          filled > 0
            ? t('candidate.cvDraft.filled', { count: filled })
            : t('candidate.cvDraft.nothingFilled'),
      }),
    [t],
  );

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
        <CvDraftPicker busy={draftBusy} status={draftStatus} onPick={(file) => void readCv(file)} />
        <CandidateForm
          onSave={(draft) => void save(draft)}
          suggestion={suggestion}
          onSuggestionApplied={suggestionApplied}
        />
      </article>
    </section>
  );
}
