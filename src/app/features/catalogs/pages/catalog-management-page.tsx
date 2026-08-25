import { useState, type FormEvent } from 'react';
import { useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
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

  const cancelEdit = (): void => {
    setEditingId('');
    setEditNameEs('');
    setEditCode('');
  };

  const createItem = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    try {
      catalogService.create(activeFamily, newNameEs, newCode);
      setNewNameEs('');
      setNewCode('');
      toastService.show('Elemento creado.', 'success');
    } catch (error) {
      notifyError(error, 'No se pudo crear el elemento.');
    }
  };

  const startEdit = (item: CatalogItem): void => {
    setEditingId(item.id);
    setEditNameEs(item.nameEs);
    setEditCode(item.code);
  };

  const saveEdit = (item: CatalogItem): void => {
    try {
      catalogService.update(activeFamily, item.id, { nameEs: editNameEs, code: editCode });
      cancelEdit();
      toastService.show('Elemento actualizado.', 'success');
    } catch (error) {
      notifyError(error, 'No se pudo actualizar el elemento.');
    }
  };

  const toggle = (item: CatalogItem): void => {
    try {
      const updated = catalogService.toggleActive(activeFamily, item.id);
      toastService.show(
        updated.isActive ? 'Elemento activado.' : 'Elemento desactivado.',
        'success',
      );
    } catch (error) {
      notifyError(error, 'No se pudo actualizar el estado.');
    }
  };

  const remove = async (item: CatalogItem): Promise<void> => {
    const confirmDelete = await confirmDialogService.confirm({
      title: 'Eliminar valor de catálogo',
      message: `Se eliminará "${item.nameEs}" y no podrás recuperarlo automáticamente.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });
    if (!confirmDelete) {
      return;
    }
    catalogService.remove(activeFamily, item.id);
    if (editingId === item.id) {
      cancelEdit();
    }
    toastService.show('Elemento eliminado.', 'success');
  };

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>Catálogos</h1>
          <p className="muted">
            Gestiona idiomas, programas, habilidades y listas maestras usadas por toda la app.
          </p>
        </div>
        <button
          className="button secondary"
          type="button"
          disabled={!editingId}
          onClick={cancelEdit}
        >
          Cancelar edición
        </button>
      </div>

      <div className="panel stack">
        <div className="toolbar">
          <div className="field field--wide">
            <label htmlFor="family">Familia de catálogo</label>
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
            Activos: {activeCount} · Total: {items.length}
          </p>
        </div>

        {editingId ? (
          <p className="empty-state">
            Modo edición activo. Guarda cambios o cancela antes de cambiar de familia.
          </p>
        ) : null}

        <form className="grid two" onSubmit={createItem} noValidate>
          <div className="field">
            <label htmlFor="newNameEs">Nombre (es)</label>
            <input
              id="newNameEs"
              name="newNameEs"
              value={newNameEs}
              onChange={(e) => setNewNameEs(e.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="newCode">Código (opcional)</label>
            <input
              id="newCode"
              name="newCode"
              value={newCode}
              placeholder="Se autogenera si lo dejas vacio"
              onChange={(e) => setNewCode(e.target.value)}
            />
          </div>
          <div className="form-actions span-all">
            <button className="button" type="submit" disabled={!!editingId}>
              Añadir
            </button>
          </div>
        </form>

        {!items.length ? (
          <p className="empty-state">No hay elementos en este catálogo.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Orden</th>
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => (
                  <tr key={item.id}>
                    <td>{item.sortOrder}</td>
                    <td>{item.code}</td>
                    <td>
                      {editingId === item.id ? (
                        <div className="grid two">
                          <input
                            name="editNameEs"
                            value={editNameEs}
                            onChange={(e) => setEditNameEs(e.target.value)}
                            required
                          />
                          <input
                            name="editCode"
                            value={editCode}
                            onChange={(e) => setEditCode(e.target.value)}
                            required
                          />
                        </div>
                      ) : (
                        item.nameEs
                      )}
                    </td>
                    <td>
                      <span className="badge">{item.isActive ? 'Activo' : 'Inactivo'}</span>
                    </td>
                    <td>
                      <div className="form-actions">
                        <button
                          className="button ghost"
                          type="button"
                          onClick={() => catalogService.move(activeFamily, item.id, -1)}
                        >
                          Subir
                        </button>
                        <button
                          className="button ghost"
                          type="button"
                          onClick={() => catalogService.move(activeFamily, item.id, 1)}
                        >
                          Bajar
                        </button>
                        {editingId === item.id ? (
                          <button className="button" type="button" onClick={() => saveEdit(item)}>
                            Guardar
                          </button>
                        ) : (
                          <button
                            className="button secondary"
                            type="button"
                            onClick={() => startEdit(item)}
                          >
                            Editar
                          </button>
                        )}
                        <button
                          className="button secondary"
                          type="button"
                          onClick={() => toggle(item)}
                        >
                          {item.isActive ? 'Desactivar' : 'Activar'}
                        </button>
                        <button
                          className="button danger"
                          type="button"
                          onClick={() => remove(item)}
                        >
                          Eliminar
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </section>
  );
}
