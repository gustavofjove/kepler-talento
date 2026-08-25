# Especificación técnica — Aplicación web de gestión de CVs

## 1. Objetivo

Desarrollar una aplicación web interna para el área de RRHH destinada a registrar, consultar, filtrar y explotar currículums recibidos por la empresa.

La aplicación sustituirá el uso de Access como solución operativa final. La base de datos Access existente se utilizará únicamente como referencia funcional y, si procede, como fuente inicial para una migración controlada de datos.

El sistema debe permitir localizar candidatos de forma ágil mediante filtros combinados por programas, idiomas, formación, experiencia, estado del proceso, disponibilidad y otros criterios definidos por RRHH.

## 2. Stack tecnológico obligatorio

La aplicación debe desarrollarse respetando el stack tecnológico corporativo existente.

### 2.1 Frontend

- Angular 21.
- TypeScript 5.9.
- Aplicación SPA.
- Arranque mediante `bootstrapApplication`.
- Rutas mediante `provideRouter`.
- Componentes standalone.
- RxJS.
- Angular CDK.
- Tailwind CSS 3.
- Internacionalización con `@ngx-translate`.
- Cliente Supabase mediante `@supabase/supabase-js`.

### 2.2 Backend / BaaS

- Supabase.
- PostgreSQL 17, según `supabase/config.toml`.
- Supabase Auth.
- MFA/TOTP configurado.
- Supabase Storage.
- Realtime habilitado.
- Row Level Security activado.
- Políticas SQL mediante migraciones en `supabase/migrations`.
- Supabase Edge Functions en TypeScript/Deno.

Edge Functions de referencia existentes en el ecosistema del proyecto:

- `admin-create-user`.
- `process-outbox`.
- `sla-check`.

La nueva aplicación deberá seguir el mismo estilo de organización, seguridad y despliegue.

### 2.3 Build y runtime

- Node.js 22 Alpine para compilación.
- Nginx unprivileged 1.27 Alpine para servir la aplicación en producción.
- Docker.
- Docker Compose.
- Puerto frontend en compose: `63151:8080`.

### 2.4 Testing y calidad

- Jest 30.
- `jest-preset-angular`.
- Playwright para pruebas E2E.
- ESLint.
- Prettier.
- Husky.
- lint-staged.
- Scripts específicos para validación de seguridad y RLS.

### 2.5 Archivos de referencia del proyecto base

El desarrollo deberá tomar como referencia la estructura y configuración existente en:

- `C:/DESARROLLOS/Kepler_tickets/package.json`
- `C:/DESARROLLOS/Kepler_tickets/angular.json`
- `C:/DESARROLLOS/Kepler_tickets/supabase/config.toml`
- `C:/DESARROLLOS/Kepler_tickets/Dockerfile.frontend`
- `C:/DESARROLLOS/Kepler_tickets/docker-compose.frontend.yml`

## 3. Alcance funcional del MVP

La primera versión de la aplicación deberá incluir como mínimo:

1. Autenticación de usuarios mediante Supabase Auth.
2. Control de acceso por roles.
3. Alta, edición, consulta y baja lógica de candidatos.
4. Gestión de catálogos maestros.
5. Asociación de múltiples idiomas por candidato.
6. Asociación de múltiples programas por candidato.
7. Asociación de formación académica y complementaria.
8. Asociación de experiencia laboral.
9. Asociación de habilidades o competencias.
10. Carga o vinculación del PDF del CV.
11. Almacenamiento seguro del CV en Supabase Storage.
12. Buscador avanzado con filtros combinados.
13. Resultados de búsqueda con:
    - Nombre y apellidos.
    - Teléfono.
    - Email, si se considera conveniente.
    - Estado del candidato.
    - Enlace seguro al CV.
14. Exportación de resultados a Excel o CSV.
15. Registro de fechas relevantes:
    - Fecha de recepción del CV.
    - Fecha de última actualización.
    - Fecha de consentimiento, si aplica.
    - Fecha prevista de revisión/eliminación.
16. Auditoría básica de creación y modificación.

## 4. Requisitos funcionales detallados

### 4.1 Gestión de candidatos

La aplicación debe permitir registrar la información principal de cada candidato.

Campos mínimos recomendados:

- Nombre.
- Apellidos.
- Teléfono.
- Email.
- Localidad.
- Provincia.
- País.
- Disponibilidad.
- Estado del candidato.
- Observaciones internas.
- Fecha de recepción del CV.
- Fuente de recepción.
- Consentimiento de tratamiento de datos.
- Fecha de consentimiento.
- Fecha de caducidad o revisión del CV.
- Fecha de creación.
- Fecha de última modificación.
- Usuario creador.
- Usuario modificador.
- Indicador de baja lógica.

