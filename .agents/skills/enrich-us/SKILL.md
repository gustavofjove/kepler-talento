---
name: enrich-us
description: Enriquecer un brief de ticket KTL (openspec/KTL-*.md) en una historia de usuario completa y accionable. Úsalo cuando el usuario pida enriquecer, completar o revisar un ticket KTL antes de crear un cambio OpenSpec.
---

<!-- Canonical copy shared by Codex ($enrich-us) and Claude Code (/enrich-us, which includes this file). Edit only here. -->

Analiza y enriquece el ticket indicado en `openspec/KTL-{code}.md`.

**Uso**: `$enrich-us KTL-3` (Codex) o `/enrich-us KTL-3` (Claude Code); también `3`, o referencias como "el de la búsqueda avanzada" / "el que está en progreso".

**Pasos**

1. **Localizar el ticket.** Busca en [openspec/](openspec/) el fichero `KTL-{code}.md`. El argumento puede ser el id del ticket, un número suelto, o palabras clave que describan su contenido. Si hay ambigüedad entre varios ficheros, pregunta cuál antes de seguir.

2. **Adoptar el rol**: experto de producto con conocimiento técnico del stack de este proyecto.

3. **Entender el problema** descrito en el ticket.

4. **Resolver ambigüedades antes de escribir.** Identifica todo hueco, contexto ausente o pregunta abierta que impida producir una historia completa y precisa. Si existe alguno, plantea al usuario **todas las preguntas de una vez** (no en varias rondas) y espera sus respuestas antes de continuar al paso 5. Si tu herramienta ofrece preguntas con opciones (por ejemplo AskUserQuestion en Claude Code), úsala cuando las preguntas admitan opciones acotadas.

5. **Evaluar si la historia ya está completa** según las buenas prácticas de producto. Una historia completa para este proyecto incluye:
   - Descripción funcional completa, desde la perspectiva de la persona usuaria.
   - Cambios de UI concretos: componentes afectados bajo [frontend/src/app/features/](frontend/src/app/features/) (`admin`, `candidates`, `catalogs`, `dashboard`, `documents`, `search`) y piezas reutilizables en [frontend/src/app/shared/](frontend/src/app/shared/).
   - Contrato de datos y API: el backend objetivo es la API ASP.NET Core de [backend/](backend/) sobre PostgreSQL propio. Indica los endpoints afectados en `backend/Web/Features/<Feature>/`, los comandos/consultas MediatR en `backend/Application/Features/<Feature>/`, las tablas (con prefijo registrado `CND_`, `CAT_`, `OPS_`, `AUD_`, `ADM_`) y las migraciones EF Core en `backend/Infrastructure/Persistence/Migrations/`, junto con los permisos (`ICurrentActor`) y los grants de `ktl_runtime`. Comprueba los endpoints que ya existen antes de proponer otros nuevos; no inventes rutas. Supabase y `localStorage` son rutas heredadas: no se amplían, solo se migran.
   - Ficheros a modificar según la arquitectura existente: en el frontend, componentes de función React en `.tsx`, servicios singleton con el shim de señales de `core/state/`, inyección vía `core/di/services.ts` y llamadas a la API a través de `core/http/api-transport.ts`; en el backend, slices verticales `Domain` → `Application` → `Infrastructure` → `Web`.
   - Criterios de aceptación verificables, redactados como escenarios comprobables.
   - Cobertura de pruebas: unitarias/integración con Vitest (y React Testing Library para componentes) en [frontend/tests/unit/](frontend/tests/unit/) y [frontend/tests/integration/](frontend/tests/integration/), e2e con Playwright en [frontend/tests/e2e/](frontend/tests/e2e/), pruebas xUnit unitarias e integración con PostgreSQL real en [backend/Tests/](backend/Tests/), y comprobaciones de seguridad en [frontend/tests/security/](frontend/tests/security/) cuando se toquen permisos, grants o almacenamiento.
   - Documentación a actualizar: [README.md](README.md), `docs/ktl-<n>/` o la spec de la capacidad en [openspec/specs/](openspec/specs/).
   - Requisitos no funcionales: seguridad (autorización en la API que falla cerrada, mínimo privilegio en base de datos, exposición de datos personales de candidatos), rendimiento de las consultas de búsqueda, accesibilidad, y comportamiento responsive.
   - Textos de interfaz en español, con tildes y ortografía correctas.

6. **Si le falta detalle**, redacta una versión mejorada: más clara, más específica y más concisa. Apóyate en el contexto técnico real del repositorio ([AGENTS.md](AGENTS.md), [openspec/config.yaml](openspec/config.yaml), [openspec/kepler-talento-stack-blueprint.md](openspec/kepler-talento-stack-blueprint.md), [openspec/specs/](openspec/specs/), [docs/](docs/) y el propio código) en lugar de suponer. Devuélvela en markdown.

7. **Actualizar el ticket.** Escribe en `openspec/KTL-{code}.md` conservando el contenido original: marca las secciones con encabezados `## [original]` y `## [enhanced]`, y añade la nueva por debajo de la antigua. Aplica formato legible (listas, tablas, bloques de código). Si el fichero ya tiene una sección `## [enhanced]`, reemplázala en lugar de acumular otra.

**Después**

El brief enriquecido es la entrada, no el plan de trabajo. Para convertirlo en artefactos ejecutables, lanza el flujo de OpenSpec sobre él —`$openspec-new-change` en Codex o `/opsx:new` en Claude Code— y pasa el fichero `openspec/KTL-{code}.md` como descripción del cambio.

**Guardarraíles**

- No modifiques código en esta skill — solo el fichero del ticket.
- No borres ni reescribas el texto original del usuario; queda bajo `## [original]`.
- Si el ticket ya está completo según el paso 5, dilo y no inventes relleno.
