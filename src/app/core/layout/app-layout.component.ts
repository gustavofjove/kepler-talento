import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { ToastService } from '../services/toast.service';

@Component({
  selector: 'rrhh-app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  styles: [
    `
      .shell {
        min-height: 100vh;
      }
      header {
        align-items: center;
        background: var(--fj-navy);
        border-bottom: 1px solid var(--fj-navy-90);
        display: flex;
        gap: 18px;
        justify-content: space-between;
        min-height: 64px;
        padding: 0 22px;
      }
      .brand {
        display: grid;
        gap: 2px;
      }
      .brand strong {
        color: var(--bg-1);
        font-family: var(--font-display);
        font-size: 18px;
      }
      .brand .muted {
        color: var(--fg-3);
      }
      nav {
        display: flex;
        flex-wrap: wrap;
        gap: 6px;
      }
      nav a {
        border-left: 3px solid transparent;
        border-radius: var(--r-md);
        color: #d8dee8;
        font-family: var(--font-display);
        font-size: 13px;
        font-weight: 600;
        padding: 8px 10px;
        text-decoration: none;
      }
      nav a:hover {
        background: var(--fj-navy-90);
      }
      nav a.active {
        background: var(--fj-navy-90);
        border-left-color: var(--fj-orange-bright);
        color: var(--fj-orange-bright);
        font-weight: 700;
      }
      main {
        margin: 0 auto;
        max-width: 1180px;
        padding: 22px;
      }
      .user {
        align-items: center;
        display: flex;
        gap: 10px;
      }
      .user .badge {
        background: rgba(224, 132, 63, 0.15);
        color: var(--fj-orange-bright);
      }
      .toasts {
        bottom: 20px;
        display: grid;
        gap: 8px;
        position: fixed;
        right: 20px;
        width: min(360px, calc(100vw - 40px));
        z-index: 20;
      }
      .toast {
        background: var(--fj-navy);
        border-radius: var(--r-lg);
        color: white;
        padding: 12px;
      }
    `,
  ],
  template: `
    <div class="shell">
      <header>
        <div class="brand">
          <strong>Kepker Talento</strong>
          <span class="muted">Gestion interna de candidatos del ecosistema Kepker</span>
        </div>
        <nav>
          <a
            routerLink="/app"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: true }"
          >
            Dashboard
          </a>
          <a routerLink="/app/candidates" routerLinkActive="active">Candidatos</a>
          <a routerLink="/app/search" routerLinkActive="active">Busqueda</a>
          <a routerLink="/app/catalogs" routerLinkActive="active">Catalogos</a>
          <a routerLink="/app/admin/users" routerLinkActive="active">Usuarios</a>
          <a routerLink="/app/admin/import" routerLinkActive="active">Importacion</a>
        </nav>
        <div class="user">
          <span class="badge">{{ auth.profile()?.role }}</span>
          <button class="button secondary" type="button" (click)="auth.signOut()">Salir</button>
        </div>
      </header>
      <main>
        <router-outlet />
      </main>
      <div class="toasts">
        @for (message of toast.messages(); track message.id) {
          <button class="toast" type="button" (click)="toast.dismiss(message.id)">
            {{ message.text }}
          </button>
        }
      </div>
    </div>
  `,
})
export class AppLayoutComponent {
  constructor(
    readonly auth: AuthService,
    readonly toast: ToastService,
  ) {}
}
