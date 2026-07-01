import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DEFAULT_ROLES } from '../../shared/models/auth.models';
import { AuthService } from './auth.service';

@Component({
  selector: 'rrhh-login-page',
  standalone: true,
  imports: [FormsModule],
  styles: [
    `
      .login {
        align-items: center;
        background:
          linear-gradient(180deg, rgba(22, 33, 54, 0.06) 0%, rgba(22, 33, 54, 0) 30%), var(--bg-3);
        display: grid;
        min-height: 100vh;
        padding: 24px;
      }
      .box {
        margin: 0 auto;
        max-width: 440px;
        width: 100%;
      }
      h1 {
        color: var(--fj-navy);
        font-family: var(--font-display);
        margin-top: 0;
      }
    `,
  ],
  template: `
    <section class="login">
      <form class="panel box" (ngSubmit)="submit()">
        <h1>Kepker Talento</h1>
        <p class="muted">Acceso interno. En local puedes entrar con cualquier email.</p>
        <div class="grid">
          <div class="field">
            <label for="email">Email</label>
            <input id="email" name="email" type="email" [(ngModel)]="email" required />
          </div>
          <div class="field">
            <label for="password">Contrasena</label>
            <input id="password" name="password" type="password" [(ngModel)]="password" required />
          </div>
          <div class="field">
            <label for="role">Rol local</label>
            <select id="role" name="role" [(ngModel)]="role">
              @for (item of roles; track item.name) {
                <option [value]="item.name">{{ item.label }}</option>
              }
            </select>
          </div>
          @if (error) {
            <p class="muted">{{ error }}</p>
          }
          <button class="button" type="submit">Entrar</button>
        </div>
      </form>
    </section>
  `,
})
export class LoginPageComponent {
  readonly roles = DEFAULT_ROLES;
  email = 'rrhh.admin@example.com';
  password = 'local-demo';
  role = 'rrhh_admin';
  error = '';

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
  ) {}

  async submit(): Promise<void> {
    try {
      await this.auth.signIn(this.email, this.password, this.role);
      await this.router.navigateByUrl('/app');
    } catch (error) {
      this.error = error instanceof Error ? error.message : 'No se pudo iniciar sesion';
    }
  }
}
