# Kepler Talento

Aplicación interna de RR. HH. para gestionar candidatos y CVs. La interfaz actual es una
SPA React; KTL-5 añade la plataforma objetivo ASP.NET Core, PostgreSQL, almacenamiento
privado de documentos, ClamAV y Nginx. Las funcionalidades heredadas que todavía usan
`localStorage` se mantienen hasta que cada vertical se migre explícitamente; identidad,
usuarios y roles ya pertenecen a la API.

## Plataforma KTL-5

- React 19, TypeScript 5.9 y Vite 7.
- .NET SDK 10.0.102 y ASP.NET Core 10.
- FastEndpoints, MediatR, FluentValidation, EF Core/Npgsql y Serilog.
- PostgreSQL 17.6.
- almacenamiento de ficheros privado con cuarentena y límite de 20 MB.
- ClamAV 1.4.3 en una red Docker privada.
- Nginx sin privilegios como único punto publicado, en `http://localhost:4200`.

La autenticación de producción usa tokens de Microsoft Entra ID. El actor sintético y el
emisor local solo existen en Development/Testing; el proceso se niega a arrancar si se
intenta habilitarlos en Production.

## Identidad y acceso KTL-16

Con `docker compose up --build`, ejecuta `npm start`, abre `http://localhost:4300/login` y
pulsa **Iniciar sesión**. En `APP_ENV=local`, la SPA obtiene un token firmado de
`POST /api/dev/token` para `admin@kepler-talento.local`; el backend lo valida por el mismo
pipeline JWT que las peticiones normales. El token no se guarda en `localStorage`.

En producción se configuran `ENTRA_CLIENT_ID`, `ENTRA_AUTHORITY` y `ENTRA_API_SCOPE`. Los
permisos efectivos se leen de PostgreSQL en cada petición y usan una sola nomenclatura
`<resource>.<action>`, por ejemplo `candidates.read`, `users.manage` y `roles.manage`.
Consulta el [contrato de autenticación y autorización](docs/ktl-16/authentication-and-authorization.md)
y el [runbook de despliegue y recuperación](docs/ktl-16/runbook.md).

## Catálogos KTL-6

Los catálogos de negocio ya no viven en `localStorage`: son propiedad de la API y se sirven
desde PostgreSQL (`CAT_CatalogItems`). Las capacidades `catalogs.read` y `catalogs.manage`
se aplican en el servidor y se corresponden con el permiso `manage_catalogs` del frontend;
ocultar la interfaz no es el control.

No existe ningún endpoint de borrado físico: un valor se retira desactivándolo, de modo que
los candidatos que lo referencian conservan su significado. Las diez familias se cargan con
una semilla explícita de despliegue durante `--migrate`. La clave `rrhh-catalogs` del
navegador queda abandonada; no se migra.

La familia **Etiquetas** incluye inicialmente `Recontratable`, `No contactar` y
`Referido por plantilla`. `No contactar` es una etiqueta informativa: la aplicación no aplica
por sí sola una restricción de contacto.

El detalle de rutas, códigos de error, unicidad de nombres, concurrencia y auditoría está en
[`docs/ktl-6/catalogs.md`](docs/ktl-6/catalogs.md).

## Etiquetas y notas personalizadas KTL-21

La ficha del candidato permite asignar etiquetas del catálogo y mantener un hilo independiente
de notas personalizadas. Cada nota conserva autor y fecha, usa su propia versión para evitar
sobrescrituras y se retira de forma lógica; no existe borrado físico. El campo histórico `notes`
del candidato no cambia. Las notas sin un usuario interno resoluble muestran
`Autor desconocido`. Consulta el [contrato KTL-21](docs/ktl-21/candidate-tags-and-notes.md).

## Migración de Access KTL-7

El esquema de candidatos (`CND_Candidates` y sus tablas de relación de idiomas, programas,
formación, experiencia y habilidades) se entrega en este slice, junto con la herramienta que
lo rellena desde la base de datos Access heredada.

