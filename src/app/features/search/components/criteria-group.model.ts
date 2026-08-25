import type { CatalogFamily } from '../../catalogs/models/catalog.models';

export type CriteriaKind = 'skill' | 'language' | 'program';

export interface CriteriaGroupDefinition {
  kind: CriteriaKind;
  label: string;
  levelLabel: string;
  addLabel: string;
  valueFamily: CatalogFamily;
  levelFamily: CatalogFamily;
}

export const CRITERIA_GROUPS: CriteriaGroupDefinition[] = [
  {
    kind: 'skill',
    label: 'Habilidad',
    levelLabel: 'Nivel de habilidad',
    addLabel: 'Añadir habilidad',
    valueFamily: 'skill',
    levelFamily: 'skill_level',
  },
  {
    kind: 'language',
    label: 'Idiomas',
    levelLabel: 'Nivel de idioma',
    addLabel: 'Añadir idioma',
    valueFamily: 'language',
    levelFamily: 'language_level',
  },
  {
    kind: 'program',
    label: 'Programas',
    levelLabel: 'Nivel de programa',
    addLabel: 'Añadir programa',
    valueFamily: 'program',
    levelFamily: 'program_level',
  },
];
