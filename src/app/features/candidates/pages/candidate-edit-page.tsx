import { useNavigate, useParams } from 'react-router';
import { useCandidates } from '../use-candidates';
import { CandidateForm } from '../components/candidate-form';
import type { CandidateDraft } from '../models/candidate.models';

export function CandidateEditPage() {
  // The '' default reproduces Angular's @Input('id') default on /candidates/new,
  // which is what selects the heading below.
  const { id: candidateId = '' } = useParams<{ id: string }>();
  const candidateService = useCandidates();
  const navigate = useNavigate();

  const candidate = candidateId ? candidateService.find(candidateId) : undefined;

  const save = async (draft: CandidateDraft): Promise<void> => {
    const saved = candidateId
      ? candidateService.update(candidateId, draft)
      : candidateService.create(draft);
    await navigate(`/app/candidates/${saved.id}`);
  };

  return (
    <section className="page">
      <div className="page-header">
        <h1>{candidateId ? 'Editar candidato' : 'Alta de candidato'}</h1>
        <p className="muted">Los cambios quedan preparados para auditoría y RLS.</p>
      </div>
      <div className="panel">
        {/* key remounts the form when navigating between new and edit. */}
        <CandidateForm key={candidateId || 'new'} candidate={candidate} onSave={save} />
      </div>
    </section>
  );
}