`backend/Tools/DataMigration`, compilada como `ktl-migrate`, es una **herramienta de línea de
comandos que ejecuta el operador**. Deliberadamente no es accesible por HTTP y ningún proyecto
de la API la referencia: mover todo el conjunto de datos de candidatos no es una superficie de
petición que el producto deba tener. **No** es la pantalla de administración «Importación»
(KTL-17), que sube un CSV pequeño y plano a la API con su propio permiso, su propio límite de
filas y análisis antivirus. Ambas comparten la misma semántica de filas y de resolución de
catálogos, definida una sola vez en `Application/Import/Rows`.

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

Las capacidades `documents.upload` y `documents.download` usan el mismo vocabulario en el
frontend y el backend. Ambas se
comprueban por petición y son independientes de `candidates.read`. El límite de contenido es
20 MiB para PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF y BMP; Nginx admite 21 MiB únicamente
para incluir el margen acotado del framing multipart.

Consulta el [contrato y flujo de documentos KTL-9](docs/ktl-9/documents.md) y la
[nota de versión](docs/ktl-9/release-notes.md).

La página de detalle incluye una vista previa del CV principal cuando es un PDF disponible y la
persona tiene `documents.download`. Otros formatos conservan la descarga. La vista previa reutiliza
el endpoint privado de contenido y no expone rutas ni enlaces permanentes; consulte la
[documentación de KTL-20](docs/ktl-20/document-preview.md).

## Presets de búsqueda KTL-14

Las búsquedas guardadas son una biblioteca compartida en PostgreSQL (`ADM_SearchPresets`). Se
gestionan en **Admin › Presets** (`/app/admin/presets`) con el permiso `manage_presets` del
frontend, que corresponde a la capacidad `presets.manage` de la API; ver y aplicar presets desde
**Búsqueda** sigue requiriendo `view_candidates` (`candidates.read`). Ambas se aplican en el
servidor; ocultar la interfaz no es el control.

La migración `ShareSearchPresets` elimina las búsquedas guardadas privadas de KTL-10, que hay que
volver a crear. Los presets se borran físicamente, con confirmación y contra la versión leída;
la regla de no borrar físicamente sigue aplicándose a candidatos y catálogos. Búsqueda y los
presets comparten un único formulario de criterios y un único resumen de solo lectura.

El detalle de rutas, códigos de error, concurrencia, modelo de datos y rollback está en
[`docs/ktl-14/presets.md`](docs/ktl-14/presets.md), y la nota para usuarios en
[`docs/ktl-14/release-notes.md`](docs/ktl-14/release-notes.md).

## Listado de candidatos paginado en servidor KTL-18

El listado de **Candidatos** ya no descarga la tabla completa: pide una sola página a
`POST /api/candidates/search`, y PostgreSQL filtra, ordena y pagina. Ordenar y filtrar se aplica
a todos los candidatos que coinciden, no solo a los que tenía el navegador. La página muestra 25
por defecto (25, 50 o 100), el texto también busca en las notas y la selección solo abarca la
página visible.

La página, la ordenación y los filtros de estado, CV e inactivos viajan en la URL, así que un
enlace copiado reabre la misma vista; el texto buscado nunca se escribe en la URL. «Incluir
inactivos» requiere `candidates.delete`. El dashboard muestra solo recuentos que el servidor puede
calcular; desaparecen «Pendientes de revisión» y «Recibidos este mes».

Consulta el [contrato del listado](docs/ktl-18/list-contract.md) y la
[nota de versión](docs/ktl-18/release-notes.md).

## Importación de candidatos KTL-17

**Admin › Importación** (`/app/admin/import`) crea candidatos reales desde un CSV. Antes la
pantalla analizaba el archivo en el navegador, guardaba un historial en `localStorage` y **no
creaba ningún candidato**; ahora todo ocurre en la API y en dos pasos:

1. **Subir y validar.** El archivo se guarda con una clave opaca en cuarentena, ClamAV lo
   analiza y solo un resultado `Clean` permite leerlo. La validación es una simulación: no
   crea, cambia ni desactiva candidatos, y devuelve un informe por fila con número de fila,
   columna y código de motivo, nunca con valores.