### 4.2 Idiomas

Un candidato puede tener varios idiomas.

Para cada idioma se debe registrar:

- Idioma.
- Nivel.
- Certificación, opcional.
- Observaciones, opcional.

Debe ser posible filtrar candidatos por uno o varios idiomas.

La búsqueda debe soportar dos modos:

- `ANY`: candidatos que tengan al menos uno de los idiomas seleccionados.
- `ALL`: candidatos que tengan todos los idiomas seleccionados.

### 4.3 Programas

Un candidato puede dominar varios programas o herramientas.

Para cada programa se debe registrar:

- Programa.
- Nivel.
- Años de experiencia, opcional.
- Observaciones, opcional.

Debe ser posible filtrar candidatos por uno o varios programas.

La búsqueda debe soportar:

- `ANY`: candidatos que tengan al menos uno de los programas seleccionados.
- `ALL`: candidatos que tengan todos los programas seleccionados.

### 4.4 Formación

Un candidato puede tener múltiples registros de formación.

Para cada formación se debe registrar:

- Tipo de formación.
- Titulación.
- Centro.
- Especialidad.
- Año de finalización.
- Estado: finalizada, en curso, pendiente u otros definidos por RRHH.
- Observaciones.

### 4.5 Experiencia laboral

Un candidato puede tener múltiples experiencias laborales.

Para cada experiencia se debe registrar:

- Empresa.
- Puesto.
- Sector.
- Funciones.
- Fecha de inicio.
- Fecha de fin.
- Actualmente en el puesto.
- Años de experiencia calculados o informados.
- Observaciones.

Debe ser posible filtrar por:

- Sector.
- Puesto.
- Texto contenido en funciones.
- Años mínimos de experiencia.
- Experiencia actual o histórica.

### 4.6 Habilidades

Un candidato puede tener múltiples habilidades.

Para cada habilidad se debe registrar:

- Habilidad.
- Nivel.
- Observaciones.

### 4.7 Documentos / CV

Cada candidato debe poder tener uno o varios documentos asociados, aunque en el MVP puede considerarse un CV principal.

Datos mínimos del documento:

- Candidato.
- Tipo de documento: CV, carta de presentación, certificado, otros.
- Nombre original del archivo.
- Ruta o path en Supabase Storage.
- Bucket.
- MIME type.
- Tamaño.
- Fecha de subida.
- Usuario que subió el documento.
- Indicador de documento principal.
- Hash del archivo, opcional.

El PDF no debe exponerse mediante una ruta física local. La apertura del CV debe realizarse mediante URL firmada generada por Supabase Storage o mediante Edge Function que valide permisos.

## 5. Requisitos de búsqueda avanzada

La búsqueda es el punto funcional más importante.

### 5.1 Principios de filtrado

La lógica debe cumplir:

- Los campos vacíos no limitan resultados.
- Los criterios de distinto tipo se combinan mediante `AND`.
- Dentro de un mismo criterio multivalor debe poder seleccionarse modo `ANY` o `ALL`.
- La búsqueda debe devolver todos los candidatos que cumplan los criterios.
- No deben aparecer candidatos duplicados.
- La consulta debe resolver correctamente relaciones uno-a-muchos y muchos-a-muchos.
- El filtrado no debe depender de consultas dinámicas inestables de Access.

### 5.2 Ejemplo de lógica

Si RRHH filtra por:

- Idiomas: inglés y francés, modo `ALL`.
- Programas: AutoCAD o SolidWorks, modo `ANY`.
- Formación: Ingeniería.
- Estado: disponible.

El sistema debe devolver candidatos que:

- Tengan inglés.
- Tengan francés.
- Tengan AutoCAD o SolidWorks.
- Tengan formación relacionada con Ingeniería.
- Estén en estado disponible.

### 5.3 Filtros mínimos del MVP

- Texto libre: nombre, apellidos, email, teléfono, observaciones.
- Estado del candidato.
- Idiomas.
- Nivel mínimo de idioma.
- Programas.
- Nivel mínimo de programa.
- Formación.
- Tipo de formación.
- Sector de experiencia.
- Puesto.
- Años mínimos de experiencia.
- Disponibilidad.
- Provincia/localidad.
- Fecha de recepción desde/hasta.
- Candidatos con CV adjunto.
- Candidatos pendientes de revisión RGPD.

## 6. Arquitectura frontend Angular

### 6.1 Estructura recomendada

La aplicación debe organizarse por dominios funcionales.

Estructura orientativa:

```text
src/
  app/
    core/
      auth/
      guards/
      interceptors/
      layout/
      supabase/
      services/
    shared/
      components/
      pipes/
      directives/
      models/
      utils/
    features/
      candidates/
        pages/
        components/
        services/
        models/
      search/
        pages/
        components/
        services/
        models/
      catalogs/
        pages/
        components/
        services/
        models/
      documents/
        services/
        models/
      admin/
        users/
        roles/
    app.config.ts
    app.routes.ts
  assets/
    i18n/
      es.json
      en.json
```

### 6.2 Rutas mínimas

Rutas recomendadas:

```text
/login
/mfa
/app
/app/candidates
/app/candidates/new
/app/candidates/:id
/app/candidates/:id/edit
/app/candidates/:id/documents
/app/search
/app/catalogs
/app/admin/users
/app/admin/roles
```

### 6.3 Componentes principales

- `AppLayoutComponent`.
- `LoginPageComponent`.
- `MfaPageComponent`.
- `CandidateListPageComponent`.
- `CandidateDetailPageComponent`.
- `CandidateEditPageComponent`.
- `CandidateFormComponent`.
- `CandidateLanguagesComponent`.
- `CandidateProgramsComponent`.
- `CandidateEducationComponent`.
- `CandidateExperienceComponent`.
- `CandidateSkillsComponent`.
- `CandidateDocumentsComponent`.
- `AdvancedSearchPageComponent`.
- `SearchFiltersComponent`.
- `SearchResultsComponent`.
- `CatalogManagementPageComponent`.
- `AdminUsersPageComponent`.

Todos los componentes deberán ser standalone.

### 6.4 Servicios frontend

Servicios mínimos recomendados:

- `SupabaseClientService`.
- `AuthService`.
- `MfaService`.
- `ProfileService`.
- `CandidateService`.
- `CandidateSearchService`.
- `CatalogService`.
- `DocumentService`.
- `ExportService`.
- `ToastService`.
- `RealtimeService`.

### 6.5 Gestión de estado

Para el MVP se recomienda usar:

- Servicios Angular con `BehaviorSubject` / `ReplaySubject`.
- RxJS para flujos asíncronos.
- Signals de Angular si el proyecto base los utiliza.
- Evitar introducir librerías de estado adicionales salvo necesidad justificada.

## 7. Modelo de datos PostgreSQL / Supabase

### 7.1 Consideraciones generales

- Todas las tablas deben crearse mediante migraciones SQL en `supabase/migrations`.
- Todas las tablas funcionales deben tener `id uuid primary key default gen_random_uuid()`.
- Usar `created_at`, `updated_at`, `created_by`, `updated_by`.
- Activar RLS en todas las tablas con datos de negocio.
- Evitar borrado físico salvo en catálogos no usados.
- Usar baja lógica mediante `deleted_at` o `is_active`.

### 7.2 Esquema recomendado

#### Tabla `profiles`

Perfil extendido del usuario autenticado.

Campos:

- `id uuid primary key references auth.users(id)`
- `display_name text`
- `email text`
- `role text`
- `is_active boolean`
- `created_at timestamptz`
- `updated_at timestamptz`

Roles iniciales:

- `rrhh_admin`
- `rrhh_user`
- `manager_reader`
- `readonly`
- `system_admin`

#### Tabla `candidates`

Campos recomendados:

- `id uuid primary key`
- `first_name text not null`
- `last_name text not null`
- `phone text`
- `email text`
- `location text`
- `province text`
- `country text default 'España'`
- `availability_id uuid`
- `status_id uuid`
- `source_id uuid`
- `notes text`
- `received_at date`
- `consent_at date`
- `review_due_at date`
- `is_active boolean default true`
- `deleted_at timestamptz`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

#### Catálogos

Tablas de catálogo recomendadas:

- `catalog_candidate_statuses`
- `catalog_availability`
- `catalog_sources`
- `catalog_languages`
- `catalog_language_levels`
- `catalog_programs`
- `catalog_program_levels`
- `catalog_education_types`
- `catalog_sectors`
- `catalog_skills`
- `catalog_skill_levels`
- `catalog_document_types`

Campos comunes:

