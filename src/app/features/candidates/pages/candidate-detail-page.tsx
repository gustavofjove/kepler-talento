import { Link, useParams } from 'react-router';
import { usePermission, useServices } from '../../../core/di/services-context';
import { useCandidates } from '../use-candidates';
import { CandidateDocuments } from '../components/candidate-documents';
import { CandidateEducation } from '../components/candidate-education';
import { CandidateExperience } from '../components/candidate-experience';
import { CandidateLanguages } from '../components/candidate-languages';
import { CandidatePrograms } from '../components/candidate-programs';
import { CandidateSkills } from '../components/candidate-skills';

export function CandidateDetailPage() {
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const { confirmDialogService } = useServices();
  const candidateService = useCandidates();

  const item = candidateService.find(candidateId);
  const canEdit = usePermission('edit_candidates');

  const setActive = async (active: boolean): Promise<void> => {
    if (!item) {
      return;
    }
    const confirmed = await confirmDialogService.confirm({
      title: active ? 'Aplicar alta lógica' : 'Aplicar baja lógica',
      message: active
        ? 'El candidato volverá a aparecer en listados activos.'
        : 'El candidato dejará de aparecer en listados activos.',
      confirmText: active ? 'Aplicar alta' : 'Aplicar baja',
      cancelText: 'Cancelar',
      danger: !active,
    });
    if (!confirmed) {
      return;
    }
    if (active) {
      candidateService.reactivate(item.id);
    } else {
      candidateService.deactivate(item.id);
    }
  };

  if (!item) {
    return (
      <section className="panel">
        <h1>Candidato no encontrado</h1>
        <Link className="button" to="/app/candidates">
          Volver
        </Link>
      </section>
    );
  }

  return (
    <section className="page">
      <div className="toolbar">
        <div className="page-header">
          <h1>
            {item.firstName} {item.lastName}
          </h1>
          <p className="muted">
            {item.email} · {item.phone}
          </p>
        </div>
        <div className="toolbar">
          {canEdit ? (
            <>
              <Link className="button secondary" to={`/app/candidates/${item.id}/edit`}>
                Editar
              </Link>
              {item.isActive ? (
                <button className="button danger" type="button" onClick={() => setActive(false)}>
                  Baja lógica
                </button>
              ) : (
                <button className="button secondary" type="button" onClick={() => setActive(true)}>
                  Alta lógica
                </button>
              )}
            </>
          ) : null}
        </div>
      </div>
      <div className="grid two">
        <article className="panel">
          <h2>Datos principales</h2>
          <p>
            <strong>Estado:</strong> {item.status}
          </p>
          <p>
            <strong>Disponibilidad:</strong> {item.availability}
          </p>
          <p>
            <strong>Localidad:</strong> {item.location} {item.province}
          </p>
          <p>
            <strong>Recepción:</strong> {item.receivedAt || 'Pendiente'}
          </p>
          <p>
            <strong>Revisión:</strong> {item.reviewDueAt || 'Pendiente'}
          </p>
          <p>{item.notes}</p>
        </article>
        <article className="panel">
          <h2>Auditoría</h2>
          <p>
            <strong>Creado:</strong> {item.createdAt.slice(0, 19)}
          </p>
          <p>
            <strong>Actualizado:</strong> {item.updatedAt.slice(0, 19)}
          </p>
          <p>
            <strong>Activo:</strong> {item.isActive ? 'Si' : 'No'}
          </p>
        </article>
      </div>
      <div className="grid two">
        <article className="panel">
          <CandidateLanguages candidateId={item.id} languages={item.languages} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidatePrograms candidateId={item.id} programs={item.programs} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateEducation candidateId={item.id} education={item.education} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateExperience
            candidateId={item.id}
            experience={item.experience}
            canEdit={canEdit}
          />
        </article>
        <article className="panel">
          <CandidateSkills candidateId={item.id} skills={item.skills} canEdit={canEdit} />
        </article>
        <article className="panel">
          <CandidateDocuments candidate={item} />
        </article>
      </div>
    </section>
  );
}
