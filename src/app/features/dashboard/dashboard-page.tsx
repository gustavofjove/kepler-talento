import { Link } from 'react-router';
import { useCandidates } from '../candidates/use-candidates';

export function DashboardPage() {
  // These counts are cheap enough that plain in-render computation beats
  // memoising with stale-dep risk.
  const candidateService = useCandidates();

  const today = new Date().toISOString().slice(0, 10);
  const month = today.slice(0, 7);

  const active = candidateService.list();
  const activeCount = active.length;
  const inactiveCount = candidateService.list(true).filter((c) => !c.isActive).length;
  const withoutCv = active.filter((c) => c.documentCount === 0).length;
  const pendingReview = active.filter((c) => c.reviewDueAt && c.reviewDueAt < today).length;
  const withPrimaryCv = active.filter((c) => c.primaryDocumentId).length;
  const receivedThisMonth = active.filter((c) => c.receivedAt.startsWith(month)).length;

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>Dashboard</h1>
          <p className="muted">Resumen operativo de candidatos y CVs.</p>
        </div>
        <Link className="button" to="/app/candidates/new">
          Alta de candidato
        </Link>
      </div>
      <div className="grid four">
        <article className="panel kpi-card">
          <span className="kpi-label">Candidatos activos</span>
          <span className="kpi-value">{activeCount}</span>
        </article>
        <article className="panel kpi-card">
          <span className="kpi-label">Sin CV adjunto</span>
          <span className="kpi-value">{withoutCv}</span>
        </article>
        <article className="panel kpi-card">
          <span className="kpi-label">Pendientes de revisión</span>
          <span className="kpi-value">{pendingReview}</span>
        </article>
        <article className="panel kpi-card">
          <span className="kpi-label">Recibidos este mes</span>
          <span className="kpi-value">{receivedThisMonth}</span>
        </article>
      </div>

      <div className="grid three">
        <article className="panel stack">
          <h2>Centro operativo</h2>
          <p className="muted">Accesos rápidos para las tareas diarias de RRHH.</p>
          <Link className="button secondary" to="/app/candidates">
            Gestionar listado
          </Link>
          <Link className="button secondary" to="/app/search">
            Búsqueda avanzada
          </Link>
        </article>
        <article className="panel kpi-card">
          <span className="kpi-label">Inactivos</span>
          <span className="kpi-value">{inactiveCount}</span>
        </article>
        <article className="panel kpi-card">
          <span className="kpi-label">Con CV principal</span>
          <span className="kpi-value">{withPrimaryCv}</span>
        </article>
      </div>
    </section>
  );
}
