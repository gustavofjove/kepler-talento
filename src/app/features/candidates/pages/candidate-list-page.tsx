import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { AppError, toAppError } from '../../../shared/models/error.models';
import { Pagination } from '../../../shared/components/pagination';
import { CandidateFiltersBar } from '../components/candidate-filters-bar';
import { CandidateTable } from '../components/candidate-table';
import type { CandidateListPage as ListPage } from '../models/candidate.models';
import {
  buildFilterChips,
  type CandidateFilters,
  EMPTY_FILTERS,
  type ListView,
  nextSort,
  PAGE_SIZE_OPTIONS,
  readListView,
  removeFilter,
  toListQuery,
  writeListView,
} from './candidate-list.logic';
import './candidate-list-page.css';

/** Typing in the text filter asks the server once the user pauses, not per keystroke. */
const TEXT_DEBOUNCE_MS = 300;

type LoadState =
  | { status: 'loading'; page?: ListPage }
  | { status: 'loaded'; page: ListPage }
  | { status: 'error'; error: AppError };

/**
 * The candidate list, one server page at a time (KTL-18).
 *
 * Page, sort and the status, CV and inactive filters live in the URL and nowhere else, so
 * the back button, a reload and a pasted link all show the same view. The free-text filter
 * is the exception: it is personal data and stays out of the URL (see `LIST_PARAMS`).
 *
 * Selection is scoped to the page on screen. Any change of view clears it, because the rows
 * it named are no longer the rows the user can see.
 */
