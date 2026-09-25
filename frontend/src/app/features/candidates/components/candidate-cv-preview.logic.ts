import type { CandidateDocument } from '../models/candidate.models';

export type PreviewMessageKey =
  | 'candidate.profile.preview.pending'
  | 'candidate.profile.preview.error'
  | 'candidate.profile.preview.refused'
  | 'candidate.profile.preview.legacyUnavailable'
  | 'candidate.profile.preview.unsupported';

// PDF open parameters: fit the page to the viewer width so the whole line is readable at
// any screen size. Chromium honours `view=FitH` (and keeps fitting on resize); Firefox's
// pdf.js reads `zoom=page-width`. Each viewer ignores the parameter it does not know.
export const FIT_WIDTH_FRAGMENT = '#view=FitH&zoom=page-width';

export function viewerUrl(objectUrl: string): string {
  return `${objectUrl}${FIT_WIDTH_FRAGMENT}`;
}

export function isPreviewable(document: CandidateDocument): boolean {
  return document.availabilityState === 'Available' && document.mimeType === 'application/pdf';
}

export function previewMessageKey(document: CandidateDocument): PreviewMessageKey {
  switch (document.availabilityState) {
    case 'Pending':
      return 'candidate.profile.preview.pending';
    case 'Error':
      return 'candidate.profile.preview.error';
    case 'Refused':
      return 'candidate.profile.preview.refused';
    case 'LegacyUnavailable':
      return 'candidate.profile.preview.legacyUnavailable';
    default:
      return 'candidate.profile.preview.unsupported';
  }
}

export function pickDefaultDocument(
  documents: readonly CandidateDocument[],
): CandidateDocument | undefined {
  const primary = documents.find((document) => document.isPrimary);
  if (primary && isPreviewable(primary)) return primary;
  const previewable = documents.filter(isPreviewable).sort((left, right) => {
    const byDate = right.uploadedAt.localeCompare(left.uploadedAt);
    return byDate || left.id.localeCompare(right.id);
  });
  return previewable[0] ?? primary;
}
