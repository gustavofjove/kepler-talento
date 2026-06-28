# Feature Specification: Gestion de CVs para RRHH

**Feature Branch**: `[001-gestion-cvs-rrhh]`

**Created**: 2026-06-28

**Status**: Definition Complete - ready for implementation planning handoff

**Input**: User description: "Basandose en los documentos markdown del proyecto, definir la aplicacion interna de RRHH para gestion de CVs con Spec Kit."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Acceso seguro a la aplicacion (Priority: P1)

Un usuario autorizado de RRHH inicia sesion, completa los controles de seguridad
requeridos por la organizacion y accede solo a las pantallas y datos permitidos
por su rol.

**Why this priority**: Sin acceso autenticado y autorizado no puede existir uso
seguro de datos personales de candidatos.

**Independent Test**: Se puede probar con usuarios de distintos roles verificando
que cada uno entra, ve solo las funciones permitidas y que un usuario no
autorizado no accede a datos de candidatos ni documentos.

**Acceptance Scenarios**:

1. **Given** un usuario autorizado activo, **When** inicia sesion y completa los
   pasos de seguridad requeridos, **Then** accede a la aplicacion con las
   acciones correspondientes a su rol.
2. **Given** un usuario no autenticado, **When** intenta abrir una pantalla
   protegida, **Then** el sistema deniega el acceso y no muestra datos.
3. **Given** un usuario con permisos de solo lectura, **When** intenta crear,
   editar, exportar o descargar documentos sin permiso, **Then** la accion se
   bloquea y queda claro que no tiene autorizacion.

---

### User Story 2 - Registrar y mantener candidatos (Priority: P1)

Un usuario de RRHH registra candidatos con sus datos principales, actualiza su
estado, mantiene fechas relevantes, conserva observaciones internas y aplica baja
logica cuando el candidato ya no debe estar activo.

**Why this priority**: La aplicacion sustituye el uso operativo de Access; el
registro fiable de candidatos es el nucleo minimo del producto.

**Independent Test**: Se puede crear, consultar, editar y dar de baja
logicamente un candidato sin depender de busqueda avanzada, exportacion o
migracion.

**Acceptance Scenarios**:

1. **Given** un usuario RRHH con permiso de alta, **When** introduce los datos
   minimos de un candidato, **Then** el candidato queda disponible para consulta
   con trazabilidad de creacion.
2. **Given** un candidato existente, **When** RRHH actualiza estado, contacto,
   disponibilidad, consentimiento o fecha de revision, **Then** los cambios se
   guardan y queda trazabilidad de modificacion.
3. **Given** un candidato que debe retirarse del uso operativo, **When** RRHH
   aplica baja logica, **Then** el candidato deja de aparecer como activo sin
   destruir su historial.

---

### User Story 3 - Enriquecer el perfil profesional del candidato (Priority: P2)

Un usuario de RRHH asocia a cada candidato idiomas, programas o herramientas,
formacion, experiencia laboral y habilidades, pudiendo mantener varios registros
de cada tipo.

**Why this priority**: La busqueda util para RRHH depende de informacion
relacional rica y mantenible.

**Independent Test**: Se puede completar el perfil de un candidato y comprobar
que cada grupo de informacion se guarda, consulta, edita y elimina logicamente
sin duplicar datos del candidato principal.

**Acceptance Scenarios**:

1. **Given** un candidato existente, **When** RRHH anade varios idiomas con
   niveles, **Then** el perfil muestra todos los idiomas asociados.
2. **Given** un candidato existente, **When** RRHH registra programas,
   formaciones, experiencias y habilidades, **Then** cada bloque queda asociado
   al candidato correcto.
3. **Given** datos repetidos o incompletos, **When** RRHH intenta guardarlos,
   **Then** el sistema evita inconsistencias y explica que debe corregirse.

---

### User Story 4 - Gestionar CVs y documentos privados (Priority: P2)

Un usuario autorizado adjunta o vincula el PDF del CV de un candidato, marca un
CV principal cuando proceda y abre el documento mediante acceso seguro.

**Why this priority**: El CV es el documento central del proceso y contiene
informacion personal sensible que no debe quedar expuesta.

**Independent Test**: Se puede subir un documento, verlo asociado al candidato,
abrirlo solo con permisos validos y comprobar que no existe enlace publico ni
ruta interna visible.

**Acceptance Scenarios**:

1. **Given** un candidato existente, **When** RRHH sube un PDF de CV, **Then** el
   documento queda asociado al candidato y marcado como disponible.
2. **Given** un usuario autorizado, **When** solicita abrir un CV, **Then** recibe
   acceso temporal o controlado al documento.
3. **Given** un usuario sin permiso de descarga, **When** intenta abrir un CV,
   **Then** el sistema deniega la operacion.

---

### User Story 5 - Buscar candidatos con filtros combinados (Priority: P1)

Un usuario de RRHH busca candidatos mediante filtros combinados por estado,
disponibilidad, fechas, idiomas, programas, formacion, experiencia, habilidades y
disponibilidad de CV, obteniendo resultados sin duplicados.

