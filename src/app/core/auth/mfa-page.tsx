import { Link } from 'react-router';

export function MfaPage() {
  return (
    <section className="page">
      <div className="panel section-block">
        <div className="page-header">
          <h1>Verificación MFA</h1>
        </div>
        <p className="muted">
          El flujo TOTP queda preparado para Supabase Auth. En modo local se considera verificado.
        </p>
        <div className="form-actions">
          <Link className="button" to="/app">
            Continuar
          </Link>
        </div>
      </div>
    </section>
  );
}
