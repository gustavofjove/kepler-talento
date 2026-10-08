import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { colorNameKey } from '../catalog-color.logic';
import type { CatalogColor } from '../models/catalog.models';
import { CatalogColorDialog } from './catalog-color-dialog';
import './catalog-color-swatch.css';

interface ReadOnlySwatchProps {
  color: CatalogColor;
}

/** A value's colour at rest: a filled circle, named by its tooltip and accessible name. */
export function CatalogColorSwatch({ color }: ReadOnlySwatchProps) {
  const { t } = useTranslation();
  const name = t(colorNameKey(color));
  return (
    <span
      className="catalog-color-swatch"
      data-catalog-color={color}
      data-testid="catalog-color"
      role="img"
      aria-label={t('catalogs.color.currentLabel', { color: name })}
      title={name}
    />
  );
}

interface PickerSwatchProps {
  /** Lets a host's `<label htmlFor>` caption the circle. */
  id?: string;
  color: CatalogColor;
  /** The trigger's accessible name, e.g. «Cambiar el color de Inglés (actual: Azul)». */
  label: string;
  testId: string;
  disabled?: boolean;
  onChange: (color: CatalogColor) => void;
}

/**
 * The same circle as a button that opens the colour dialog. Choosing a colour changes only the
 * host's draft: the host stores it with its own save.
 */
export function CatalogColorPickerSwatch({
  id,
  color,
  label,
  testId,
  disabled = false,
  onChange,
}: PickerSwatchProps) {
  const { t } = useTranslation();
  const [isOpen, setOpen] = useState(false);
  return (
    <>
      <button
        id={id}
        type="button"
        className="catalog-color-swatch catalog-color-swatch--button"
        data-catalog-color={color}
        data-testid={testId}
        aria-label={label}
        aria-haspopup="dialog"
        title={t(colorNameKey(color))}
        disabled={disabled}
        onClick={() => setOpen(true)}
        // The row editor saves on Enter and cancels on Escape; neither belongs to this button.
        onKeyDown={(event) => event.stopPropagation()}
      />
      <CatalogColorDialog
        isOpen={isOpen}
        onOpenChange={setOpen}
        color={color}
        onChange={onChange}
      />
    </>
  );
}
