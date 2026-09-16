import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatDate } from '../../../core/i18n/format';
import type { CandidateListItem } from '../models/candidate.models';
import {
  type ListSort,
  type SortField,
  sortIndicator,
  statusLabel,
} from '../pages/candidate-list.logic';

interface Props {
  candidates: CandidateListItem[];
  canEdit: boolean;
  sort: ListSort;
  onSort: (field: SortField) => void;
  selectedIds: ReadonlySet<string>;
  allVisibleSelected: boolean;
  onToggleSelected: (candidateId: string, checked: boolean) => void;
  onToggleSelectAll: (checked: boolean) => void;
}

export function CandidateTable({
  candidates,
  canEdit,
  sort,
  onSort,
  selectedIds,
  allVisibleSelected,
  onToggleSelected,
  onToggleSelectAll,
}: Props) {
  const { t } = useTranslation();
  const sortButton = (field: SortField, label: string) => (
    <button
      className="button ghost"
      type="button"
      name={`sort-${field}`}
      data-testid={`candidate-sort-${field}`}
      onClick={() => onSort(field)}
    >
      {label} {sortIndicator(sort, field)}
    </button>
  );
  const ariaSort = (field: SortField) =>
    sort.field === field ? (sort.direction === 'asc' ? 'ascending' : 'descending') : 'none';

  return (
    <div className="panel table-wrap">
      <table data-testid="candidate-table">
        <thead>
          <tr>
            {canEdit ? (
              <th>
                <input
                  type="checkbox"
                  aria-label={t('candidates.list.selectPage')}
                  checked={allVisibleSelected}
                  onChange={(event) => onToggleSelectAll(event.target.checked)}
                />
              </th>
            ) : null}
            <th aria-sort={ariaSort('lastName')}>
              {sortButton('lastName', t('candidates.list.column.name'))}
            </th>
            <th>{t('candidates.list.column.phone')}</th>
            <th aria-sort={ariaSort('status')}>
              {sortButton('status', t('candidates.list.column.status'))}
            </th>
            <th>{t('candidates.list.column.cv')}</th>
            <th aria-sort={ariaSort('updatedAt')}>
              {sortButton('updatedAt', t('candidates.list.column.updatedAt'))}
            </th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {candidates.length ? (
            candidates.map((candidate) => (
              <tr key={candidate.candidateId} data-testid="candidate-row">
                {canEdit ? (
                  <td>
                    <input
                      type="checkbox"
                      aria-label={t('candidates.list.selectRow', {
                        name: `${candidate.firstName} ${candidate.lastName}`,
                      })}
                      checked={selectedIds.has(candidate.candidateId)}
                      onChange={(event) =>
                        onToggleSelected(candidate.candidateId, event.target.checked)
                      }
                    />
                  </td>
                ) : null}
                <td>
                  <strong>
                    {candidate.firstName} {candidate.lastName}
                  </strong>
                  <div className="muted">{candidate.email}</div>
                </td>
                <td>{candidate.phone}</td>
                <td>
                  <span className="badge">{statusLabel(candidate.status, t)}</span>
                  {!candidate.isActive ? (
                    <span className="badge inactive-badge">{t('candidates.list.inactive')}</span>
                  ) : null}
                </td>
                <td>
                  {candidate.hasPrimaryCv
                    ? t('candidates.list.cv.available')
                    : t('candidates.list.cv.pending')}
                </td>
                <td>{formatDate(candidate.updatedAt)}</td>
                <td>
                  <Link
                    className="button secondary"
                    to={`/app/candidates/${candidate.candidateId}`}
                  >
                    {t('candidates.list.open')}
                  </Link>
                </td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={canEdit ? 7 : 6} className="muted">
                {t('candidates.list.noRows')}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