export function CandidateListPage() {
  const { candidateService, toastService, confirmDialogService } = useServices();
  const { t } = useTranslation();
  const notifyError = useErrorToast();
  const [searchParams, setSearchParams] = useSearchParams();

  const canEdit = usePermission('candidates.update');
  const canCreate = usePermission('candidates.create');
  const canIncludeRemoved = usePermission('candidates.delete');

  const [text, setText] = useState('');
  const [appliedText, setAppliedText] = useState('');
  useEffect(() => {
    const timer = setTimeout(() => setAppliedText(text), TEXT_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [text]);

  const urlKey = searchParams.toString();
  const view = useMemo(
    () => readListView(new URLSearchParams(urlKey), appliedText),
    [urlKey, appliedText],
  );
  const viewKey = JSON.stringify(toListQuery(view));

  const [load, setLoad] = useState<LoadState>({ status: 'loading' });
  // Bumped after a bulk action so the same view is fetched again.
  const [revision, setRevision] = useState(0);
  // Selection belongs to one view; a different key means it no longer applies.
  const [selection, setSelection] = useState<{ key: string; ids: ReadonlySet<string> }>({
    key: viewKey,
    ids: new Set(),
  });
  const selectedIds = selection.key === viewKey ? selection.ids : new Set<string>();

  useEffect(() => {
    const controller = new AbortController();
    setLoad((current) => ({
      status: 'loading',
      page: current.status === 'error' ? undefined : current.page,
    }));
    candidateService
      .listPage(JSON.parse(viewKey), controller.signal)
      .then((page) => {
        if (!controller.signal.aborted) {
          setLoad({ status: 'loaded', page });
        }
      })
      .catch((error: unknown) => {
        if (
          controller.signal.aborted ||
          (error instanceof AppError && error.code === 'CANCELLED')
        ) {
          return;
        }
        setLoad({ status: 'error', error: toAppError(error) });
      });
    return () => controller.abort();
  }, [candidateService, viewKey, revision]);

  // The URL most recently written. Two changes made before React re-renders (a sort click
  // straight after a filter change) must build on each other, not both on the last render —
  // otherwise the second silently undoes the first.
  const latestUrl = useRef(urlKey);
  latestUrl.current = urlKey;

  const navigate = useCallback(
    (change: (current: ListView) => ListView, replace = false): void => {
      const next = writeListView(change(readListView(new URLSearchParams(latestUrl.current))));
      latestUrl.current = next.toString();
      setSearchParams(next, { replace });
    },
    [setSearchParams],
  );

  const patchFilters = (patch: Partial<CandidateFilters>): void => {
    const { textFilter, ...rest } = patch;
    if (textFilter !== undefined) {
      setText(textFilter);
      // Typing is not navigation: only the page resets, and it replaces the history entry.
      if (view.page !== 1) {
        navigate((current) => ({ ...current, page: 1 }), true);
      }
    }
    if (Object.keys(rest).length) {
      navigate((current) => ({
        ...current,
        filters: { ...current.filters, ...rest },
        page: 1,
      }));
    }
  };

  const page = load.status === 'error' ? undefined : load.page;
  const items = page?.items ?? [];
  const totalCount = page?.totalCount ?? 0;
  const pageCount = Math.max(Math.ceil(totalCount / view.pageSize), 1);
  const filters: CandidateFilters = { ...view.filters, textFilter: text };
  const chips = buildFilterChips(filters, t);
  const allVisibleSelected =
    items.length > 0 && items.every((item) => selectedIds.has(item.candidateId));

  const setSelected = (ids: ReadonlySet<string>): void => setSelection({ key: viewKey, ids });

  const toggleSelected = (candidateId: string, checked: boolean): void => {
    const next = new Set(selectedIds);
    if (checked) {
      next.add(candidateId);
    } else {
      next.delete(candidateId);
    }
    setSelected(next);
  };

  const toggleSelectAll = (checked: boolean): void => {
    const next = new Set(selectedIds);
    for (const item of items) {
      if (checked) {
        next.add(item.candidateId);
      } else {
        next.delete(item.candidateId);
      }
    }
    setSelected(next);
  };

  const runBulk = async (mode: 'deactivate' | 'reactivate'): Promise<void> => {
    const isDeactivate = mode === 'deactivate';
    // Only rows on this page can be selected, so the selection is exactly what the user sees.
    const ids = items.map((item) => item.candidateId).filter((id) => selectedIds.has(id));
    if (!ids.length) {
      toastService.show(
        t(
          isDeactivate
            ? 'candidates.list.bulk.noneDeactivate'
            : 'candidates.list.bulk.noneReactivate',
        ),
        'warning',
      );
      return;
    }

    const confirmation = await confirmDialogService.confirm({
      title: t(
        isDeactivate
          ? 'candidates.list.bulk.confirmDeactivateTitle'
          : 'candidates.list.bulk.confirmReactivateTitle',
      ),
      message: t(
        isDeactivate
          ? 'candidates.list.bulk.confirmDeactivateMessage'
          : 'candidates.list.bulk.confirmReactivateMessage',
        { count: ids.length },
      ),
      confirmText: t(
        isDeactivate
          ? 'candidates.list.bulk.applyDeactivate'
          : 'candidates.list.bulk.applyReactivate',
      ),
      cancelText: t('candidates.list.bulk.cancel'),
      danger: isDeactivate,
    });
    if (!confirmation) {
      return;
    }

    try {
      const updated = isDeactivate
        ? await candidateService.deactivateMany(ids)
        : await candidateService.reactivateMany(ids);
      setSelected(new Set());
      toastService.show(
        t(isDeactivate ? 'candidates.list.bulk.deactivated' : 'candidates.list.bulk.reactivated', {
          count: updated,
        }),
        'success',
      );
      setRevision((current) => current + 1);
    } catch (error) {
      // A refusal partway through leaves the earlier candidates applied, which is the
      // existing contract; the selection is kept so the user can see what remains.
      notifyError(error, t('candidates.list.bulk.failed'));
      setRevision((current) => current + 1);
    }
  };

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>{t('candidates.list.title')}</h1>
          <p className="muted">{t('candidates.list.subtitle')}</p>
        </div>
        {canCreate ? (
          <Link className="button" to="/app/candidates/new">
            {t('candidates.list.new')}
          </Link>
        ) : null}
      </div>

      <CandidateFiltersBar
        filters={filters}
        chips={chips}
        canIncludeInactive={canIncludeRemoved}
        onPatch={patchFilters}
        onClear={() => {
          setText('');
          setAppliedText('');
          navigate((current) => ({ ...current, filters: EMPTY_FILTERS, page: 1 }));
        }}
        onRemoveChip={(key) => {
          if (key === 'text') {
            setText('');
            return;
          }
          navigate((current) => ({
            ...current,
            filters: removeFilter(current.filters, key),
            page: 1,
          }));
        }}
      />

      <div className="toolbar">
        <p className="muted" data-testid="candidate-list-total">
          {t('candidates.list.total', { shown: items.length, count: totalCount })}
          {!view.filters.includeInactive ? ` · ${t('candidates.list.inactiveHidden')}` : ''}
        </p>
        {!canEdit ? <p className="empty-state">{t('candidates.list.readOnly')}</p> : null}
        {canEdit ? (
          <div className="form-actions">
            <span className="muted" data-testid="candidate-selection-count">
              {t('candidates.list.selectedOnPage', { count: selectedIds.size })}
            </span>
            <button
              className="button danger"
              type="button"
              disabled={selectedIds.size === 0}
              onClick={() => void runBulk('deactivate')}
            >
              {t('candidates.list.bulk.deactivate', { count: selectedIds.size })}
            </button>
            <button
              className="button secondary"
              type="button"
              disabled={selectedIds.size === 0}
              onClick={() => void runBulk('reactivate')}
            >
              {t('candidates.list.bulk.reactivate', { count: selectedIds.size })}
            </button>
          </div>
        ) : null}
      </div>

      {load.status === 'loading' && !page ? (
        <div className="empty-state">{t('candidates.list.loading')}</div>
      ) : load.status === 'error' ? (
        <div className="empty-state" data-testid="candidate-list-error">
          {load.error.message || t('candidates.list.error')}
        </div>
      ) : !totalCount ? (
        <div className="empty-state" data-testid="candidate-list-empty">
          {t('candidates.list.empty')}
        </div>
      ) : !items.length ? (
        <div className="empty-state" data-testid="candidate-list-past-end">
          {t('candidates.list.pastEnd')}
        </div>
      ) : null}

      <CandidateTable
        candidates={items}
        canEdit={canEdit}
        sort={view.sort}
        onSort={(field) =>
          navigate((current) => ({ ...current, sort: nextSort(current.sort, field), page: 1 }))
        }
        selectedIds={selectedIds}
        allVisibleSelected={allVisibleSelected}
        onToggleSelected={toggleSelected}
        onToggleSelectAll={toggleSelectAll}
      />

      <Pagination
        page={view.page}
        pageCount={pageCount}
        pageSize={view.pageSize}
        pageSizes={PAGE_SIZE_OPTIONS}
        onPageChange={(next) => navigate((current) => ({ ...current, page: Math.max(next, 1) }))}
        onPageSizeChange={(next) =>
          navigate((current) => ({ ...current, pageSize: next, page: 1 }))
        }
      />
    </section>
  );
}
