import { useServices } from '../di/services-context';

/**
 * Surfaces a thrown error as an error toast.
 *
 * Business validation lives in the service layer and throws `Error`s carrying
 * the exact Spanish message the user should see; the fallback only applies when
 * something non-Error reaches the catch block.
 */
export function useErrorToast(): (error: unknown, fallback: string) => void {
  const { toastService } = useServices();
  return (error: unknown, fallback: string): void => {
    toastService.show(error instanceof Error && error.message ? error.message : fallback, 'error');
  };
}