- `id uuid primary key`
- `code text unique`
- `name_es text not null`
- `name_en text`
- `sort_order integer default 100`
- `is_active boolean default true`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`

#### Tabla `candidate_languages`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `language_id uuid not null references catalog_languages(id)`
- `level_id uuid references catalog_language_levels(id)`
- `certification text`
- `notes text`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

Restricción recomendada:

```sql
unique(candidate_id, language_id)
```

#### Tabla `candidate_programs`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `program_id uuid not null references catalog_programs(id)`
- `level_id uuid references catalog_program_levels(id)`
- `years_experience numeric(4,1)`
- `notes text`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

Restricción recomendada:

```sql
unique(candidate_id, program_id)
```

#### Tabla `candidate_education`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `education_type_id uuid references catalog_education_types(id)`
- `degree text not null`
- `specialty text`
- `institution text`
- `end_year integer`
- `status text`
- `notes text`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

#### Tabla `candidate_experience`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `company text`
- `position text`
- `sector_id uuid references catalog_sectors(id)`
- `functions text`
- `start_date date`
- `end_date date`
- `is_current boolean default false`
- `years_experience numeric(4,1)`
- `notes text`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

#### Tabla `candidate_skills`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `skill_id uuid not null references catalog_skills(id)`
- `level_id uuid references catalog_skill_levels(id)`
- `notes text`
- `created_at timestamptz default now()`
- `updated_at timestamptz default now()`
- `created_by uuid references auth.users(id)`
- `updated_by uuid references auth.users(id)`

#### Tabla `candidate_documents`

- `id uuid primary key`
- `candidate_id uuid not null references candidates(id) on delete cascade`
- `document_type_id uuid references catalog_document_types(id)`
- `storage_bucket text not null`
- `storage_path text not null`
- `original_filename text not null`
- `mime_type text`
- `size_bytes bigint`
- `is_primary boolean default false`
- `uploaded_at timestamptz default now()`
- `uploaded_by uuid references auth.users(id)`
- `file_hash text`
- `created_at timestamptz default now()`

Restricciones recomendadas:

```sql
unique(storage_bucket, storage_path)
```

Para documento principal único por candidato se recomienda índice parcial:

```sql
create unique index candidate_documents_one_primary_per_candidate
on candidate_documents(candidate_id)
where is_primary = true;
```

#### Tabla `candidate_audit_log`

- `id uuid primary key`
- `candidate_id uuid references candidates(id)`
- `action text not null`
- `entity_name text`
- `entity_id uuid`
- `old_data jsonb`
- `new_data jsonb`
- `created_at timestamptz default now()`
- `created_by uuid references auth.users(id)`

## 8. Funciones SQL recomendadas

### 8.1 Función de búsqueda avanzada

Para evitar lógica compleja en el frontend, se recomienda crear una función RPC en PostgreSQL:

```sql
search_candidates(filters jsonb)
```

La función debe recibir un objeto JSON con los filtros seleccionados y devolver los candidatos coincidentes.

Ventajas:

- Centraliza la lógica de filtrado.
- Permite aplicar `ANY` y `ALL`.
- Evita duplicados.
- Facilita pruebas unitarias SQL.
- Respeta RLS si se define como `security invoker`.

Ejemplo de payload:

```json
{
  "text": "naval",
  "status_ids": ["uuid"],
  "language_ids": ["uuid1", "uuid2"],
  "language_mode": "ALL",
  "program_ids": ["uuid3", "uuid4"],
  "program_mode": "ANY",
  "education_type_ids": [],
  "sector_ids": [],
  "min_years_experience": 3,
  "received_from": "2025-01-01",
  "received_to": "2026-12-31",
  "has_cv": true
}
```

Respuesta recomendada:

```sql
returns table (
  candidate_id uuid,
  first_name text,
  last_name text,
  phone text,
  email text,
  status_name text,
  primary_cv_document_id uuid,
  updated_at timestamptz
)
```

### 8.2 Función para URL segura de CV

Crear una Edge Function o función controlada para generar URLs firmadas de Supabase Storage.

No se debe devolver directamente el `storage_path` al usuario final como si fuera una ruta navegable pública.

## 9. Supabase Storage

### 9.1 Bucket

Crear un bucket privado:

```text
candidate-cvs
```

Configuración:

- Público: no.
- Acceso mediante URLs firmadas.
- Límite de tamaño configurable.
- Tipos permitidos inicialmente:
  - `application/pdf`.

### 9.2 Estructura de rutas

Estructura recomendada:

```text
candidate-cvs/{candidate_id}/{document_id}/{sanitized_filename}.pdf
```

Ejemplo:

```text
candidate-cvs/8d3.../3a1.../cv_juan_perez.pdf
```

### 9.3 Reglas

- Solo usuarios autenticados con rol permitido pueden subir CVs.
- Solo usuarios autorizados pueden descargar CVs.
- No se deben usar buckets públicos.
- La eliminación física del archivo debe estar controlada.

## 10. Seguridad, Auth, MFA y RLS

### 10.1 Autenticación

- Usar Supabase Auth.
- Exigir MFA/TOTP según configuración corporativa.
- La aplicación debe comprobar sesión activa al cargar.
- Las rutas protegidas deben usar guards Angular.

### 10.2 Roles

Roles recomendados:

| Rol              | Permisos                                                         |
| ---------------- | ---------------------------------------------------------------- |
| `rrhh_admin`     | Gestión completa de candidatos, catálogos y usuarios funcionales |
| `rrhh_user`      | Alta, edición, búsqueda y descarga de CVs                        |
| `manager_reader` | Consulta limitada de candidatos y descarga si se autoriza        |
| `readonly`       | Solo consulta básica, sin descarga salvo política específica     |
| `system_admin`   | Administración técnica                                           |

### 10.3 RLS

Todas las tablas con datos personales deben tener RLS activado.

Tablas con RLS obligatorio:

- `profiles`
- `candidates`
- `candidate_languages`
- `candidate_programs`
- `candidate_education`
- `candidate_experience`
- `candidate_skills`
- `candidate_documents`
- `candidate_audit_log`
- Catálogos, al menos para escritura.

Ejemplo orientativo:

```sql
alter table candidates enable row level security;

