export interface CatalogItem {
  id: string;
  code: string;
  nameEs: string;
  nameEn?: string;
  sortOrder: number;
  isActive: boolean;
}

export const DEFAULT_LANGUAGES = ['Ingles', 'Frances', 'Aleman', 'Italiano', 'Portugues'];
export const DEFAULT_PROGRAMS = ['Excel', 'SAP', 'AutoCAD', 'Navision', 'Power BI'];
export const DEFAULT_SKILLS = ['Gestion documental', 'Atencion al cliente', 'Analisis', 'Compras'];

export const DEFAULT_LANGUAGE_LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'];
export const DEFAULT_PROGRAM_LEVELS = ['Basico', 'Medio', 'Avanzado'];
export const DEFAULT_SKILL_LEVELS = ['Basico', 'Medio', 'Alto'];
export const DEFAULT_EDUCATION_TYPES = ['Grado', 'Master', 'FP', 'Curso', 'Doctorado'];
export const DEFAULT_EDUCATION_STATUSES = ['Finalizada', 'En curso', 'Pendiente'];
export const DEFAULT_SECTORS = [
  'Servicios',
  'Industria',
  'Tecnologia',
  'Comercio',
  'Sanidad',
  'Educacion',
];
