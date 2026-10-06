import { formatNumber } from '../../../core/i18n/format';
import type { ChipTone } from '../../../shared/components/status-chip';
import type { CandidateDocument } from '../models/candidate.models';

const PREFIX = 'candidate.profile.documents.';

export const ACCEPTED_FILES = '.pdf,.doc,.docx,.odt,.rtf,.txt,.jpg,.jpeg,.png,.tif,.tiff,.bmp';

export function documentStateChip(
  state: string | undefined,
): { labelKey: string; tone: ChipTone } | undefined {
  switch (state) {
    case 'Available':
      return undefined;
    case 'Pending':
      return { labelKey: `${PREFIX}state.pending`, tone: 'neutral' };
    case 'Error':
      return { labelKey: `${PREFIX}state.error`, tone: 'danger' };
    case 'Refused':
      return { labelKey: `${PREFIX}state.refused`, tone: 'danger' };
    default:
      return { labelKey: `${PREFIX}state.legacy`, tone: 'neutral' };
  }
}

export function documentExplanationKey(state: string | undefined): string | undefined {
  switch (state) {
    case 'Available':
    case 'Pending':
      return undefined;
    case 'Error':
      return `${PREFIX}explanation.error`;
    case 'Refused':
      return `${PREFIX}explanation.refused`;
    default:
      return `${PREFIX}explanation.legacy`;
  }
}

export function documentDetails(
  document: Pick<CandidateDocument, 'originalFilename' | 'mimeType' | 'sizeBytes' | 'documentType'>,
) {
  const extension = document.originalFilename.match(/\.([^.]+)$/)?.[1];
  return {
    format: (extension ?? document.mimeType.split('/')[1] ?? '').toUpperCase(),
    sizeBytes: document.sizeBytes,
    type:
      document.documentType && document.documentType !== 'CV' ? document.documentType : undefined,
  };
}

export function formatFileSize(bytes: number): string {
  const megabytes = bytes >= 1_000_000;
  return formatNumber(megabytes ? bytes / 1_000_000 : Math.max(1, Math.round(bytes / 1000)), {
    style: 'unit',
    unit: megabytes ? 'megabyte' : 'kilobyte',
    unitDisplay: 'short',
    minimumFractionDigits: megabytes ? 1 : 0,
    maximumFractionDigits: megabytes ? 1 : 0,
  });
}

export function canMarkPrimary(
  document: Pick<CandidateDocument, 'availabilityState' | 'isPrimary'>,
): boolean {
  return document.availabilityState === 'Available' && !document.isPrimary;
}
