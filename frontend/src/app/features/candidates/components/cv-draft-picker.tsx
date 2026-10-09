import type { ChangeEvent } from 'react';
import { useTranslation } from 'react-i18next';
import './cv-draft-picker.css';

export interface CvDraftStatus {
  tone: 'info' | 'error';
  text: string;
}

export interface CvDraftAttach {
  checked: boolean;
  onChange: (checked: boolean) => void;
}

interface CvDraftPickerProps {
  busy: boolean;
  status: CvDraftStatus | null;
  onPick: (file: File) => void;
  /** Set once a CV has been read and may be attached on save (KTL-42). */
  attach?: CvDraftAttach;
}

/**
 * The «Rellenar desde un CV» control on the create page (KTL-32). Presentational: the page owns
 * the request, the outcome message and the read CV it may attach on save (KTL-42).
 */
export function CvDraftPicker({ busy, status, onPick, attach }: CvDraftPickerProps) {
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
      {attach && (
        <label className="inline-check cv-draft-attach">
          <input
            type="checkbox"
            name="attachCv"
            data-testid="cv-draft-attach"
            checked={attach.checked}
            disabled={busy}
            onChange={(event) => attach.onChange(event.target.checked)}
          />
          {t('candidate.cvDraft.attach')}
        </label>
      )}
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
