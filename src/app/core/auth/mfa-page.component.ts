import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'rrhh-mfa-page',
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="page">
      <div class="panel section-block">
        <div class="page-header">
          <h1>Verificacion MFA</h1>
        </div>
        <p class="muted">
          El flujo TOTP queda preparado para Supabase Auth. En modo local se considera verificado.
        </p>
        <div class="form-actions">
          <a class="button" routerLink="/app">Continuar</a>
        </div>
      </div>
    </section>
  `,
})
export class MfaPageComponent {}