create policy "rrhh can read candidates"
on candidates
for select
to authenticated
using (
  exists (
    select 1
    from profiles p
    where p.id = auth.uid()
      and p.is_active = true
      and p.role in ('rrhh_admin', 'rrhh_user', 'manager_reader', 'readonly', 'system_admin')
  )
);

create policy "rrhh can insert candidates"
on candidates
for insert
to authenticated
with check (
  exists (
    select 1
    from profiles p
    where p.id = auth.uid()
      and p.is_active = true
      and p.role in ('rrhh_admin', 'rrhh_user', 'system_admin')
  )
);
```

### 10.4 Protección de datos

Al tratarse de CVs, la aplicación debe contemplar:

- Acceso restringido.
- No exposición pública de documentos.
- Registro de fechas de recepción.
- Registro de consentimiento cuando aplique.
- Fecha de revisión o eliminación.
- Baja lógica de candidatos.
- Auditoría básica.
- Exportaciones controladas.
- Evitar almacenamiento de CVs en rutas locales no gobernadas.

## 11. Edge Functions

### 11.1 Funciones recomendadas

Crear Edge Functions siguiendo el patrón del proyecto existente.

Funciones mínimas recomendadas:

#### `candidate-create-signed-cv-url`

Objetivo:

- Recibir `document_id`.
- Validar usuario autenticado.
- Validar permisos.
- Obtener `storage_bucket` y `storage_path`.
- Generar signed URL temporal.
- Devolver URL al frontend.

#### `candidate-import-access-csv`

Objetivo:

- Permitir importar datos depurados desde CSV exportado de Access.
- Validar estructura.
- Insertar candidatos y relaciones.
- Registrar errores de importación.

#### `candidate-retention-check`

Objetivo:

- Localizar CVs con fecha de revisión vencida.
- Generar listado para RRHH.
- Opcionalmente crear notificaciones o tareas internas.

#### `candidate-export-results`

Objetivo opcional:

- Generar exportación controlada de resultados.
- Registrar quién exporta y cuándo.
- Limitar campos exportados según rol.

### 11.2 Consideraciones Deno/TypeScript

- Usar el cliente Supabase server-side.
- No exponer service role key al frontend.
- Las funciones privilegiadas deben ejecutarse exclusivamente en Edge Functions.
- Validar payloads de entrada.
- Devolver errores controlados.
- Registrar eventos relevantes.

## 12. Realtime

Realtime puede utilizarse para:

- Actualizar listados cuando otro usuario modifica candidatos.
- Notificar cambios en estados.
- Refrescar catálogos si se modifican.
- Alertar de importaciones finalizadas.

Para el MVP no es imprescindible que toda la aplicación sea realtime, pero debe respetar que Realtime está habilitado en el stack.

## 13. Internacionalización

Usar `@ngx-translate`.

Idiomas iniciales:

- Español: `assets/i18n/es.json`.
- Inglés: `assets/i18n/en.json`.

Todos los textos visibles deben ir a ficheros de traducción, especialmente:

- Menús.
- Botones.
- Estados.
- Mensajes de validación.
- Títulos.
- Cabeceras de tablas.
- Mensajes de error.

## 14. UI / UX

### 14.1 Estilo

- Tailwind CSS 3.
- Angular CDK para elementos de interfaz cuando sea útil.
- Interfaz sobria, interna y orientada a productividad.
- Diseño responsive para escritorio y portátil.
- No es prioritario el uso móvil en el MVP.

### 14.2 Pantallas mínimas

#### Login / MFA

- Inicio de sesión.
- Flujo MFA/TOTP.
- Mensajes de error claros.

#### Dashboard

- Resumen básico:
  - Candidatos activos.
  - Candidatos recibidos este mes.
  - Candidatos pendientes de revisión.
  - Candidatos sin CV adjunto.

#### Candidatos

- Listado paginado.
- Búsqueda rápida.
- Acceso a detalle.
- Alta de candidato.
- Edición.
- Baja lógica.

#### Detalle de candidato

Secciones:

- Datos principales.
- Idiomas.
- Programas.
- Formación.
- Experiencia.
- Habilidades.
- Documentos.
- Auditoría básica.

#### Buscador avanzado

- Panel de filtros.
- Multiselección para idiomas, programas, sectores, formación.
- Selector `ANY` / `ALL`.
- Botón limpiar filtros.
- Botón buscar.
- Tabla de resultados.
- Exportar.
- Abrir CV.

#### Catálogos

- Gestión de listas cerradas.
- Alta, edición, desactivación.
- Ordenación.

#### Administración

- Usuarios.
- Roles.
- Activación/desactivación de perfiles.

## 15. Exportación

La exportación debe permitir descargar resultados a CSV o XLSX.

Campos mínimos:

- Nombre.
- Apellidos.
- Teléfono.
- Email.
- Estado.
- Localidad.
- Provincia.
- Fecha de recepción.
- Fecha de última actualización.
- Indicador de CV disponible.

No incluir rutas internas de Storage. Si se incluye referencia al CV, debe ser un identificador funcional o enlace temporal generado de forma segura.

## 16. Migración desde Access

### 16.1 Principio general

La base Access existente no debe ser utilizada como base de datos de producción.

Debe usarse para:

- Identificar campos.
- Revisar formularios existentes.
- Exportar datos iniciales.
- Validar necesidades de RRHH.

### 16.2 Proceso recomendado

1. Exportar tablas Access a CSV.
2. Revisar y limpiar datos.
3. Homogeneizar valores de catálogos.
4. Eliminar duplicados.
5. Resolver datos huérfanos.
6. Cargar catálogos.
7. Cargar candidatos.
8. Cargar relaciones.
9. Validar conteos.
10. Validar candidatos de muestra con RRHH.

### 16.3 Problemas detectados en la BBDD Access

La BBDD Access revisada presentaba riesgos habituales de prototipo:

- Falta de relaciones reales entre tablas funcionales.
- Posibles datos huérfanos.
- Duplicidad de campos entre candidatos y tablas relacionadas.
- Mezcla de IDs y textos en campos de relación.
- Dificultad para filtros dinámicos complejos.

La nueva aplicación debe corregir estos puntos mediante modelo relacional normalizado.

## 17. Testing

### 17.1 Unit tests

Usar Jest 30 con `jest-preset-angular`.

Cubrir como mínimo:

- Servicios de candidatos.
- Servicios de búsqueda.
- Transformación de filtros.
- Validaciones de formularios.
- Guards de autenticación.
- Utilidades de exportación.
- Componentes principales con lógica no trivial.

### 17.2 E2E

Usar Playwright.

Flujos mínimos:

1. Login.
2. MFA, si el entorno de test lo permite.
3. Alta de candidato.
4. Edición de candidato.
5. Carga de CV.
6. Búsqueda por idioma.
7. Búsqueda por programa.
8. Búsqueda combinada idioma + programa + formación.
9. Apertura segura de CV.
10. Exportación de resultados.
11. Baja lógica de candidato.

### 17.3 Pruebas SQL / RLS

Crear scripts de validación para:

- Usuario RRHH puede leer candidatos.
- Usuario RRHH puede insertar candidatos.
- Usuario readonly no puede modificar.
- Usuario sin perfil activo no puede acceder.
- Usuario no autenticado no puede leer.
- Storage no permite acceso público.
- Signed URLs solo se generan para usuarios autorizados.

## 18. Calidad, linting y formato

El proyecto debe respetar:

- ESLint.
- Prettier.
- Husky.
- lint-staged.

Se recomienda añadir scripts:

```json
{
  "lint": "ng lint",
  "format": "prettier --write .",
  "format:check": "prettier --check .",
  "test": "jest",
  "test:watch": "jest --watch",
  "e2e": "playwright test",
  "security:rls": "node scripts/check-rls.js",
  "security:storage": "node scripts/check-storage-policies.js"
}
```

Adaptar los nombres a los scripts ya existentes en `Kepler_tickets`.

## 19. Docker y despliegue

### 19.1 Build frontend

El frontend debe compilar con Node.js 22 Alpine.

### 19.2 Runtime

La app se servirá como frontend estático mediante Nginx unprivileged 1.27 Alpine.

### 19.3 Docker Compose

Respetar el puerto:

```yaml
ports:
  - '63151:8080'
