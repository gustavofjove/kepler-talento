import { Injectable, signal } from '@angular/core';
import { Candidate } from '../../candidates/models/candidate.models';
import { CandidateService } from '../../candidates/services/candidate.service';
import { CatalogFamily, CatalogItem, DEFAULT_CATALOGS } from '../models/catalog.models';

const STORAGE_KEY = 'rrhh-catalogs';

type CatalogState = Record<CatalogFamily, CatalogItem[]>;

@Injectable({ providedIn: 'root' })
export class CatalogService {
  readonly catalogs = signal<CatalogState>(this.restore());

  constructor(private readonly candidateService: CandidateService) {}

  list(family: CatalogFamily, includeInactive = false): CatalogItem[] {
    const items = this.catalogs()
      [family].slice()
      .sort((a, b) => a.sortOrder - b.sortOrder || a.nameEs.localeCompare(b.nameEs));
    return includeInactive ? items : items.filter((item) => item.isActive);
  }

  activeNames(family: CatalogFamily): string[] {
    return this.list(family).map((item) => item.nameEs);
  }

  create(family: CatalogFamily, nameEs: string, code?: string, nameEn?: string): CatalogItem {
    const trimmedName = nameEs.trim();
    if (!trimmedName) {
      throw new Error('El nombre es obligatorio.');
    }

    const items = this.catalogs()[family];
    if (items.some((item) => item.nameEs.toLowerCase() === trimmedName.toLowerCase())) {
      throw new Error('Ya existe un valor con ese nombre.');
    }

    const nextSortOrder = items.length ? Math.max(...items.map((item) => item.sortOrder)) + 1 : 1;
    const item: CatalogItem = {
      id: crypto.randomUUID(),
      code: this.resolveCode(items, trimmedName, code),
      nameEs: trimmedName,
      nameEn: nameEn?.trim() || undefined,
      sortOrder: nextSortOrder,
      isActive: true,
    };

    this.setFamily(family, [...items, item]);
    return item;
  }

  update(
    family: CatalogFamily,
    id: string,
    patch: { nameEs: string; code?: string; nameEn?: string },
  ): CatalogItem {
    const trimmedName = patch.nameEs.trim();
    if (!trimmedName) {
      throw new Error('El nombre es obligatorio.');
    }

    const items = this.catalogs()[family];
    const current = items.find((item) => item.id === id);
    if (!current) {
      throw new Error('No se encontró el elemento del catálogo.');
    }

    if (
      items.some(
        (item) => item.id !== id && item.nameEs.toLowerCase() === trimmedName.toLowerCase(),
      )
    ) {
      throw new Error('Ya existe un valor con ese nombre.');
    }

    const code = this.resolveCode(
      items.filter((item) => item.id !== id),
      trimmedName,
      patch.code || current.code,
    );

    const updated = items.map((item) =>
      item.id === id
        ? {
            ...item,
            nameEs: trimmedName,
            code,
            nameEn: patch.nameEn?.trim() || undefined,
          }
        : item,
    );

    this.setFamily(family, updated);
    const refreshed = this.catalogs()[family].find((item) => item.id === id);
    if (!refreshed) {
      throw new Error('No se encontró el elemento del catálogo.');
    }
    return refreshed;
  }

  toggleActive(family: CatalogFamily, id: string): CatalogItem {
    const items = this.catalogs()[family];
    const current = items.find((item) => item.id === id);
    if (!current) {
      throw new Error('No se encontró el elemento del catálogo.');
    }

    if (current.isActive && this.isCatalogValueInUse(family, current.nameEs)) {
      throw new Error('No se puede desactivar: el valor esta en uso por candidatos.');
    }

    const next = items.map((item) =>
      item.id === id
        ? {
            ...item,
            isActive: !item.isActive,
          }
        : item,
    );
    this.setFamily(family, next);

    const refreshed = this.catalogs()[family].find((item) => item.id === id);
    if (!refreshed) {
      throw new Error('No se encontró el elemento del catálogo.');
    }
    return refreshed;
  }

  remove(family: CatalogFamily, id: string): void {
    const current = this.catalogs()[family].find((item) => item.id === id);
    if (!current) {
      return;
    }

    if (this.isCatalogValueInUse(family, current.nameEs)) {
      throw new Error('No se puede eliminar: el valor esta en uso por candidatos.');
    }

    const filtered = this.catalogs()[family].filter((item) => item.id !== id);
    this.setFamily(family, this.normalizeSortOrder(filtered));
  }

  private isCatalogValueInUse(family: CatalogFamily, value: string): boolean {
    const normalized = value.trim().toLowerCase();
    const candidates = this.candidateService.list(true);

    return candidates.some((candidate) => this.matchesFamilyValue(candidate, family, normalized));
  }

