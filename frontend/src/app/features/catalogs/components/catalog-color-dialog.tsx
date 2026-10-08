import { useId } from 'react';
import { Dialog, Heading, ListBox, ListBoxItem, Modal, ModalOverlay } from 'react-aria-components';
import { useTranslation } from 'react-i18next';
import { CheckIcon } from '../../../shared/components/icons';
import { CATALOG_COLORS, colorNameKey } from '../catalog-color.logic';
import type { CatalogColor } from '../models/catalog.models';
import '../../../shared/components/modal.css';
import './catalog-color-dialog.css';

interface CatalogColorDialogProps {
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
  color: CatalogColor;
  onChange: (color: CatalogColor) => void;
}

/**
 * The palette as a grid of named swatches (KTL-41). A listbox rather than radios: arrow keys move
 * between colours without choosing, and Enter, Space or a click chooses and closes, so browsing
 * the grid never dismisses it. Escape and «Cancelar» close without a change; focus returns to
 * whatever opened the dialog.
 */
export function CatalogColorDialog({
  isOpen,
  onOpenChange,
  color,
  onChange,
}: CatalogColorDialogProps) {
  const { t } = useTranslation();
  const descriptionId = useId();

  const choose = (next: CatalogColor): void => {
    onChange(next);
    onOpenChange(false);
  };

  return (
    <ModalOverlay className="overlay" isOpen={isOpen} onOpenChange={onOpenChange} isDismissable>
      <Modal className="catalog-color-modal">
        <Dialog
          className="dialog catalog-color-dialog"
          data-testid="catalog-color-dialog"
          aria-describedby={descriptionId}
        >
          <Heading slot="title">{t('catalogs.color.dialog.title')}</Heading>
          <p id={descriptionId} className="muted">
            {t('catalogs.color.dialog.description')}
          </p>
          <ListBox
            aria-label={t('catalogs.color.dialog.title')}
            className="catalog-color-grid"
            layout="grid"
            selectionMode="single"
            selectedKeys={[color]}
            autoFocus="first"
            // An emptied selection is either the current colour pressed again (it toggles off) or
            // Escape, which a listbox handles by clearing. Both mean «keep it»: close unchanged.
            onSelectionChange={(keys) => {
              const [next] = keys === 'all' ? [] : [...keys];
              if (next === undefined) {
                onOpenChange(false);
                return;
              }
              choose(next as CatalogColor);
            }}
          >
            {CATALOG_COLORS.map((option) => (
              <ListBoxItem
                key={option}
                id={option}
                textValue={t(colorNameKey(option))}
                className="catalog-color-option"
                data-testid="catalog-color-option"
                data-value={option}
              >
                <span className="catalog-color-option-swatch" data-catalog-color={option} />
                <span className="catalog-color-option-name">{t(colorNameKey(option))}</span>
                {option === color ? (
                  <span className="catalog-color-option-check">
                    <CheckIcon />
                  </span>
                ) : null}
              </ListBoxItem>
            ))}
          </ListBox>
          <div className="actions">
            <button
              className="button ghost"
              type="button"
              data-testid="catalog-color-cancel"
              onClick={() => onOpenChange(false)}
            >
              {t('catalogs.color.dialog.cancel')}
            </button>
          </div>
        </Dialog>
      </Modal>
    </ModalOverlay>
  );
}
