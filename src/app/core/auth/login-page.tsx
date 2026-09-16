import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { useServices } from '../di/services-context';
import { useErrorToast } from '../services/use-error-toast';
import './login-page.css';

export function LoginPage() {
  const { t } = useTranslation();
  const { authService } = useServices();
  const navigate = useNavigate();
  const notifyError = useErrorToast();

  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      setBusy(true);
      await authService.signIn();
      await navigate('/app');
    } catch (err) {
      setError(notifyError(err, t('auth.login.failed')));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="login">
      <form className="panel box" onSubmit={submit}>
        <h1>{t('app.title')}</h1>
        <p className="muted">{t('auth.login.intro')}</p>
        <div className="grid">
          {error ? <p className="muted">{error}</p> : null}
          <button className="button" type="submit" disabled={busy} data-testid="sign-in">
            {t('auth.login.submit')}
          </button>
        </div>
      </form>
    </section>
  );
}