```

### 19.4 Variables de entorno

Variables mínimas:

- `SUPABASE_URL`
- `SUPABASE_ANON_KEY`
- `APP_ENV`
- `APP_VERSION`

No incluir claves service role en el frontend.

## 20. Criterios de aceptación

La aplicación se considerará aceptada para MVP cuando:

1. Un usuario autorizado pueda iniciar sesión con Supabase Auth.
2. El flujo MFA/TOTP funcione según configuración del entorno.
3. Un usuario RRHH pueda crear un candidato.
4. Un usuario RRHH pueda editar un candidato.
5. Un candidato pueda tener múltiples idiomas.
6. Un candidato pueda tener múltiples programas.
7. Un candidato pueda tener múltiples experiencias.
8. Un candidato pueda tener múltiples formaciones.
9. Se pueda subir un PDF de CV a Supabase Storage privado.
10. Se pueda abrir el CV mediante enlace seguro.
11. La búsqueda ignore filtros vacíos.
12. La búsqueda combine filtros diferentes mediante `AND`.
13. La búsqueda soporte `ANY` y `ALL` en idiomas.
14. La búsqueda soporte `ANY` y `ALL` en programas.
15. La búsqueda devuelva todos los candidatos válidos sin duplicados.
16. Los resultados muestren nombre, apellidos, teléfono y CV.
17. Se pueda exportar el resultado.
18. RLS impida accesos no autorizados.
19. Los tests mínimos estén implementados.
20. La app se pueda construir y servir en Docker/Nginx.

## 21. Plan de desarrollo por fases para agente de IA en VS Code

### Fase 0 — Análisis del proyecto base

Objetivo:

- Revisar la estructura existente del proyecto `Kepler_tickets`.
- Identificar convenciones reales de carpetas, scripts, estilos, servicios y migraciones.
- No modificar código todavía.

Tareas:

1. Leer `package.json`.
2. Leer `angular.json`.
3. Leer `supabase/config.toml`.
4. Revisar `src/app`.
5. Revisar `supabase/migrations`.
6. Revisar Edge Functions existentes.
7. Revisar Dockerfile y compose.

Salida esperada:

- Resumen de convenciones detectadas.
- Propuesta de integración para el nuevo módulo de gestión de CVs.

### Fase 1 — Migraciones base

Objetivo:

- Crear tablas, catálogos y relaciones.

Tareas:

1. Crear migración SQL inicial.
2. Crear tablas de candidatos.
3. Crear catálogos.
4. Crear tablas relacionales.
5. Crear tabla de documentos.
6. Crear tabla de auditoría.
7. Activar RLS.
8. Añadir políticas mínimas.

### Fase 2 — Storage

Objetivo:

- Crear configuración de bucket privado para CVs.

Tareas:

1. Crear bucket `candidate-cvs`.
2. Definir políticas de acceso.
3. Validar que no es público.
4. Preparar servicio frontend de subida.

### Fase 3 — Auth y perfiles

Objetivo:

- Integrar perfiles y roles funcionales.

Tareas:

1. Crear tabla `profiles`.
2. Crear políticas.
3. Crear guard Angular.
4. Crear servicio de sesión.
5. Crear control de roles.

### Fase 4 — CRUD candidatos

Objetivo:

- Implementar alta, edición, consulta y baja lógica.

Tareas:

1. Crear modelos TypeScript.
2. Crear `CandidateService`.
3. Crear páginas de listado, detalle y edición.
4. Crear formulario principal.
5. Añadir validaciones.
6. Añadir tests unitarios.

### Fase 5 — Relaciones del candidato

Objetivo:

- Implementar idiomas, programas, formación, experiencia y habilidades.

Tareas:

1. Crear componentes standalone por sección.
2. Crear servicios de catálogos.
3. Permitir altas y eliminaciones de relaciones.
4. Evitar duplicados.
5. Añadir tests.

### Fase 6 — Documentos CV

Objetivo:

- Subir, registrar y abrir CVs.

Tareas:

1. Crear `DocumentService`.
2. Subir PDF a Storage.
3. Registrar documento en `candidate_documents`.
4. Marcar CV principal.
5. Crear Edge Function para signed URL.
6. Abrir CV desde detalle y resultados.

### Fase 7 — Buscador avanzado

Objetivo:

- Implementar búsqueda combinada.

Tareas:

1. Crear RPC `search_candidates(filters jsonb)`.
2. Crear modelos de filtros.
3. Crear `CandidateSearchService`.
4. Crear pantalla de búsqueda.
5. Añadir `ANY` / `ALL`.
6. Evitar duplicados.
7. Añadir tests SQL y frontend.

### Fase 8 — Exportación

Objetivo:

- Exportar resultados.

Tareas:

1. Crear exportación CSV o XLSX.
2. Respetar permisos.
3. Registrar evento de exportación si se define.
4. Añadir tests.

### Fase 9 — Importación desde Access

Objetivo:

- Cargar datos iniciales depurados.

Tareas:

1. Definir formato CSV de importación.
2. Crear Edge Function o script controlado.
3. Validar catálogos.
4. Cargar candidatos.
5. Cargar relaciones.
6. Generar informe de errores.

### Fase 10 — Hardening

Objetivo:

- Revisar seguridad, RLS, tests, rendimiento y despliegue.

Tareas:

1. Ejecutar lint.
2. Ejecutar tests unitarios.
3. Ejecutar E2E.
4. Ejecutar checks RLS.
5. Revisar Storage.
6. Revisar Docker build.
7. Documentar despliegue.

## 22. Prompt inicial para agente de IA en VS Code

Usar el siguiente prompt como arranque del desarrollo:

```text
Necesito desarrollar un módulo/aplicación interna de gestión de CVs para RRHH dentro del stack tecnológico existente del proyecto.