**Why this priority**: La necesidad principal de RRHH es localizar candidatos
rapidamente segun criterios profesionales y operativos.

**Independent Test**: Se puede preparar un conjunto de candidatos de muestra,
aplicar filtros vacios, simples y combinados, y verificar que los resultados son
correctos y no duplicados.

**Acceptance Scenarios**:

1. **Given** filtros vacios, **When** RRHH ejecuta una busqueda, **Then** el
   sistema no restringe por esos filtros vacios.
2. **Given** filtros de familias diferentes, **When** RRHH busca candidatos,
   **Then** el sistema combina las familias mediante criterio acumulativo.
3. **Given** varios idiomas o programas seleccionados, **When** RRHH elige modo
   "cualquiera" o "todos", **Then** los resultados respetan esa semantica.
4. **Given** candidatos con multiples relaciones, **When** aparecen en resultados,
   **Then** cada candidato se muestra una sola vez.

---

### User Story 6 - Exportar resultados controlados (Priority: P3)

Un usuario con permiso exporta resultados de busqueda a un fichero util para
trabajo interno, sin exponer rutas tecnicas ni datos no necesarios.

**Why this priority**: RRHH necesita explotar listados, pero la exportacion debe
estar gobernada por permisos y proteccion de datos.

**Independent Test**: Se puede ejecutar una busqueda, exportar resultados y
verificar que el fichero contiene los campos permitidos y no incluye rutas
internas de documentos.

**Acceptance Scenarios**:

1. **Given** resultados de busqueda y permiso de exportacion, **When** RRHH
   exporta, **Then** obtiene un fichero con los campos funcionales acordados.
2. **Given** un usuario sin permiso de exportacion, **When** intenta exportar,
   **Then** la accion queda bloqueada.
3. **Given** un resultado con CV asociado, **When** se exporta, **Then** el
   fichero no revela rutas internas ni enlaces permanentes.

---

### User Story 7 - Importar datos depurados desde Access o CSV (Priority: P3)

Un usuario tecnico o administrador importa datos depurados desde la base Access
existente o sus CSV exportados, valida errores y conserva un informe de carga.

**Why this priority**: La base Access es una referencia funcional y posible
fuente inicial, pero no debe seguir siendo backend operativo.

**Independent Test**: Se puede importar un lote pequeno de muestra, comprobar
candidatos y relaciones cargadas, revisar errores y validar con RRHH registros
representativos.

**Acceptance Scenarios**:

1. **Given** un CSV depurado de candidatos, **When** se importa, **Then** se crean
   candidatos y relaciones validas sin duplicidades evidentes.
2. **Given** filas invalidas, **When** se procesa la importacion, **Then** el
   sistema las rechaza o marca con errores trazables sin detener todo el lote
   valido.
3. **Given** la base Access original, **When** se completa la migracion inicial,
   **Then** Access queda solo como referencia historica y no como fuente
   operativa.

### Edge Cases

- Un candidato puede no tener CV adjunto todavia; debe poder registrarse y
  aparecer en busquedas que incluyan o excluyan esa condicion.
- Dos candidatos pueden compartir nombre y apellidos; la aplicacion debe
  permitir distinguirlos por datos adicionales sin fusionarlos automaticamente.
- Un filtro multi-valor puede tener modo "cualquiera" o "todos"; el resultado
  debe cambiar de forma predecible.
- Los documentos no PDF o demasiado grandes deben rechazarse con mensaje claro.
- Una fecha de consentimiento, revision o caducidad ausente debe tratarse segun
  reglas de RRHH sin ocultar el riesgo.
- Los usuarios desactivados o sin rol valido no deben conservar acceso efectivo.
- Las exportaciones vacias deben producir un resultado comprensible, no un error
  tecnico.
- Los datos importados desde Access pueden venir duplicados, incompletos o con
  relaciones desnormalizadas.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow only authenticated and authorized users to
  access candidate data and protected actions.
- **FR-002**: The system MUST support roles for full RRHH administration, RRHH
  operational work, limited manager reading, readonly access, and technical
  administration.
- **FR-003**: The system MUST allow authorized RRHH users to create, read,
  update, and logically deactivate candidates.
- **FR-004**: The system MUST capture candidate name, surnames, phone, email,
  location, availability, status, source, internal notes, consent information,
  reception date, review/deletion date, creation metadata, and modification
  metadata when available.
- **FR-005**: The system MUST allow each candidate to have multiple languages,
  including level and optional certification information.
- **FR-006**: The system MUST allow each candidate to have multiple programs or
  tools, including level or experience notes.
- **FR-007**: The system MUST allow each candidate to have multiple education
  records with type, institution, dates, status, and notes.
- **FR-008**: The system MUST allow each candidate to have multiple work
  experience records with company, position, period, functions, sector, and
  notes.
- **FR-009**: The system MUST allow each candidate to have multiple skills or
  competencies.
- **FR-010**: The system MUST allow each candidate to have one or more
  associated documents, with support for a principal CV in the MVP.
- **FR-011**: The system MUST keep CV documents private and provide document
  opening only through permission-checked temporary or controlled access.
