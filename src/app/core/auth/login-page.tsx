import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { DEFAULT_ROLES } from '../../shared/models/auth.models';
import { useServices } from '../di/services-context';
import './login-page.css';

export function LoginPage() {
  const { authService } = useServices();
  const navigate = useNavigate();

  const [email, setEmail] = useState('rrhh.admin@example.com');
  const [password, setPassword] = useState('local-demo');
  const [role, setRole] = useState('rrhh_admin');
  const [error, setError] = useState('');

  const submit = async (event: FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();
    try {
      await authService.signIn(email, password, role);
      await navigate('/app');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo iniciar sesión');
    }
  };

  return (
    <section className="login">
      <form className="panel box" onSubmit={submit}>
        <h1>Kepker Talento</h1>
        <p className="muted">Acceso interno. En local puedes entrar con cualquier email.</p>
        <div className="grid">
          <div className="field">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              name="email"
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="password">Contraseña</label>
            <input
              id="password"
              name="password"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
            />
          </div>
          <div className="field">
            <label htmlFor="role">Rol local</label>
            <select
              id="role"
              name="role"
              value={role}
              onChange={(event) => setRole(event.target.value)}
            >
              {DEFAULT_ROLES.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.label}
                </option>
              ))}
            </select>
          </div>
          {error ? <p className="muted">{error}</p> : null}
          <button className="button" type="submit">
            Entrar
          </button>
        </div>
      </form>
    </section>
  );
}
