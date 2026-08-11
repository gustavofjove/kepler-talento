---
name: 'Enrich User Story'
description: Enriquecer un brief de ticket KTL en una historia de usuario completa y accionable
category: Workflow
tags: [product, spec, openspec]
---

Analiza y enriquece el ticket indicado en `openspec/KTL-{code}.md`.

**Uso**: `/enrich-us KTL-3`, `/enrich-us 3`, o referencias como "el de la búsqueda avanzada" / "el que está en progreso".

**Pasos**

1. **Localizar el ticket.** Busca en [openspec/](openspec/) el fichero `KTL-{code}.md`. El argumento puede ser el id del ticket, un número suelto, o palabras clave que describan su contenido. Si hay ambigüedad entre varios ficheros, pregunta cuál antes de seguir.

2. **Adoptar el rol**: experto de producto con conocimiento técnico del stack de este proyecto.

3. **Entender el problema** descrito en el ticket.

4. **Resolver ambigüedades antes de escribir.** Identifica todo hueco, contexto ausente o pregunta abierta que impida producir una historia completa y precisa. Si existe alguno, plantea al usuario **todas las preguntas de una vez** (no en varias rondas) y espera sus respuestas antes de continuar al paso 5. Usa la herramienta AskUserQuestion cuando las preguntas admitan opciones acotadas.

5. **Evaluar si la historia ya está completa** según las buenas prácticas de producto. Una historia completa para este proyecto incluye:
   - Descripción funcional completa, desde la perspectiva de la persona usuaria.
   - Cambios de UI concretos: componentes afectados bajo [src/app/features/](src/app/features/) (`admin`, `candidates`, `catalogs`, `dashboard`, `documents`, `search`) y piezas reutilizables en [src/app/shared/](src/app/shared/).
   - Contrato de datos: tablas, vistas, funciones RPC y políticas RLS de Supabase afectadas, con las migraciones necesarias en [supabase/migrations/](supabase/migrations/) y las Edge Functions en [supabase/functions/](supabase/functions/). Este proyecto no expone una API REST propia — el acceso a datos va por el cliente de Supabase en [src/app/core/supabase/](src/app/core/supabase/) y los servicios de [src/app/core/services/](src/app/core/services/). No inventes endpoints.
   - Ficheros a modificar según la arquitectura existente (standalone components, signals, servicios en `core`, modelos en `shared/models`).
   - Criterios de aceptación verificables, redactados como escenarios comprobables.
   - Cobertura de pruebas: unitarias/integración con Jest en [tests/unit/](tests/unit/) y [tests/integration/](tests/integration/), e2e con Playwright en [tests/e2e/](tests/e2e/), y comprobaciones de seguridad en [tests/security/](tests/security/) cuando se toquen permisos o RLS.
   - Documentación a actualizar: [README.md](README.md), [docs/](docs/), [SUPABASE_INTEGRATION_GUIDE.md](SUPABASE_INTEGRATION_GUIDE.md) o la spec correspondiente en [specs/](specs/).
   - Requisitos no funcionales: seguridad (RLS, exposición de datos personales de candidatos), rendimiento de las consultas de búsqueda, accesibilidad, y comportamiento responsive.
   - Textos de interfaz en español, con tildes y ortografía correctas.

6. **Si le falta detalle**, redacta una versión mejorada: más clara, más específica y más concisa. Apóyate en el contexto técnico real del repositorio ([AGENTS.md](AGENTS.md), [especificacion_tecnica_app_gestion_cvs_supabase_angular.md](especificacion_tecnica_app_gestion_cvs_supabase_angular.md), [SUPABASE_INTEGRATION_GUIDE.md](SUPABASE_INTEGRATION_GUIDE.md), [specs/](specs/) y el propio código) en lugar de suponer. Devuélvela en markdown.

7. **Actualizar el ticket.** Escribe en `openspec/KTL-{code}.md` conservando el contenido original: marca las secciones con encabezados `## [original]` y `## [enhanced]`, y añade la nueva por debajo de la antigua. Aplica formato legible (listas, tablas, bloques de código). Si el fichero ya tiene una sección `## [enhanced]`, reemplázala en lugar de acumular otra.

**Después**

El brief enriquecido es la entrada, no el plan de trabajo. Para convertirlo en artefactos ejecutables, lanza el flujo de OpenSpec sobre él:

```
/opsx:new
```

y pasa el fichero `openspec/KTL-{code}.md` como descripción del cambio.

**Guardarraíles**

- No modifiques código en este comando — solo el fichero del ticket.
- No borres ni reescribas el texto original del usuario; queda bajo `## [original]`.
- Si el ticket ya está completo según el paso 5, dilo y no inventes relleno.
