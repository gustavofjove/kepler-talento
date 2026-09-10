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

## Migración de Access KTL-7

El esquema de candidatos (`CND_Candidates` y sus tablas de relación de idiomas, programas,
formación, experiencia y habilidades) se entrega en este slice, junto con la herramienta que
lo rellena desde la base de datos Access heredada.

`backend/Tools/DataMigration`, compilada como `ktl-migrate`, es una **herramienta de línea de
comandos que ejecuta el operador**. Deliberadamente no es accesible por HTTP y ningún proyecto
de la API la referencia: mover todo el conjunto de datos de candidatos no es una superficie de
petición que el producto deba tener. **No** es la pantalla de administración «Importación»,
que valida un CSV plano y pequeño en el navegador y no crea ningún candidato.

```powershell
ktl-migrate validate --connection <string> --export <directory> [--mappings <file>] [--output <directory>]
ktl-migrate load     --connection <string> --export <directory> --pre-migration-backup <label> [--overwrite-app-edits]
ktl-migrate report   --connection <string> --run <guid>
```

- `validate` no escribe ningún dato de negocio: prepara la exportación en un esquema de
  staging, aplica todas las reglas de fila, resuelve todas las referencias de catálogo y
  verifica todos los documentos, y después emite el informe.
- `load` se niega a arrancar sin `--pre-migration-backup`; el informe nombra esa copia de
  seguridad como la forma de deshacer la ejecución.
- Las reejecuciones se basan en un identificador de origen por fila, de modo que corrigen en
  lugar de duplicar. Los registros que la aplicación haya modificado desde su carga se omiten
  y se informan, no se sobrescriben.

La herramienta se conecta con el rol de base de datos de **migración**, nunca con el rol de
runtime. Los valores de texto libre de Access se resuelven contra entradas de catálogo
existentes y nunca se crean automáticamente; todo lo que quede sin resolver se informa para
que se tome una decisión explícita.

Consulta [`docs/ktl-7/migration-runbook.md`](docs/ktl-7/migration-runbook.md) para la
secuencia del operador y el rollback, y
[`docs/ktl-7/access-export-procedure.md`](docs/ktl-7/access-export-procedure.md) para el
contrato de exportación.

## Candidatos KTL-8

Los candidatos ya no viven en `localStorage`: son propiedad de la API y se sirven desde
PostgreSQL. La clave `rrhh-candidates` guardaba la tabla completa de candidatos —identidad,
contacto, localización, consentimiento, retención, estado y observaciones— en claro y en
cada dispositivo.

> **Nota de versión.** En el primer arranque de esta versión, cada navegador **elimina** su
> copia local de candidatos. La eliminación se ejecuta antes de cualquier llamada a la API y
> con independencia de ella, así que ocurre incluso si el backend no está disponible. Los
> candidatos creados con versiones anteriores existían solo en ese navegador y no se
> conservan: la base de datos es ahora el sistema de referencia. Tampoco se crea ya el
> candidato de ejemplo `demo-1`; una base de datos vacía muestra una lista vacía.

Las capacidades `candidates.read`, `candidates.create`, `candidates.update` y
`candidates.delete` se aplican en el servidor antes de despachar cada petición, y se
corresponden con los permisos `view_candidates`, `create_candidates`, `edit_candidates` y
`delete_candidates` del frontend; ocultar la interfaz no es el control.

No existe ningún endpoint de borrado físico: un candidato se retira desactivándolo, lo que
registra la fecha de baja y mantiene el registro recuperable. La regla también se sostiene
en la base de datos, porque el rol de runtime no tiene `DELETE` sobre `CND_Candidates`.

También desaparece el slice de referencia de KTL-5 (`/api/reference/candidates/{id}`): el
slice de lectura real cumple su función con autorización por operación, y una segunda ruta
menos protegida hacia datos personales no debe sobrevivir.

El detalle de rutas, códigos de error, concurrencia, auditoría y contrato del frontend está
en [`docs/ktl-8/candidates.md`](docs/ktl-8/candidates.md).

## Documentos de candidato KTL-9

Los CV ya no son metadatos ni enlaces simulados. El frontend envía el archivo real como
`multipart/form-data` a la API, que lo guarda con una clave opaca en cuarentena, valida su
formato y programa el análisis antivirus. La aceptación responde `202 Accepted`: significa
«aceptado y en análisis», no «disponible inmediatamente». Solo un resultado `Clean` permite
la descarga; las respuestas nunca exponen claves ni rutas internas.

Las capacidades de backend `documents.upload` y `documents.download` se corresponden con
`upload_candidate_documents` y `download_candidate_documents` en el frontend. Ambas se
comprueban por petición y son independientes de `candidates.read`. El límite de contenido es
20 MiB para PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF y BMP; Nginx admite 21 MiB únicamente
para incluir el margen acotado del framing multipart.

Consulta el [contrato y flujo de documentos KTL-9](docs/ktl-9/documents.md) y la
[nota de versión](docs/ktl-9/release-notes.md).

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
- [Brief KTL-7](openspec/KTL-7.md), [runbook de migración](docs/ktl-7/migration-runbook.md) y
  [procedimiento de exportación](docs/ktl-7/access-export-procedure.md)
- [Brief KTL-8](openspec/KTL-8.md) y [candidatos KTL-8](docs/ktl-8/candidates.md)
- [Cambio OpenSpec](openspec/changes/ktl-5-dotnet-infrastructure)
- [Plan técnico existente](specs/001-gestion-cvs-rrhh/plan.md)
- [Principios vigentes](openspec/config.yaml)