2. **Confirmar carga.** Solo un lote validado y sin filas rechazadas se puede confirmar. La
   carga es una operación duradera: si la API se reinicia a mitad, se reanuda sin duplicar
   candidatos. Las personas que ya existen (mismo correo) se omiten, no se rechazan.

Todo el recorrido exige `candidates.import`, que tiene `rrhh_admin`; `candidates.create` no
basta. Para probarlo en local con el stack completo:

```powershell
docker compose up --build        # aplica la migración AddImportBatches con el migrator
npm start                        # http://localhost:4300/app/admin/import
```

Un archivo mínimo válido:

```csv
first_name,last_name,email,status,languages
Ana,Ruiz,ana.ruiz@example.test,available,Inglés:B2
```

Los archivos subidos se purgan 30 días después de cerrarse el lote (`Import:RetentionDays`);
los recuentos y el informe por fila se conservan. La purga también se puede lanzar a mano con
`dotnet backend/Web/bin/Debug/net10.0/Web.dll --purge-imports`. La clave del navegador
`rrhh.import.batches.v1` se elimina en el primer arranque y no se migra, porque describía
cargas que nunca ocurrieron.

Consulta el [contrato del archivo](docs/ktl-17/import-file-contract.md), los
[códigos de motivo](docs/ktl-17/row-reason-codes.md), el [runbook](docs/ktl-17/runbook.md) y la
[nota de versión](docs/ktl-17/release-notes.md).

## Auditoría KTL-19

Cada evento de auditoría registra el **identificador interno del usuario** que lo causó (o el
actor «sistema» para el antivirus y la conciliación de documentos). Nunca guarda correos, nombres
ni el identificador del proveedor de identidad. Además de los cambios, ahora se auditan las dos
lecturas que identifican a una persona: abrir la ficha de un candidato y descargar un documento.
Las búsquedas y los listados no se auditan a propósito.

`ktl_runtime` ya no puede modificar ni borrar eventos: el registro es de solo inserción para la
aplicación. **Admin › Auditoría** (`/app/admin/audit`) lista el registro con filtros por fechas,
tipo de evento, actor y sujeto; exige `audit.read`, que solo tiene `system_admin`. Los eventos
anteriores a KTL-19 aparecen como «Actor desconocido».

El actor sintético de desarrollo no tiene usuario almacenado, así que las operaciones auditadas lo
rechazan salvo que `DevelopmentActor:UserId` indique uno. El stack de Compose no lo usa.

Consulta el [contrato de auditoría](docs/ktl-19/audit-contract.md) y el
[runbook](docs/ktl-19/runbook.md).

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
dotnet run --project backend/Web/Web.csproj -- --migrate
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

## Textos de interfaz (i18n)

Los textos visibles se sirven con i18next desde `src/assets/i18n/es.json`. El español es el
único idioma activo y no hay selector de idioma; `en.json` se mantiene pero no se carga.

- Para añadir un texto, crea una clave con el esquema `funcionalidad.seccion.elemento` en
  `es.json` (con tildes correctas) y úsala con `const { t } = useTranslation()` y
  `t('clave')`. Añadir el valor en `en.json` es opcional.
- Las validaciones nuevas lanzan `TranslatableError(clave, valores)` y el componente las
  muestra con `errorText(err, t)`.
- `npm run lint` falla si un `.tsx` contiene texto JSX literal, salvo los archivos de la
  lista `LEGACY_HARDCODED_COPY` de `eslint.config.js`. Esa lista solo puede reducirse: al
  cambiar los textos de uno de esos archivos, se migran a claves y se quita de la lista.
- En tests, una clave inexistente hace fallar la ejecución.

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
- [Cambio OpenSpec KTL-5](openspec/changes/archive/2026-08-25-ktl-5-dotnet-infrastructure)
- [Plan técnico existente](specs/001-gestion-cvs-rrhh/plan.md)
- [Principios vigentes](openspec/config.yaml)