Stack obligatorio:
- Angular 21.
- TypeScript 5.9.
- SPA con bootstrapApplication, provideRouter y componentes standalone.
- RxJS.
- Angular CDK.
- Tailwind CSS 3.
- @ngx-translate.
- @supabase/supabase-js.
- Supabase como backend/BaaS.
- PostgreSQL 17 según supabase/config.toml.
- Supabase Auth con MFA/TOTP.
- Supabase Storage.
- Realtime habilitado.
- RLS obligatorio en tablas de negocio.
- Supabase Edge Functions en TypeScript/Deno.
- Docker con Node.js 22 Alpine para build.
- Nginx unprivileged 1.27 Alpine para runtime.
- Puerto frontend en compose: 63151:8080.
- Jest 30 + jest-preset-angular.
- Playwright.
- ESLint, Prettier, Husky y lint-staged.

Archivos de referencia:
- C:/DESARROLLOS/Kepler_tickets/package.json
- C:/DESARROLLOS/Kepler_tickets/angular.json
- C:/DESARROLLOS/Kepler_tickets/supabase/config.toml
- C:/DESARROLLOS/Kepler_tickets/Dockerfile.frontend
- C:/DESARROLLOS/Kepler_tickets/docker-compose.frontend.yml

Objetivo funcional:
Crear una aplicación interna para RRHH que permita registrar candidatos, asociar múltiples idiomas, programas, formaciones, experiencias, habilidades y documentos PDF de CV, y realizar búsquedas avanzadas combinando criterios.