- **FR-012**: The system MUST prevent users from seeing internal storage paths,
  permanent private document locations, or service credentials.
- **FR-013**: The system MUST provide advanced search by candidate status,
  availability, dates, languages, programs, education, experience, skills, and
  CV availability.
- **FR-014**: The system MUST ignore empty search filters.
- **FR-015**: The system MUST combine different filter families with cumulative
  matching.
- **FR-016**: The system MUST support "any selected value" and "all selected
  values" matching for languages and programs.
- **FR-017**: The system MUST return each matching candidate only once in search
  results.
- **FR-018**: Search results MUST show at least name, surnames, phone, candidate
  status, and whether a secure CV action is available.
- **FR-019**: The system MUST allow authorized users to export search results in
  a controlled tabular format.
- **FR-020**: Exports MUST exclude internal storage paths and fields not needed
  for the selected business purpose.
- **FR-021**: The system MUST record basic audit information for candidate and
  document creation and modification.
- **FR-022**: The system MUST support controlled initial import from Access or
  CSV exports, including validation of candidates, relations, and import errors.
- **FR-023**: The system MUST keep Access as a reference or migration source
  only, never as the production operational database.
- **FR-024**: The system MUST provide user-friendly validation messages for
  missing mandatory data, invalid files, duplicate-prone records, and
  unauthorized actions.
- **FR-025**: The system MUST be usable in Spanish for RRHH users and structured
  so additional languages can be added later.

### Key Entities *(include if feature involves data)*

- **User Profile**: Represents an application user, their active status, role,
  and security-related settings.
- **Role**: Represents a permission group controlling who may view, create,
  edit, export, administer, or download candidate information.
- **Candidate**: Represents a person whose CV is managed by RRHH, including
  contact data, status, availability, consent and review dates, audit metadata,
  and logical deletion state.
- **Language**: A catalog item that can be associated with many candidates.
- **Candidate Language**: Relationship between a candidate and a language,
  including level, certification, and notes.
- **Program or Tool**: A catalog item representing software, program, or tool
  knowledge relevant to candidate matching.
- **Candidate Program**: Relationship between a candidate and a program or tool,
  including level, years or notes when available.
- **Education Record**: Academic or complementary training associated with a
  candidate.
- **Experience Record**: Work history associated with a candidate.
- **Skill**: A competency catalog item or freeform competency associated with a
  candidate.
- **Candidate Document**: A private document associated with a candidate,
  including document type, principal-CV flag, upload metadata, and functional
  document status.
- **Search Filter Set**: A user-selected group of criteria for locating
  candidates.
- **Export**: A generated result file and its business context, governed by user
  permissions.
- **Import Batch**: A controlled load from Access or CSV, including source,
  outcome, errors, and validation status.
- **Audit Event**: A record of important create, update, document, export, or
  import operations.

### Data Protection & Access *(include if feature involves personal, restricted, or document data)*

- Only authorized users may view, create, edit, export, deactivate, or download
  candidate information according to their role.
- CV documents must remain private; users may open them only through secure,
  temporary or otherwise permission-checked access.
- The application must preserve consent, reception, review/deletion, creation,
  and modification metadata where available.
- Candidate removal during normal use must be logical rather than destructive.
- Exports must be permission-controlled and exclude private storage paths,
  service credentials, permanent document URLs, and unnecessary personal data.
- Security must fail closed for unauthenticated users, inactive users, invalid
  roles, and users without document or export permission.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized RRHH user can create a candidate with required
  fields in under 3 minutes during acceptance testing.
- **SC-002**: An authorized RRHH user can locate candidates using combined
  filters and obtain results without duplicates in at least 95% of tested search
  scenarios.
- **SC-003**: Search supports empty filters, cumulative filters, and any/all
  matching for languages and programs across the MVP acceptance dataset.
- **SC-004**: 100% of unauthorized access tests for candidate data, CV download,
  export, and write operations are denied.
- **SC-005**: A CV attached to a candidate can be opened by an authorized user
  without exposing a permanent or internal storage path.
- **SC-006**: Exported files contain the agreed functional fields and contain no
  internal document paths in 100% of export acceptance tests.
- **SC-007**: A controlled import of a representative Access or CSV sample
  produces an import summary with loaded records and row-level errors.
- **SC-008**: RRHH can validate at least 10 representative migrated candidates
  against the original source before accepting the initial migration.
- **SC-009**: The MVP can be demonstrated end-to-end from login through
  candidate creation, profile enrichment, CV upload, search, secure CV opening,
  and export.

## Assumptions

- The first release is an internal web application for RRHH and authorized
  internal readers, not a public candidate portal.
- Spanish is the primary user-facing language for MVP.
- The MVP stores one principal CV per candidate, while allowing the model to
  support additional documents.
- Email may be shown in search results only if RRHH confirms it is needed for
  the operational workflow; otherwise it remains available in candidate detail.
- Access is available as a local reference database and/or CSV export source,
  but not as a live system dependency.
- Candidate hard deletion is out of scope for normal users in MVP.
- Notifications and realtime collaboration are not required for the first
  business definition unless added in planning.
