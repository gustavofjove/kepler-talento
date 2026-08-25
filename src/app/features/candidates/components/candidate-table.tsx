import { Link } from 'react-router';
import type { Candidate } from '../models/candidate.models';
import { type SortDirection, type SortField, sortIndicator } from '../pages/candidate-list.logic';

interface Props {
  candidates: Candidate[];
  canEdit: boolean;
  sort: { field: SortField; direction: SortDirection };
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
  const sortButton = (field: SortField, label: string) => (
    <button className="button ghost" type="button" onClick={() => onSort(field)}>
      {label} {sortIndicator(sort, field)}
    </button>
  );

  return (
    <div className="panel table-wrap">
      <table>
        <thead>
          <tr>
            {canEdit ? (
              <th>
                <input
                  type="checkbox"
                  checked={allVisibleSelected}
                  onChange={(event) => onToggleSelectAll(event.target.checked)}
                />
              </th>
            ) : null}
            <th>{sortButton('lastName', 'Nombre')}</th>
            <th>Teléfono</th>
            <th>{sortButton('status', 'Estado')}</th>
            <th>CV</th>
            <th>{sortButton('updatedAt', 'Actualizado')}</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {candidates.length ? (
            candidates.map((candidate) => (
              <tr key={candidate.id}>
                {canEdit ? (
                  <td>
                    <input
                      type="checkbox"
                      checked={selectedIds.has(candidate.id)}
                      onChange={(event) => onToggleSelected(candidate.id, event.target.checked)}
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
                  <span className="badge">{candidate.status}</span>
                  {!candidate.isActive ? (
                    <span className="badge inactive-badge">Inactivo</span>
                  ) : null}
                </td>
                <td>{candidate.documents.length ? 'Disponible' : 'Pendiente'}</td>
                <td>{candidate.updatedAt.slice(0, 10)}</td>
                <td>
                  <Link className="button secondary" to={`/app/candidates/${candidate.id}`}>
                    Abrir
                  </Link>
                </td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={canEdit ? 7 : 6} className="muted">
                Sin candidatos para mostrar.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
