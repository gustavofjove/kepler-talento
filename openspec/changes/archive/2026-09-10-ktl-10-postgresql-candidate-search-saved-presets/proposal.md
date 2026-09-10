## Why

La búsqueda actual filtra en el navegador después de cargar todos los candidatos y sus agregados, un modelo que deja de ser viable tras los cortes KTL-8 y KTL-9 y que no escala con el conjunto migrado por KTL-7. RRHH necesita conservar la paridad funcional de búsqueda y sus presets mediante consultas paginadas y autorizadas en PostgreSQL antes de retirar ese estado completo del navegador.

## What Changes

- Añadir una búsqueda de candidatos en la API que implemente todo el contrato `SearchFilters`, combine familias con `AND`, aplique `ANY`/`ALL` dentro de cada familia, ignore filtros vacíos y nunca duplique candidatos.
- Devolver únicamente la proyección mínima `SearchResult`, excluir candidatos eliminados lógicamente y resolver `hasCv` contra el CV principal disponible de KTL-9.
- Paginar y limitar los resultados en el servidor, elegir índices a partir del conjunto real migrado por KTL-7 y cancelar mediante `AbortSignal` las consultas sustituidas por otra más reciente.
- Persistir presets por propietario en PostgreSQL mediante la API, con nombre único por propietario, listado, creación, renombrado, actualización, eliminación y seguimiento de último uso.
- Declarar no compatibles los presets locales de `rrhh.search.presets.v1`, incluidas las formas antiguas con `languageValues` y `programValues`, y retirar esa clave del código; conservar `rrhh.search.last-filters.v1` como preferencia local del navegador. Esta decisión evita asociar datos de navegador a un propietario de API sin una identidad migrable fiable.
- Exigir `view_candidates` en toda búsqueda y cerrar en el diseño el alcance de `view_all_candidates`; no registrar términos de búsqueda ni ampliar la proyección con datos personales innecesarios.
- Mantener fuera de alcance la exportación CSV, la autenticación, los presets compartidos y la búsqueda difusa, ranqueada o por relevancia.
- Actores: profesionales de RRHH que buscan candidatos y administran sus propios presets; administradores con permisos amplios cuya visibilidad deberá quedar definida; usuarios no autenticados o sin permiso, que serán rechazados sin datos.
- Entidades principales: candidato y sus relaciones de habilidades, idiomas y programas; estado de CV principal; propietario y preset de búsqueda.
- Supuestos y bordes: KTL-6, KTL-8 y KTL-9 proporcionan catálogos, agregado de candidato y estado documental; criterios sin nivel coinciden con cualquier nivel; selecciones vacías no restringen; nombres de preset se comparan sin distinguir mayúsculas; peticiones fuera de rango se rechazan o acotan de forma estable.
- Éxito medible: paridad con el buscador actual sobre una fixture compartida, semántica correcta para todas las familias y modos, cero duplicados, límites aplicados por el servidor, presets con ciclo completo y tratamiento legado documentado, consultas sustituidas realmente abortadas, y evidencia unitaria, de integración, arquitectura, seguridad y Playwright.

## Capabilities

### New Capabilities

- `candidate-search`: Búsqueda PostgreSQL paginada y acotada de candidatos con el contrato de filtros existente, proyección mínima, semántica `ANY`/`ALL`, estado de CV principal y autorización cerrada por defecto.
- `saved-search-presets`: Persistencia API de presets privados por propietario, ciclo de vida completo, último uso, retirada de la persistencia local y tratamiento explícito de las formas legadas.

### Modified Capabilities

Ninguna. La búsqueda usará los requisitos ya vigentes de `frontend-api-transport` para transporte compartido, cancelación y servicios sustituibles en pruebas.

## Impact

- Frontend: `CandidateSearchService`, `SearchPresetsService`, modelos y página/componentes de búsqueda, DI y pruebas; desaparece el fan-out de agregados y la persistencia de presets en `localStorage`, pero permanece `rrhh.search.last-filters.v1`.
- Backend: nuevos slices verticales, contratos HTTP, repositorios/consultas PostgreSQL, tablas `ADM_` o adyacentes a candidato para presets, migración EF Core repetible, índices y grants mínimos de runtime.
- Seguridad y datos personales: se consultan identidad/contacto de candidatos y los términos pueden contener datos personales. Se preservan los principios 1 y 3 mediante proyección mínima, exclusión lógica, ausencia de términos en logs, autorización API `view_candidates` en cada consulta, alcance explícito de `view_all_candidates`, PostgreSQL no accesible desde el navegador y límites de paginación. No se cambian políticas RLS de Supabase, acceso a almacenamiento ni definiciones de roles; sí se decide el uso observable de permisos existentes y se prueba el fallo cerrado.
- Dependencias: KTL-6, KTL-8 y KTL-9 deben aportar los datos y contratos base; KTL-7 aporta el conjunto migrado con el que se justifican los índices. No se añade una dependencia runtime nueva.
