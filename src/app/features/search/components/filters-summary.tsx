import type { SummaryGroup } from './filters-summary.logic';

export function FiltersSummary({ groups }: { groups: SummaryGroup[] }) {
  return (
    <div className="filters-summary" data-testid="filters-summary" aria-label="Resumen de filtros">
      {groups.length ? (
        groups.map((group) => (
          <div className="summary-group" key={group.label}>
            <h3>{group.label}</h3>
            <div className="summary-values">
              {group.values.map((value) => (
                <span className="chip" key={value}>
                  {value}
                </span>
              ))}
            </div>
          </div>
        ))
      ) : (
        <p className="empty-state">Sin filtros aplicados.</p>
      )}
    </div>
  );
}
