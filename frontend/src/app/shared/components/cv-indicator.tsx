import { useTranslation } from 'react-i18next';
import './cv-indicator.css';

/**
 * The «CV» cell of a candidate table (KTL-34): a tick when the candidate has a primary CV, nothing
 * visible otherwise. Both states carry an accessible name so the cell is never silent.
 */
export function CvIndicator({ hasCv }: { hasCv: boolean }) {
  const { t } = useTranslation();

  if (!hasCv) {
    return (
      <span className="cv-indicator-hidden" data-testid="cv-indicator-absent">
        {t('cvIndicator.absent')}
      </span>
    );
  }

  const label = t('cvIndicator.present');
  return (
    <svg
      className="cv-indicator"
      data-testid="cv-indicator-present"
      viewBox="0 0 24 24"
      width="18"
      height="18"
      role="img"
      aria-label={label}
      focusable="false"
    >
      <title>{label}</title>
      <path
        d="M5 12.5l4.5 4.5L19 7.5"
        fill="none"
        stroke="currentColor"
        strokeWidth={2.5}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
