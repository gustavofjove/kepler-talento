export interface CatalogItem {
  id: string;
  code: string;
  nameEs: string;
  nameEn?: string;
  sortOrder: number;
  isActive: boolean;
}

export type CatalogFamily =
  | 'language'
  | 'program'
  | 'skill'
  | 'language_level'
  | 'program_level'
  | 'skill_level'
  | 'education_type'
  | 'education_status'
  | 'sector';

export const DEFAULT_CATALOGS: Record<CatalogFamily, string[]> = {
  language: ['Ingles', 'Frances', 'Aleman', 'Italiano', 'Portugues'],
  program: ['Excel', 'SAP', 'AutoCAD', 'Navision', 'Power BI'],
  skill: ['Gestion documental', 'Atencion al cliente', 'Analisis', 'Compras'],
  language_level: ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'],
  program_level: ['Basico', 'Medio', 'Avanzado'],
  skill_level: ['Basico', 'Medio', 'Alto'],
  education_type: ['Grado', 'Master', 'FP', 'Curso', 'Doctorado'],
  education_status: ['Finalizada', 'En curso', 'Pendiente'],
  sector: ['Servicios', 'Industria', 'Tecnologia', 'Comercio', 'Sanidad', 'Educacion'],
};

export const CATALOG_FAMILY_LABELS: Record<CatalogFamily, string> = {
  language: 'Idiomas',
  program: 'Programas',
  skill: 'Habilidades',
  language_level: 'Niveles de idioma',
  program_level: 'Niveles de programa',
  skill_level: 'Niveles de habilidad',
  education_type: 'Tipos de formacion',
  education_status: 'Estados de formacion',
  sector: 'Sectores',
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
