import { useCallback } from 'react';
import { useServices } from '../di/services-context';
import { AppError } from '../../shared/models/error.models';

interface FieldIssue {
  property?: string;
  code?: string;
  message?: string;
}

/**
 * A validation problem carries a generic `detail` plus the per-field issues that say
 * what actually went wrong ("Ya existe un valor con ese nombre."). The field message
 * is the one the user needs, so it wins when present.
 */
function fieldMessage(error: unknown): string | null {
  if (!(error instanceof AppError)) {
    return null;
  }
  const issues = (error.details as { errors?: FieldIssue[] } | undefined)?.errors;
  if (!Array.isArray(issues)) {
    return null;
  }
  return issues.find((issue) => issue?.message)?.message ?? null;
}

/**
 * Surfaces a thrown error as an error toast.
 *
 * Business validation lives in the service layer and throws `Error`s carrying
 * the exact Spanish message the user should see; the fallback only applies when
 * something non-Error reaches the catch block.
 */
export function useErrorToast(): (error: unknown, fallback: string) => void {
  const { toastService } = useServices();
  return useCallback(
    (error: unknown, fallback: string): void => {
      const specific = fieldMessage(error);
      toastService.show(
        specific ?? (error instanceof Error && error.message ? error.message : fallback),
        'error',
      );
    },
    [toastService],
  );
}
