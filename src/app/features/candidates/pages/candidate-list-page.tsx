import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { Pagination } from '../../../shared/components/pagination';
import { CandidateFiltersBar } from '../components/candidate-filters-bar';
import { CandidateTable } from '../components/candidate-table';
import { useCandidates } from '../use-candidates';
import { useSignal } from '../../../core/state/use-signal';
import {
  buildFilterChips,
  type CandidateFilters,
  EMPTY_FILTERS,
  filterCandidates,
  nextSort,
  paginate,
  removeFilter,
  type SortDirection,
  type SortField,
  sortCandidates,
  totalPages,
} from './candidate-list.logic';
import './candidate-list-page.css';

export function CandidateListPage() {
  const { toastService, confirmDialogService } = useServices();
  const candidateService = useCandidates();
  const allCandidates = useSignal(candidateService.candidates);

  const [filters, setFilters] = useState<CandidateFilters>(EMPTY_FILTERS);
  const [sort, setSort] = useState<{ field: SortField; direction: SortDirection }>({
    field: 'updatedAt',
    direction: 'desc',
  });
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  // Immutable: Angular re-rendered on Set mutation, React does not.
  const [selectedIds, setSelectedIds] = useState<ReadonlySet<string>>(() => new Set());

  const scoped = useMemo(
    () => (filters.includeInactive ? allCandidates : allCandidates.filter((c) => c.isActive)),
    [allCandidates, filters.includeInactive],
  );
  const filtered = useMemo(
    () => sortCandidates(filterCandidates(scoped, filters), sort.field, sort.direction),
    [scoped, filters, sort.field, sort.direction],
  );
  const pageCount = totalPages(filtered.length, pageSize);
  const paged = useMemo(() => paginate(filtered, page, pageSize), [filtered, page, pageSize]);
  const chips = useMemo(() => buildFilterChips(filters), [filters]);

  const canEdit = usePermission('edit_candidates');
  const canCreate = usePermission('create_candidates');
  const allVisibleSelected = paged.length > 0 && paged.every((item) => selectedIds.has(item.id));

  const patchFilters = (patch: Partial<CandidateFilters>): void => {
    setFilters((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  const toggleSelected = (candidateId: string, checked: boolean): void => {
    setSelectedIds((current) => {
      const next = new Set(current);
      if (checked) {
        next.add(candidateId);
      } else {
        next.delete(candidateId);
      }
      return next;
    });
  };

  const toggleSelectAll = (checked: boolean): void => {
    setSelectedIds((current) => {
      const next = new Set(current);
      for (const candidate of paged) {
        if (checked) {
          next.add(candidate.id);
        } else {
          next.delete(candidate.id);
        }
      }
      return next;
    });
  };

  const runBulk = async (mode: 'deactivate' | 'reactivate'): Promise<void> => {
    const isDeactivate = mode === 'deactivate';
    const actionWord = isDeactivate ? 'baja' : 'alta';

    if (!selectedIds.size) {
      toastService.show(
        `Selecciona al menos un candidato para aplicar ${actionWord} lógica.`,
        'warning',
      );
      return;
    }

    const selected = candidateService.list(true).filter((c) => selectedIds.has(c.id));
    if (!selected.length) {
      setSelectedIds(new Set());
      toastService.show('No hay candidatos válidos seleccionados.', 'warning');
      return;
    }

    const confirmation = await confirmDialogService.confirm({
      title: isDeactivate ? 'Confirmar baja lógica masiva' : 'Confirmar alta lógica masiva',
      message: `Se aplicará ${actionWord} lógica a ${selected.length} candidato(s).`,
      confirmText: isDeactivate ? 'Aplicar baja' : 'Aplicar alta',
      cancelText: 'Cancelar',
      danger: isDeactivate,
    });
    if (!confirmation) {
      return;
    }

    const ids = selected.map((c) => c.id);
    const updated = isDeactivate
      ? candidateService.deactivateMany(ids)
      : candidateService.reactivateMany(ids);
    setSelectedIds(new Set());
    toastService.show(
      `${isDeactivate ? 'Baja' : 'Alta'} lógica aplicada a ${updated} candidato(s).`,
      'success',
    );
    setPage(1);
  };

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>Candidatos</h1>
          <p className="muted">Alta, consulta, edición y baja lógica.</p>
        </div>
        {canCreate ? (
          <Link className="button" to="/app/candidates/new">
            Nuevo candidato
          </Link>
        ) : null}
      </div>

      <CandidateFiltersBar
        filters={filters}
        chips={chips}
        onPatch={patchFilters}
        onClear={() => {
          setFilters(EMPTY_FILTERS);
          setPage(1);
        }}
        onRemoveChip={(key) => {
          setFilters((current) => removeFilter(current, key));
          setPage(1);
        }}
      />

      <div className="toolbar">
        <p className="muted">
          Mostrando {paged.length} de {filtered.length} candidatos
          {!filters.includeInactive ? ' · Inactivos ocultos' : ''}
        </p>
        {!canEdit ? (
          <p className="empty-state">
            Modo solo lectura: puedes consultar perfiles pero no modificar candidatos.
          </p>
        ) : null}
        {canEdit ? (
          <div className="form-actions">
            <button
              className="button danger"
              type="button"
              disabled={selectedIds.size === 0}
              onClick={() => runBulk('deactivate')}
            >
              Baja lógica masiva ({selectedIds.size})
            </button>
            <button
              className="button secondary"
              type="button"
              disabled={selectedIds.size === 0}
              onClick={() => runBulk('reactivate')}
            >
              Alta lógica masiva ({selectedIds.size})
            </button>
          </div>
        ) : null}
      </div>

      {!filtered.length ? (
        <div className="empty-state">
          No hay candidatos con los filtros actuales. Ajusta filtros o crea un nuevo candidato.
        </div>
      ) : null}

      <CandidateTable
        candidates={paged}
        canEdit={canEdit}
        sort={sort}
        onSort={(field) => {
          setSort((current) => nextSort(current, field));
          setPage(1);
        }}
        selectedIds={selectedIds}
        allVisibleSelected={allVisibleSelected}
        onToggleSelected={toggleSelected}
        onToggleSelectAll={toggleSelectAll}
      />

      <Pagination
        page={page}
        pageCount={pageCount}
        pageSize={pageSize}
        onPageChange={(next) => setPage(Math.min(Math.max(next, 1), pageCount))}
        onPageSizeChange={(next) => {
          setPageSize(next);
          setPage(1);
        }}
      />
    </section>
  );
}
