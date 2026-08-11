# RRHH BBDD

Aplicacion interna de RRHH para gestionar CVs de candidatos, definida con un
flujo de Spec-Driven Development mediante GitHub Spec Kit.

## Estado

La fase actual es de definicion funcional y tecnica. Todavia no hay
implementacion de la aplicacion: el repositorio contiene la especificacion,
constitucion, plan, modelo de datos, contratos, checklists y tareas necesarias
para iniciar la implementacion de forma controlada.

## Objetivo

Sustituir el uso operativo de Access por una aplicacion web interna que permita
a RRHH:

- registrar y mantener candidatos;
- asociar idiomas, programas, formacion, experiencia, habilidades y documentos;
- almacenar CVs en un repositorio privado;
- buscar candidatos con filtros avanzados combinados;
- exportar resultados de forma controlada;
- importar datos depurados desde Access o CSV;
- aplicar roles, RLS, auditoria y controles de proteccion de datos.

## Stack previsto

- Angular 21
- TypeScript 5.9
- RxJS
- Angular CDK
- Tailwind CSS 3
- `@ngx-translate`
- Supabase Auth
- PostgreSQL 17
- Supabase Storage privado
- Supabase Edge Functions con TypeScript/Deno
- Jest 30
- Playwright
- Docker y Nginx unprivileged

## Documentacion principal

- [Especificacion funcional](specs/001-gestion-cvs-rrhh/spec.md)
- [Plan tecnico](specs/001-gestion-cvs-rrhh/plan.md)
- [Investigacion y decisiones](specs/001-gestion-cvs-rrhh/research.md)
- [Modelo de datos](specs/001-gestion-cvs-rrhh/data-model.md)
- [Contratos](specs/001-gestion-cvs-rrhh/contracts)
- [Guia de validacion](specs/001-gestion-cvs-rrhh/quickstart.md)
- [Tareas de implementacion](specs/001-gestion-cvs-rrhh/tasks.md)
- [Principios y reglas del proyecto](openspec/config.yaml)

## Flujo de trabajo

El repositorio sigue OpenSpec. Cada cambio avanza por sus artefactos:

```text
Proposal -> Specs -> Design -> Tasks -> Implementation -> Archive
```

Los briefs de ticket se escriben en `openspec/KTL-*.md` y se enriquecen con
`/enrich-us`. A partir de ahi, `/opsx:new` crea el cambio en
`openspec/changes/<nombre>/` y `/opsx:continue` genera un artefacto por
invocacion; `/opsx:apply` implementa las tareas y `/opsx:archive` cierra el
cambio y sincroniza las specs.

La documentacion de `specs/001-gestion-cvs-rrhh/` describe el sistema tal como
esta construido hoy y se mantiene como referencia; el trabajo nuevo no se anade
ahi. La trazabilidad con los requisitos `FR-*` y criterios `SC-*` se mantiene
desde los artefactos del cambio activo.

## Seguridad y datos

Los CVs y datos de candidatos son informacion personal. La base Access local se
mantiene fuera del repositorio y solo debe usarse como referencia funcional o
fuente de importacion depurada.

Reglas clave:

- no subir bases `.accdb` o `.mdb`;
- no exponer claves `service_role` en frontend;
- no usar buckets publicos para CVs;
- no exponer rutas internas de Storage;
- aplicar RLS en tablas con datos personales;
- registrar auditoria para operaciones sensibles.

## Estructura actual

```text
openspec/                         Configuracion, cambios y specs de OpenSpec
specs/001-gestion-cvs-rrhh/       Documentacion de diseno del sistema actual
AGENTS.md                         Contexto gestionado para agentes
SUPABASE_INTEGRATION_GUIDE.md     Patron de integracion Supabase
especificacion_tecnica_*.md       Documento tecnico fuente del dominio RRHH/CVs
```

## Siguiente paso

La siguiente fase es implementar siguiendo `tasks.md`, empezando por la
infraestructura base, migraciones Supabase, autenticacion y controles de acceso.

## Gates Operativos

- `npm run test:integration`: contratos de Edge Functions y guardrails.
- `npm run smoke:staging`: checklist rapido de staging.
- `npm run release:gate`: gate automatizado para build, unit, integration, y checks de seguridad.

Runbook de backup/restore/rollback: [docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md](docs/BACKUP_RESTORE_ROLLBACK_RUNBOOK.md)

## Ejecucion local con Docker Desktop

El frontend se puede construir y servir en Docker con Nginx:

```powershell
docker compose -f docker-compose.frontend.yml up -d --build
```

Por defecto usa el puerto `63151`. Si ese puerto ya esta ocupado:

```powershell
$env:FRONTEND_PORT="63152"
docker compose -f docker-compose.frontend.yml up -d --build
```

## Convivencia de frontends locales

En el entorno actual conviven dos frontends del ecosistema Kepler:

- `RRHH BBDD` en `http://localhost:63151`
- `KeplerDesk` en `http://localhost:63153`

Si necesitas volver a mover `KeplerDesk`, recreate el contenedor con otro
puerto host libre manteniendo `63151` reservado para RRHH BBDD.

URL local:

```text
http://localhost:63151
```

o el puerto alternativo que hayas indicado.

Para parar el contenedor:

```powershell
docker compose -f docker-compose.frontend.yml down
```

La aplicacion arranca en modo local/demo si `SUPABASE_URL` y
`SUPABASE_ANON_KEY` estan vacios. Cuando exista un entorno Supabase, copia
`.env.example` a `.env` y rellena esas variables.
