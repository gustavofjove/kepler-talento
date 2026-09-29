import type { ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import './cv-draft-picker.css';

export interface CvDraftStatus {
  tone: 'info' | 'error';
  text: string;
}

interface CvDraftPickerProps {
  busy: boolean;
  status: CvDraftStatus | null;
  onPick: (file: File) => void;
}

/**
 * The «Rellenar desde un CV» control on the create page (KTL-32). Presentational: the page owns
 * the request and the outcome message.
 */
export function CvDraftPicker({ busy, status, onPick }: CvDraftPickerProps) {
  const { t } = useTranslation();

  const pick = (event: ChangeEvent<HTMLInputElement>): void => {
    const file = event.target.files?.[0];
    // Cleared so picking the same file again is a fresh pick.
    event.target.value = '';
    if (file) onPick(file);
  };

  return (
    <div className="cv-draft-picker" data-testid="cv-draft-picker" aria-busy={busy}>
      <div className="field">
        <label htmlFor="cvFile">{t('candidate.cvDraft.label')}</label>
        <input
          id="cvFile"
          name="cvFile"
          type="file"
          accept=".pdf,.docx"
          data-testid="cv-draft-file"
          aria-describedby="cvFile-hint"
          disabled={busy}
          onChange={pick}
        />
        <p id="cvFile-hint" className="muted">
          {t('candidate.cvDraft.hint')}
        </p>
      </div>
      <p
        className={status?.tone === 'error' ? 'cv-draft-status error' : 'cv-draft-status'}
        role="status"
        data-testid="cv-draft-status"
      >
        {busy ? t('candidate.cvDraft.busy') : (status?.text ?? '')}
      </p>
    </div>
  );
}
