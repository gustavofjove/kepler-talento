import { useState, type FormEvent, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import '../../../shared/components/data-table.css';
import { isRowClick } from '../../../shared/components/row-link';
import { ArrowDownIcon, ArrowUpIcon } from '../components/move-icons';
import { useCatalogs } from '../use-catalogs';
import {
  CATALOG_FAMILY_LABELS,
  type CatalogFamily,
  type CatalogItem,
} from '../models/catalog.models';

const FAMILY_OPTIONS = (Object.keys(CATALOG_FAMILY_LABELS) as CatalogFamily[]).map((key) => ({
  key,
  label: CATALOG_FAMILY_LABELS[key],
}));

export function CatalogManagementPage() {
  const { t } = useTranslation();
  const { toastService, confirmDialogService } = useServices();
  const notifyError = useErrorToast();
  const catalogService = useCatalogs();

  const [activeFamily, setActiveFamily] = useState<CatalogFamily>('language');
  const [newNameEs, setNewNameEs] = useState('');
  const [newCode, setNewCode] = useState('');
  const [editingId, setEditingId] = useState('');
  const [editNameEs, setEditNameEs] = useState('');
  const [editCode, setEditCode] = useState('');

  const items = catalogService.list(activeFamily, true);
  const activeCount = items.filter((item) => item.isActive).length;
  const isLoading = catalogService.status === 'idle' || catalogService.status === 'loading';
  const hasFailed = catalogService.status === 'error';

  const cancelEdit = (): void => {
    setEditingId('');
    setEditNameEs('');
    setEditCode('');
  };

  const createItem = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      await catalogService.create(activeFamily, newNameEs, newCode);
      setNewNameEs('');
      setNewCode('');
      toastService.show(t('catalogs.management.toast.created'), 'success');
    } catch (error) {
      notifyError(error, t('catalogs.management.error.create'));
    }
  };

  const startEdit = (item: CatalogItem): void => {
    setEditingId(item.id);
    setEditNameEs(item.nameEs);
    setEditCode(item.code);
  };

  const saveEdit = async (item: CatalogItem): Promise<void> => {
    try {
      await catalogService.update(activeFamily, item.id, { nameEs: editNameEs, code: editCode });
      cancelEdit();
      toastService.show(t('catalogs.management.toast.updated'), 'success');
    } catch (error) {
      notifyError(error, t('catalogs.management.error.update'));
    }
  };

  /** Enter saves and Escape cancels, as in any inline editor. */
  const onEditKeyDown = (event: KeyboardEvent<HTMLInputElement>, item: CatalogItem): void => {
    if (event.key === 'Enter') {
      event.preventDefault();
      void saveEdit(item);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      cancelEdit();
    }
  };

  /**
   * Deactivation is the only way to retire a value: catalog values are never deleted,
   * so existing candidate records keep their meaning.
   */
  const toggle = async (item: CatalogItem): Promise<void> => {
    if (item.isActive) {
      const confirmDeactivate = await confirmDialogService.confirm({
        title: t('catalogs.management.confirmDeactivate.title'),
        message: t('catalogs.management.confirmDeactivate.message', { name: item.nameEs }),
        confirmText: t('catalogs.management.action.deactivate'),
        cancelText: t('catalogs.management.action.cancel'),
        danger: true,
      });
      if (!confirmDeactivate) {
        return;
      }
    }
    try {
      const updated = await catalogService.toggleActive(activeFamily, item.id);
      toastService.show(
        t(
          updated.isActive
            ? 'catalogs.management.toast.activated'
            : 'catalogs.management.toast.deactivated',
        ),
        'success',
      );
    } catch (error) {
      notifyError(error, t('catalogs.management.error.toggle'));
    }
  };

  const move = async (item: CatalogItem, direction: -1 | 1): Promise<void> => {
    try {
      await catalogService.move(activeFamily, item.id, direction);
    } catch (error) {
      notifyError(error, t('catalogs.management.error.move'));
    }
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>{t('catalogs.management.title')}</h1>
        <p className="muted">{t('catalogs.management.subtitle')}</p>
      </div>

      <div className="panel stack">
        <div className="toolbar">
          <div className="field field--wide">
            <label htmlFor="family">{t('catalogs.management.family')}</label>
            <select
              id="family"
              name="family"
              value={activeFamily}
              onChange={(e) => {
                setActiveFamily(e.target.value as CatalogFamily);
                cancelEdit();
              }}
            >
              {FAMILY_OPTIONS.map((family) => (
                <option key={family.key} value={family.key}>
                  {family.label}
                </option>
              ))}
            </select>
          </div>
          <p className="muted">
            {isLoading
              ? t('catalogs.management.loading')
              : hasFailed
                ? t('catalogs.management.loadFailed')
                : t('catalogs.management.counts', { active: activeCount, total: items.length })}
          </p>
        </div>

        {(['language_level', 'program_level', 'skill_level'] as CatalogFamily[]).includes(
          activeFamily,
        ) && <p className="muted">{t('catalogs.management.levelOrderHint')}</p>}

        <form className="grid two" onSubmit={(event) => void createItem(event)} noValidate>
          <div className="field">
            <label htmlFor="newNameEs">{t('catalogs.management.form.name')}</label>
            <input
              id="newNameEs"
              name="newNameEs"
              value={newNameEs}
              onChange={(e) => setNewNameEs(e.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="newCode">{t('catalogs.management.form.code')}</label>
            <input
              id="newCode"
              name="newCode"
              value={newCode}
              placeholder={t('catalogs.management.form.codePlaceholder')}
              onChange={(e) => setNewCode(e.target.value)}
            />
          </div>
          <div className="form-actions span-all">
            <button
              className="button"
              type="submit"
              disabled={!!editingId || isLoading || hasFailed}
            >
              {t('catalogs.management.form.add')}
            </button>
          </div>
        </form>

        {isLoading ? (
          <p className="empty-state">{t('catalogs.management.loading')}</p>
        ) : hasFailed ? (
          <p className="empty-state">
            {catalogService.error?.message ?? t('catalogs.management.loadFailed')}
          </p>
        ) : !items.length ? (
          <p className="empty-state">{t('catalogs.management.empty')}</p>
        ) : (
          <div className="table-wrap">
            {/* A catalog value has no page of its own: its row opens the inline editor instead. */}
            <table className="catalog-table data-table">
              <thead>
                <tr>
                  <th>{t('catalogs.management.column.order')}</th>
                  <th>{t('catalogs.management.column.code')}</th>
                  <th>{t('catalogs.management.column.name')}</th>
                  <th>{t('catalogs.management.column.status')}</th>
                  <th>{t('catalogs.management.column.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => {
                  const isEditing = editingId === item.id;
                  // While one row is edited, the other rows' actions are disabled
                  // (not hidden, so the table does not reflow) until Save or Cancel.
                  const isLocked = !!editingId && !isEditing;
                  // Only an idle table edits on row click; a locked or editing row stays put.
                  const canStartEdit = !editingId;
                  return (
                    <tr
                      key={item.id}
                      className={
                        isEditing ? 'is-editing' : canStartEdit ? 'row-link-row' : undefined
                      }
                      data-testid="catalog-row"
                      onClick={(event) => {
                        if (canStartEdit && event.button === 0 && isRowClick(event)) {
                          startEdit(item);
                        }
                      }}
                    >
                      <td>{item.sortOrder}</td>
                      <td>
                        {isEditing ? (
                          <input
                            className="cell-input"
                            name="editCode"
                            aria-label={t('catalogs.management.edit.code', {
                              name: item.nameEs,
                            })}
                            value={editCode}
                            onChange={(e) => setEditCode(e.target.value)}
                            onKeyDown={(e) => onEditKeyDown(e, item)}
                            required
                          />
                        ) : (
                          item.code
                        )}
                      </td>
                      <td>
                        {isEditing ? (
                          <input
                            className="cell-input"
                            name="editNameEs"
                            aria-label={t('catalogs.management.edit.name', {
                              name: item.nameEs,
                            })}
                            value={editNameEs}
                            onChange={(e) => setEditNameEs(e.target.value)}
                            onKeyDown={(e) => onEditKeyDown(e, item)}
                            autoFocus
                            required
                          />
                        ) : (
                          // The row's keyboard target: the name reads as text and starts the edit.
                          <button
                            className="row-link"
                            type="button"
                            data-testid="catalog-edit"
                            aria-label={t('catalogs.management.action.editLabel', {
                              name: item.nameEs,
                            })}
                            disabled={isLocked}
                            onClick={() => startEdit(item)}
                          >
                            {item.nameEs}
                          </button>
                        )}
                      </td>
                      <td>
                        <span className="badge">
                          {t(
                            item.isActive
                              ? 'catalogs.management.status.active'
                              : 'catalogs.management.status.inactive',
                          )}
                        </span>
                      </td>
                      <td>
                        <div className="form-actions">
                          {isEditing ? (
                            <>
                              <button
                                className="button"
                                type="button"
                                data-testid="catalog-edit-save"
                                onClick={() => void saveEdit(item)}
                              >
                                {t('catalogs.management.action.save')}
                              </button>
                              <button
                                className="button ghost"
                                type="button"
                                data-testid="catalog-edit-cancel"
                                onClick={cancelEdit}
                              >
                                {t('catalogs.management.action.cancel')}
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                className="button ghost icon-button"
                                type="button"
                                data-testid="catalog-move-up"
                                aria-label={t('catalogs.management.action.moveUpLabel', {
                                  name: item.nameEs,
                                })}
                                title={t('catalogs.management.action.moveUp')}
                                disabled={isLocked}
                                onClick={() => void move(item, -1)}
                              >
                                <ArrowUpIcon />
                              </button>
                              <button
                                className="button ghost icon-button"
                                type="button"
                                data-testid="catalog-move-down"
                                aria-label={t('catalogs.management.action.moveDownLabel', {
                                  name: item.nameEs,
                                })}
                                title={t('catalogs.management.action.moveDown')}
                                disabled={isLocked}
                                onClick={() => void move(item, 1)}
                              >
                                <ArrowDownIcon />
                              </button>
                              <button
                                className={item.isActive ? 'button danger' : 'button secondary'}
                                type="button"
                                disabled={isLocked}
                                onClick={() => void toggle(item)}
                              >
                                {t(
                                  item.isActive
                                    ? 'catalogs.management.action.deactivate'
                                    : 'catalogs.management.action.activate',
                                )}
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
