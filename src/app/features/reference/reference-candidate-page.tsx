import { useEffect, useState } from 'react';
import { useErrorToast } from '../../core/services/use-error-toast';
import { useReferenceCandidate } from './use-reference-candidate';

const REFERENCE_ID = '11111111-1111-4111-8111-111111111111';

export function ReferenceCandidatePage() {
  const { candidate, loading, service } = useReferenceCandidate();
  const reportError = useErrorToast();
  const [requested, setRequested] = useState(false);

  useEffect(() => {
    if (!requested) return;
    const controller = new AbortController();
    void service.load(REFERENCE_ID, controller.signal).catch((error: unknown) => {
      if (!controller.signal.aborted)
        reportError(error, 'No se ha podido cargar el candidato de referencia.');
    });
    return () => controller.abort();
  }, [reportError, requested, service]);

  return (
    <main data-testid="reference-candidate-page">
      <h1>Integración de referencia</h1>
      <button
        name="loadReferenceCandidate"
        type="button"
        onClick={() => setRequested(true)}
        disabled={loading}
      >
        {loading ? 'Cargando…' : 'Probar API'}
      </button>
      {candidate ? (
        <p data-testid="reference-candidate-result">
          {candidate.firstName} {candidate.lastName}
        </p>
      ) : null}
    </main>
  );
}