Requisitos críticos:
- Access no será la base de datos de producción.
- La BBDD Access existente solo sirve como referencia funcional y posible origen de importación.
- Un candidato puede tener varios idiomas.
- Un candidato puede tener varios programas.
- Un candidato puede tener varias formaciones.
- Un candidato puede tener varias experiencias.
- Un candidato puede tener varias habilidades.
- Los CVs deben almacenarse en Supabase Storage privado.
- Los CVs deben abrirse mediante enlace seguro o signed URL.
- No deben exponerse rutas físicas locales.
- Los filtros vacíos no deben limitar resultados.
- Los criterios de distinto tipo se combinan con AND.
- Los criterios multiselección deben soportar modo ANY y ALL.
- La búsqueda debe devolver todos los candidatos que cumplan criterios, sin duplicados.
- Los resultados deben mostrar nombre, apellidos, teléfono y enlace seguro al CV.
- Debe existir exportación de resultados.
- Deben aplicarse RLS y políticas SQL desde migraciones.
- Deben añadirse tests unitarios, E2E y checks de seguridad/RLS.

Antes de modificar archivos:
1. Analiza la estructura actual del proyecto.
2. Resume las convenciones detectadas.
3. Indica qué archivos vas a crear o modificar.
4. Propón la primera fase de implementación.

Desarrolla por fases:
1. Migraciones y modelo de datos.
2. RLS y roles.
3. Storage privado para CVs.
4. Auth/guards/perfiles.
5. CRUD candidatos.
6. Relaciones de idiomas, programas, formación, experiencia y habilidades.
7. Gestión documental de CVs.
8. Buscador avanzado con RPC search_candidates(filters jsonb).
9. Exportación.
10. Importación desde Access/CSV.
11. Tests y hardening.

Después de cada fase:
- Explica qué se ha implementado.
- Indica cómo probarlo.
- Ejecuta o propone los comandos de test/lint correspondientes.
```

## 23. Notas para el agente de IA

- No introducir otro backend distinto de Supabase.
- No crear API REST propia en Node/Express salvo justificación expresa.
- No usar SQL Server.
- No usar Entity Framework.
- No usar ASP.NET Core.
- No convertir Access en backend operativo.
- No exponer claves service role en Angular.
- No crear buckets públicos para CVs.
- No implementar filtros complejos solo en frontend si pueden resolverse mejor mediante RPC SQL.
- No saltarse RLS en tablas con datos personales.
- No incluir rutas locales de archivos en enlaces visibles para el usuario.
- Mantener coherencia con el proyecto `Kepler_tickets`.
