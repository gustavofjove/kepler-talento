import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { formatDate } from '../../../core/i18n/format';
import '../../../shared/components/data-table.css';
import { useRowLink } from '../../../shared/components/row-link';
import { displayPhone, mailtoHref } from '../contact-links';
import type { CandidateListItem } from '../models/candidate.models';
import { RowCvActions, RowCvInlineRow } from './row-cv-preview/row-cv-preview';
import { useRowCvTable } from './row-cv-preview/row-cv-preview.context';
import {
  type ListSort,
  type SortField,
  sortIndicator,
  statusLabel,
} from '../pages/candidate-list.logic';

/** This table's key in the page's row CV preview (KTL-35). */
const CANDIDATE_TABLE_ID = 'candidates';

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
  const cv = useRowCvTable(
    CANDIDATE_TABLE_ID,
    candidates.map((candidate) => candidate.candidateId),
  );
  const columnCount = 4 + (canEdit ? 1 : 0) + (cv.enabled ? 1 : 0);
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
            <th aria-sort={ariaSort('updatedAt')}>
              {sortButton('updatedAt', t('candidates.list.column.updatedAt'))}
            </th>
            {cv.enabled ? <th>{t('candidates.list.column.cv')}</th> : null}
          </tr>
        </thead>
        <tbody>
          {candidates.length ? (
            candidates.map((candidate) => (
              <Fragment key={candidate.candidateId}>
                {/* The whole row opens the candidate; the name is its keyboard link. */}
                <tr
                  ref={cv.rowRef(candidate.candidateId)}
                  className={
                    cv.isOpen(candidate.candidateId) ? 'row-link-row is-cv-open' : 'row-link-row'
                  }
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
                  <td>{displayPhone(candidate.phone)}</td>
                  <td>
                    <span className="badge">{statusLabel(candidate.status, t)}</span>
                    {!candidate.isActive ? (
                      <span className="badge inactive-badge">{t('candidates.list.inactive')}</span>
                    ) : null}
                  </td>
                  <td>{formatDate(candidate.updatedAt)}</td>
                  {cv.enabled ? (
                    <td data-row-link-ignore="">
                      <RowCvActions
                        tableId={CANDIDATE_TABLE_ID}
                        candidateId={candidate.candidateId}
                        name={`${candidate.firstName} ${candidate.lastName}`}
                        downloadable={candidate.primaryCvDownloadable}
                        previewable={candidate.primaryCvPreviewable}
                      />
                    </td>
                  ) : null}
                </tr>
                <RowCvInlineRow
                  tableId={CANDIDATE_TABLE_ID}
                  candidateId={candidate.candidateId}
                  colSpan={columnCount}
                />
              </Fragment>
            ))
          ) : (
            <tr>
              <td colSpan={columnCount} className="muted">
                {t('candidates.list.noRows')}
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
