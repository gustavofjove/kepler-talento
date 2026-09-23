import { useServices } from '../../core/di/services-context';
import { useSignal } from '../../core/state/use-signal';
import type { PositionPage } from './position.models';

export function usePositions(): PositionPage {
  const { positionService } = useServices();
  return useSignal(positionService.state);
}
