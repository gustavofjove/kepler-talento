import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatDate } from '../../../core/i18n/format';
import '../../../shared/components/data-table.css';
import { useRowLink } from '../../../shared/components/row-link';
import { mailtoHref } from '../contact-links';
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
  const rowLink = useRowLink();
  const sortButton = (field: SortField, label: string) => (
    <button
      className="th-sort"
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
      <table className="data-table" data-testid="candidate-table">
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
          </tr>
        </thead>
        <tbody>
          {candidates.length ? (
            candidates.map((candidate) => (
              // The whole row opens the candidate; the name is its keyboard link.
              <tr
                key={candidate.candidateId}
                className="row-link-row"
                data-testid="candidate-row"
                onClick={rowLink(`/app/candidates/${candidate.candidateId}`)}
                onAuxClick={rowLink(`/app/candidates/${candidate.candidateId}`)}
              >
                {canEdit ? (
                  // A near-miss around the checkbox only ever selects.
                  <td data-row-link-ignore="">
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
                  <Link className="row-link" to={`/app/candidates/${candidate.candidateId}`}>
                    {candidate.firstName} {candidate.lastName}
                  </Link>
                  {candidate.email ? (
                    <div className="muted contact-links">
                      <a href={mailtoHref(candidate.email)}>{candidate.email}</a>
                    </div>
                  ) : null}
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
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={canEdit ? 6 : 5} className="muted">
                {t('candidates.list.noRows')}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
