import type { ReactNode } from 'react';

export type ChipTone = 'success' | 'danger' | 'neutral';

interface StatusChipProps {
  tone: ChipTone;
  children: ReactNode;
  testId?: string;
}

export function StatusChip({ tone, children, testId }: StatusChipProps) {
  return (
    <span className={`badge badge--${tone}`} data-tone={tone} data-testid={testId}>
      {children}
    </span>
  );
}