  private matchesFamilyValue(
    candidate: Candidate,
    family: CatalogFamily,
    normalizedValue: string,
  ): boolean {
    const equals = (value: string | undefined): boolean =>
      (value || '').trim().toLowerCase() === normalizedValue;

    switch (family) {
      case 'language':
        return candidate.languages.some((item) => equals(item.language));
      case 'program':
        return candidate.programs.some((item) => equals(item.program));
      case 'skill':
        return candidate.skills.some((item) => equals(item.skill));
      case 'language_level':
        return candidate.languages.some((item) => equals(item.level));
      case 'program_level':
        return candidate.programs.some((item) => equals(item.level));
      case 'skill_level':
        return candidate.skills.some((item) => equals(item.level));
      case 'education_type':
        return candidate.education.some((item) => equals(item.educationType));
      case 'education_status':
        return candidate.education.some((item) => equals(item.status));
      case 'sector':
        return candidate.experience.some((item) => equals(item.sector));
      default:
        return false;
    }
  }

  move(family: CatalogFamily, id: string, direction: -1 | 1): void {
    const ordered = this.list(family, true);
    const index = ordered.findIndex((item) => item.id === id);
    if (index < 0) {
      return;
    }

    const target = index + direction;
    if (target < 0 || target >= ordered.length) {
      return;
    }

    const [moved] = ordered.splice(index, 1);
    ordered.splice(target, 0, moved);
    this.setFamily(family, this.normalizeSortOrder(ordered));
  }

  private setFamily(family: CatalogFamily, items: CatalogItem[]): void {
    const nextState: CatalogState = {
      ...this.catalogs(),
      [family]: this.normalizeSortOrder(items),
    };
    this.persist(nextState);
  }

  private normalizeSortOrder(items: CatalogItem[]): CatalogItem[] {
    return items.map((item, index) => ({ ...item, sortOrder: index + 1 }));
  }

  private resolveCode(items: CatalogItem[], nameEs: string, explicitCode?: string): string {
    const preferred = (explicitCode?.trim() || this.slugify(nameEs)).toUpperCase();
    if (!preferred) {
      throw new Error('El código es obligatorio.');
    }

    const exists = (value: string): boolean =>
      items.some((item) => item.code.toUpperCase() === value.toUpperCase());

    if (!exists(preferred)) {
      return preferred;
    }

    let suffix = 2;
    let candidate = `${preferred}_${suffix}`;
    while (exists(candidate)) {
      suffix += 1;
      candidate = `${preferred}_${suffix}`;
    }
    return candidate;
  }

  private persist(state: CatalogState): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    this.catalogs.set(state);
  }

  private restore(): CatalogState {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      try {
        const parsed = JSON.parse(raw) as CatalogState;
        return this.ensureFamilies(parsed);
      } catch {
        localStorage.removeItem(STORAGE_KEY);
      }
    }

    return this.seedDefaults();
  }

  private ensureFamilies(parsed: Partial<CatalogState>): CatalogState {
    const seeded = this.seedDefaults();
    const families = Object.keys(DEFAULT_CATALOGS) as CatalogFamily[];

    return families.reduce((acc, family) => {
      const source = parsed[family];
      acc[family] =
        Array.isArray(source) && source.length
          ? this.normalizeSortOrder(
              source
                .map((item) => ({
                  ...item,
                  id: item.id || crypto.randomUUID(),
                  code: item.code || this.slugify(item.nameEs),
                  nameEs: item.nameEs || '',
                  sortOrder: Number.isFinite(item.sortOrder) ? item.sortOrder : 9999,
                  isActive: item.isActive !== false,
                }))
                .sort((a, b) => a.sortOrder - b.sortOrder || a.nameEs.localeCompare(b.nameEs)),
            )
          : seeded[family];
      return acc;
    }, {} as CatalogState);
  }

  private seedDefaults(): CatalogState {
    const families = Object.keys(DEFAULT_CATALOGS) as CatalogFamily[];
    return families.reduce((acc, family) => {
      acc[family] = DEFAULT_CATALOGS[family].map((nameEs, index) => ({
        id: crypto.randomUUID(),
        code: this.slugify(nameEs),
        nameEs,
        sortOrder: index + 1,
        isActive: true,
      }));
      return acc;
    }, {} as CatalogState);
  }

  private slugify(value: string): string {
    const normalized = value
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-zA-Z0-9]+/g, '_')
      .replace(/^_+|_+$/g, '')
      .toUpperCase();

    return normalized || 'ITEM';
  }
}
