import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { CatalogService } from '../services/catalog.service';
import { CATALOG_FAMILY_LABELS, CatalogFamily, CatalogItem } from '../models/catalog.models';
import { ConfirmDialogService } from '../../../shared/components/confirm-dialog.service';

@Component({
  selector: 'rrhh-catalog-management-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="toolbar">
        <div class="page-header">
          <h1>Catálogos</h1>
          <p class="muted">
            Gestiona idiomas, programas, habilidades y listas maestras usadas por toda la app.
          </p>
        </div>
        <button
          class="button secondary"
          type="button"
          [disabled]="!editingId"
          (click)="cancelEdit()"
        >
          Cancelar edición
        </button>
      </div>

      <div class="panel stack">
        <div class="toolbar">
          <div class="field" style="min-width: 320px;">
            <label>Familia de catálogo</label>
            <select name="family" [(ngModel)]="activeFamily" (ngModelChange)="cancelEdit()">
              @for (family of familyOptions; track family.key) {
                <option [value]="family.key">{{ family.label }}</option>
              }
            </select>
          </div>
          <p class="muted">Activos: {{ activeCount }} · Total: {{ allCount }}</p>
        </div>

        @if (editingId) {
          <p class="empty-state">
            Modo edición activo. Guarda cambios o cancela antes de cambiar de familia.
          </p>
        }

        <form class="grid two" (ngSubmit)="createItem()">
          <div class="field">
            <label>Nombre (es)</label>
            <input name="newNameEs" [(ngModel)]="newNameEs" required />
          </div>
          <div class="field">
            <label>Código (opcional)</label>
            <input
              name="newCode"
              [(ngModel)]="newCode"
              placeholder="Se autogenera si lo dejas vacio"
            />
          </div>
          <div class="form-actions" style="grid-column: 1 / -1;">
            <button class="button" type="submit" [disabled]="!!editingId">Añadir</button>
          </div>
        </form>

        @if (!items.length) {
          <p class="empty-state">No hay elementos en este catálogo.</p>
        } @else {
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Orden</th>
                  <th>Código</th>
                  <th>Nombre</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                @for (item of items; track item.id) {
                  <tr>
                    <td>{{ item.sortOrder }}</td>
                    <td>{{ item.code }}</td>
                    <td>
                      @if (editingId === item.id) {
                        <div class="grid two">
                          <input name="editNameEs" [(ngModel)]="editNameEs" required />
                          <input name="editCode" [(ngModel)]="editCode" required />
                        </div>
                      } @else {
                        {{ item.nameEs }}
                      }
                    </td>
                    <td>
                      <span class="badge">{{ item.isActive ? 'Activo' : 'Inactivo' }}</span>
                    </td>
                    <td>
                      <div class="form-actions">
                        <button class="button ghost" type="button" (click)="move(item, -1)">
                          Subir
                        </button>
                        <button class="button ghost" type="button" (click)="move(item, 1)">
                          Bajar
                        </button>
                        @if (editingId === item.id) {
                          <button class="button" type="button" (click)="saveEdit(item)">
                            Guardar
                          </button>
                        } @else {
                          <button class="button secondary" type="button" (click)="startEdit(item)">
                            Editar
                          </button>
                        }
                        <button class="button secondary" type="button" (click)="toggle(item)">
                          {{ item.isActive ? 'Desactivar' : 'Activar' }}
                        </button>
                        <button class="button danger" type="button" (click)="remove(item)">
                          Eliminar
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </section>
  `,
})
export class CatalogManagementPageComponent {
  readonly familyOptions = (Object.keys(CATALOG_FAMILY_LABELS) as CatalogFamily[]).map((key) => ({
    key,
    label: CATALOG_FAMILY_LABELS[key],
  }));

  activeFamily: CatalogFamily = 'language';
  newNameEs = '';
  newCode = '';
  editingId = '';
  editNameEs = '';
  editCode = '';

  constructor(
    private readonly catalogService: CatalogService,
    private readonly toast: ToastService,
    private readonly confirmDialog: ConfirmDialogService,
  ) {}

  get items(): CatalogItem[] {
    return this.catalogService.list(this.activeFamily, true);
  }

  get activeCount(): number {
    return this.items.filter((item) => item.isActive).length;
  }

  get allCount(): number {
    return this.items.length;
  }

  createItem(): void {
    try {
      this.catalogService.create(this.activeFamily, this.newNameEs, this.newCode);
      this.newNameEs = '';
      this.newCode = '';
      this.toast.show('Elemento creado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo crear el elemento.',
        'error',
      );
    }
  }

  startEdit(item: CatalogItem): void {
    this.editingId = item.id;
    this.editNameEs = item.nameEs;
    this.editCode = item.code;
  }

  saveEdit(item: CatalogItem): void {
    try {
      this.catalogService.update(this.activeFamily, item.id, {
        nameEs: this.editNameEs,
        code: this.editCode,
      });
      this.cancelEdit();
      this.toast.show('Elemento actualizado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar el elemento.',
        'error',
      );
    }
  }

  cancelEdit(): void {
    this.editingId = '';
    this.editNameEs = '';
    this.editCode = '';
  }

  toggle(item: CatalogItem): void {
    try {
      const updated = this.catalogService.toggleActive(this.activeFamily, item.id);
      this.toast.show(updated.isActive ? 'Elemento activado.' : 'Elemento desactivado.', 'success');
    } catch (error) {
      this.toast.show(
        error instanceof Error ? error.message : 'No se pudo actualizar el estado.',
        'error',
      );
    }
  }

  move(item: CatalogItem, direction: -1 | 1): void {
    this.catalogService.move(this.activeFamily, item.id, direction);
  }

  async remove(item: CatalogItem): Promise<void> {
    const confirmDelete = await this.confirmDialog.confirm({
      title: 'Eliminar valor de catálogo',
      message: `Se eliminará "${item.nameEs}" y no podrás recuperarlo automáticamente.`,
      confirmText: 'Eliminar',
      cancelText: 'Cancelar',
      danger: true,
    });

    if (!confirmDelete) {
      return;
    }

    this.catalogService.remove(this.activeFamily, item.id);
    if (this.editingId === item.id) {
      this.cancelEdit();
    }
    this.toast.show('Elemento eliminado.', 'success');
  }
}
