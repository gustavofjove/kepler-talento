import { useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import type { SearchPreset } from '../../search/models/search.models';
import { presetErrorKey } from './preset-errors';

/**
 * Deleting a preset, shared by the list and the detail page: always confirmed first, always
 * against the version the page loaded. Resolves true only when the preset is gone.
 */
export function useDeletePreset(): (preset: SearchPreset) => Promise<boolean> {
  const { searchPresetsService, confirmDialogService, toastService } = useServices();
  const notifyError = useErrorToast();
  const { t } = useTranslation();

  return useCallback(
    async (preset: SearchPreset): Promise<boolean> => {
      const confirmed = await confirmDialogService.confirm({
        title: t('presets.delete.title'),
        message: t('presets.delete.message', { name: preset.name }),
        confirmText: t('presets.delete.confirm'),
        cancelText: t('presets.delete.cancel'),
        danger: true,
      });
      if (!confirmed) {
        return false;
      }
      try {
        await searchPresetsService.removePreset(preset.id, preset.version);
        toastService.show(t('presets.delete.done', { name: preset.name }), 'success');
        return true;
      } catch (error) {
        const key = presetErrorKey(error);
        if (key) {
          toastService.show(t(key), 'error');
        } else {
          notifyError(error, t('presets.delete.failed'));
        }
        return false;
      }
    },
    [confirmDialogService, notifyError, searchPresetsService, t, toastService],
  );
}
