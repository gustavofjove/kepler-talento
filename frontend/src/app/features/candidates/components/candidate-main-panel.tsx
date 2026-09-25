import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { Candidate, CandidateDraft } from '../models/candidate.models';
import { useCandidates } from '../use-candidates';
import { CandidateForm } from './candidate-form';
import { CandidatePanel } from './candidate-panel';
import type { PanelControl } from './candidate-panel.logic';

const FORM_ID = 'candidate-main-form';

interface Props {
  candidate: Candidate;
  control: PanelControl;
}

/** Datos principales: the core record, read as a property list and edited with the core form. */
export function CandidateMainPanel({ candidate, control }: Props) {
  const { t } = useTranslation();
  const { toastService } = useServices();
  const candidateService = useCandidates();
  const notifyError = useErrorToast();
  const [saving, setSaving] = useState(false);

  const save = async (draft: CandidateDraft): Promise<void> => {
    setSaving(true);
    try {
      // The version travels with the aggregate inside the service, so a panel opened before
      // someone else's edit is refused rather than overwriting it, and the draft is kept.
      await candidateService.update(candidate.id, draft);
      toastService.show(t('candidate.panel.saved.main'), 'success');
      control.onClose();
    } catch (error) {
      notifyError(error, t('candidate.edit.saveFailure'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <CandidatePanel
      id="main"
      title={t('candidate.detail.mainData')}
      control={control}
      mode="form"
      formId={FORM_ID}
      saving={saving}
    >
      {control.editing ? (
        <CandidateForm
          candidate={candidate}
          formId={FORM_ID}
          onSave={(draft) => void save(draft)}
          onDirtyChange={control.onDirtyChange}
        />
      ) : (
        <>
          <dl className="prop-list">
            <dt>{t('candidate.detail.status')}</dt>
            <dd>{candidate.status}</dd>
            <dt>{t('candidate.detail.availability')}</dt>
            <dd>{candidate.availability}</dd>
            <dt>{t('candidate.detail.location')}</dt>
            <dd>
              {candidate.location} {candidate.province}
            </dd>
            <dt>{t('candidate.detail.receivedAt')}</dt>
            <dd>{candidate.receivedAt || t('candidate.detail.pending')}</dd>
            <dt>{t('candidate.detail.reviewDueAt')}</dt>
            <dd>{candidate.reviewDueAt || t('candidate.detail.pending')}</dd>
          </dl>
          {candidate.notes ? <p>{candidate.notes}</p> : null}
        </>
      )}
    </CandidatePanel>
  );
}
