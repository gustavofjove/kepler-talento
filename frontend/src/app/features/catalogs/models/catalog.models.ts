export interface CatalogItem {
  id: string;
  code: string;
  nameEs: string;
  nameEn?: string;
  sortOrder: number;
  isActive: boolean;
  /** The chip colour (KTL-41). Always `orange` outside the colourable families. */
  color: CatalogColor;
  /** Row version carried back on writes so a stale change is rejected as a conflict. */
  version: number;
}

/** The closed palette tokens; the hues live in `styles.css` under `[data-catalog-color]`. */
export type CatalogColor =
  'orange' | 'yellow' | 'green' | 'teal' | 'blue' | 'indigo' | 'violet' | 'pink' | 'grey';

export type CatalogLoadStatus = 'idle' | 'loading' | 'loaded' | 'error';

export type CatalogFamily =
  | 'language'
  | 'program'
  | 'skill'
  | 'language_level'
  | 'program_level'
  | 'skill_level'
  | 'education_type'
  | 'education_status'
  | 'sector'
  | 'tag';

type LegacyDefaultCatalogFamily = Exclude<CatalogFamily, 'tag'>;

export const DEFAULT_CATALOGS: Record<LegacyDefaultCatalogFamily, string[]> = {
  language: ['Inglés', 'Francés', 'Alemán', 'Italiano', 'Portugués'],
  program: ['Excel', 'SAP', 'AutoCAD', 'Navision', 'Power BI'],
  skill: ['Gestión documental', 'Atención al cliente', 'Análisis', 'Compras'],
  language_level: ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'],
  program_level: ['Básico', 'Medio', 'Avanzado'],
  skill_level: ['Básico', 'Medio', 'Alto'],
  education_type: ['Grado', 'Master', 'FP', 'Curso', 'Doctorado'],
  education_status: ['Finalizada', 'En curso', 'Pendiente'],
  sector: ['Servicios', 'Industria', 'Tecnología', 'Comercio', 'Sanidad', 'Educación'],
};

export const CATALOG_FAMILY_LABELS: Record<CatalogFamily, string> = {
  language: 'Idiomas',
  program: 'Programas',
  skill: 'Habilidades',
  language_level: 'Idiomas (niveles)',
  program_level: 'Programas (niveles)',
  skill_level: 'Habilidades (niveles)',
  education_type: 'Tipos de formación',
  education_status: 'Estados de formación',
  sector: 'Sectores',
  tag: 'Etiquetas',
};

export const DEFAULT_LANGUAGES = DEFAULT_CATALOGS.language;
export const DEFAULT_PROGRAMS = DEFAULT_CATALOGS.program;
export const DEFAULT_SKILLS = DEFAULT_CATALOGS.skill;

export const DEFAULT_LANGUAGE_LEVELS = DEFAULT_CATALOGS.language_level;
export const DEFAULT_PROGRAM_LEVELS = DEFAULT_CATALOGS.program_level;
export const DEFAULT_SKILL_LEVELS = DEFAULT_CATALOGS.skill_level;
export const DEFAULT_EDUCATION_TYPES = DEFAULT_CATALOGS.education_type;
export const DEFAULT_EDUCATION_STATUSES = DEFAULT_CATALOGS.education_status;
export const DEFAULT_SECTORS = DEFAULT_CATALOGS.sector;
