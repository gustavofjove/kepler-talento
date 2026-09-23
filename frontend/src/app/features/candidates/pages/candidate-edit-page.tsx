import { useNavigate, useParams } from 'react-router';
import { useErrorToast } from '../../../core/services/use-error-toast';
import { useCandidate } from '../use-candidates';
import { CandidateForm } from '../components/candidate-form';
import type { CandidateDraft } from '../models/candidate.models';

export function CandidateEditPage() {
  // The '' default reproduces Angular's @Input('id') default on /candidates/new,
  // which is what selects the heading below.
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const candidateService = useCandidate(candidateId);
  const notifyError = useErrorToast();
  const navigate = useNavigate();

  const candidate = candidateId ? candidateService.find(candidateId) : undefined;
  // The form snapshots its draft from `candidate` on first render, so it must not be
  // rendered until the aggregate has arrived — otherwise it initialises empty and never
  // picks the candidate up.
  const aggregate = candidateId ? candidateService.aggregateStatus(candidateId) : 'loaded';
  const isLoading = aggregate === 'loading';
  const hasFailed = aggregate === 'error';
  const isMissing = aggregate === 'missing';

  const save = async (draft: CandidateDraft): Promise<void> => {
    try {
      // The version travels with the loaded aggregate inside the service, so a form
      // opened before someone else's edit is refused rather than overwriting it.
      const saved = candidateId
        ? await candidateService.update(candidateId, draft)
        : await candidateService.create(draft);
      await navigate(`/app/candidates/${saved.id}`);
    } catch (error) {
      notifyError(error, 'No se ha podido guardar el candidato.');
    }
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>{candidateId ? 'Editar candidato' : 'Alta de candidato'}</h1>
        <p className="muted">Los cambios quedan preparados para auditoría y RLS.</p>
      </div>
      <div className="panel">
        {isLoading ? (
          <p className="empty-state">Cargando candidato…</p>
        ) : hasFailed ? (
          <p className="empty-state" data-testid="candidate-edit-error">
            {candidateService.error?.message ?? 'No se ha podido cargar el candidato.'}
          </p>
        ) : isMissing ? (
          <p className="empty-state">Candidato no encontrado.</p>
        ) : (
          /* key remounts the form when navigating between new and edit. */
          <CandidateForm key={candidateId || 'new'} candidate={candidate} onSave={save} />
        )}
      </div>
    </section>
  );
}
