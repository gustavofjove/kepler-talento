# Kepler Talento

Aplicación interna de RR. HH. para gestionar candidatos y CVs. La interfaz actual es una
SPA React; KTL-5 añade la plataforma objetivo ASP.NET Core, PostgreSQL, almacenamiento
privado de documentos, ClamAV y Nginx. Las funcionalidades existentes que todavía usan
`localStorage` o Supabase se mantienen hasta que cada vertical se migre explícitamente.

## Plataforma KTL-5

- React 19, TypeScript 5.9 y Vite 7.
- .NET SDK 10.0.102 y ASP.NET Core 10.
- FastEndpoints, MediatR, FluentValidation, EF Core/Npgsql y Serilog.
- PostgreSQL 17.6.
- almacenamiento de ficheros privado con cuarentena y límite de 20 MB.
- ClamAV 1.4.3 en una red Docker privada.
- Nginx sin privilegios como único punto publicado, en `http://localhost:4200`.

La autenticación de producción está aplazada. El actor sintético solo existe en
Development/Testing y el proceso se niega a arrancar si se intenta habilitar en
Production.

## Catálogos KTL-6

Los catálogos de negocio ya no viven en `localStorage`: son propiedad de la API y se sirven
desde PostgreSQL (`CAT_CatalogItems`). Las capacidades `catalogs.read` y `catalogs.manage`
se aplican en el servidor y se corresponden con el permiso `manage_catalogs` del frontend;
ocultar la interfaz no es el control.

No existe ningún endpoint de borrado físico: un valor se retira desactivándolo, de modo que
los candidatos que lo referencian conservan su significado. Las nueve familias se cargan con
una semilla explícita de despliegue durante `--migrate`. La clave `rrhh-catalogs` del
navegador queda abandonada; no se migra.

El detalle de rutas, códigos de error, unicidad de nombres, concurrencia y auditoría está en
[`docs/ktl-6/catalogs.md`](docs/ktl-6/catalogs.md).

## Requisitos

- Docker Desktop con Compose v2. El escáner necesita aproximadamente 3 GiB de RAM y se
  recomiendan 4 GiB disponibles para Docker.
- .NET SDK 10.0.102 para trabajar fuera de contenedores.
- Node.js 22 y npm 10 para trabajar fuera de contenedores.

Los paquetes NuGet se fijan centralmente en `backend/Directory.Packages.props`. La
herramienta `dotnet-ef` queda fijada mediante `.config/dotnet-tools.json`.

## Arranque del stack completo

```powershell
Copy-Item .env.example .env
docker compose up --build
```

El migrador termina antes de que arranque la API. Nginx sirve la SPA y reenvía `/api` al
backend con el mismo origen. PostgreSQL, ClamAV y los directorios de documentos no
publican puertos ni rutas al host.

Rutas de diagnóstico:

- `http://localhost:4200/api/health/live`: proceso vivo.
- `http://localhost:4200/api/health/ready`: PostgreSQL, migraciones y almacenamiento.
- `http://localhost:4200/api/health/scanner`: estado separado de ClamAV; puede estar
  degradado sin impedir la descarga de documentos que ya estaban limpios.
- `http://localhost:4200/platform/reference`: arnés sintético, solo en Development/Test.

Para detener los contenedores sin borrar datos:

```powershell
docker compose down
```

Para conectar una herramienta local a PostgreSQL, aplique el override de desarrollo. El
puerto se publica exclusivamente en la interfaz loopback y no queda accesible desde la
intranet:

```powershell
docker compose -f docker-compose.yml -f docker-compose.local.yml up -d postgres
```

Use `localhost`, el puerto `POSTGRES_PORT` (5432 por defecto), la base
`kepler_talento` y el usuario `ktl_migrator`. No use este override en el despliegue de
intranet.

Los volúmenes `postgres-data`, `documents` y `clamav-signatures` son persistentes. No use
`docker compose down --volumes` salvo que quiera eliminar deliberadamente los datos de
desarrollo.

## Trabajo local sin Compose

```powershell
npm ci
dotnet tool restore
dotnet restore backend/KeplerTalento.slnx
npm run build:all
npm test
npm run test:backend
```

La API nunca migra automáticamente durante el arranque normal. La migración explícita es:

```powershell
dotnet run --project backend/Web/KeplerTalento.Web.csproj -- --migrate
```

Requiere `ConnectionStrings__ApplicationDatabase` y una identidad de migración con permiso
DDL. La API usa una identidad distinta (`ktl_runtime`) con DML limitado a las tablas
aprobadas.

## Imágenes, configuración y almacenamiento

Las imágenes son reproducibles y multi-stage: `backend/Dockerfile` y
`Dockerfile.frontend`. Los valores de desarrollo están documentados en `.env.example`;
los secretos reales no deben versionarse. Dentro del volumen de documentos existen raíces
separadas `quarantine` y `available`; PostgreSQL almacena únicamente claves opacas, nunca
rutas de host, UNC ni nombres físicos derivados de candidatos.

Si la API no está lista, revise en este orden: migrador, PostgreSQL, permisos de escritura
del volumen y configuración. Si solo falla `/api/health/scanner`, revise la memoria de
Docker, la actualización de firmas y los límites de ClamAV. Los documentos nuevos se
mantienen en cuarentena durante la incidencia.

## Backup, restore y operación

El procedimiento operativo está en
[`docs/ktl-5/operator-runbook.md`](docs/ktl-5/operator-runbook.md). Los comandos principales
son:

```powershell
./scripts/operations/backup.ps1
./scripts/operations/restore.ps1 -RecoveryId <yyyyMMddTHHmmssZ> -ConfirmNonProduction
./scripts/operations/reconcile.ps1
```

Cada recovery set contiene dump PostgreSQL, archivos y manifiesto con hashes, sin
credenciales. La base inicial es backup diario, RPO de 24 horas, RTO de 4 horas y 30 días
de retención.

## Calidad y seguridad

```powershell
npm run lint
npm run format:check
npm run security:rls
npm run security:storage
npm run test:security
npm run release:gate
```

`security:rls` conserva el nombre histórico del gate, pero para KTL-5 valida la frontera
PostgreSQL privada y sus grants de mínimo privilegio. Las comprobaciones Supabase siguen
aplicándose solo a rutas heredadas no migradas.

## Alcance aplazado

KTL-5 no decide ni implementa autenticación, migración de datos Access, topología final de
producción, SMB ni el comportamiento de producto CSV. Tampoco cambia en bloque los
servicios funcionales actuales: aporta las interfaces y operaciones reutilizables para
cambios posteriores.

Documentación principal:

- [Brief KTL-5](openspec/KTL-5.md)
- [Brief KTL-6](openspec/KTL-6.md) y [catálogos KTL-6](docs/ktl-6/catalogs.md)
- [Cambio OpenSpec](openspec/changes/ktl-5-dotnet-infrastructure)
- [Plan técnico existente](specs/001-gestion-cvs-rrhh/plan.md)
- [Principios vigentes](openspec/config.yaml)
