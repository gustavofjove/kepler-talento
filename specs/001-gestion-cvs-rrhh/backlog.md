# Product Backlog: Continuacion De Desarrollo (2026-07-01)

## Objetivo

Cerrar brechas de usabilidad, operacion y produccion detectadas durante la implementacion inicial del MVP.

## Priorizacion

- `P0`: bloquea operacion segura o continuidad de datos.
- `P1`: mejora fuerte de productividad de RRHH en uso diario.
- `P2`: mejora de calidad, gobernanza y adopcion.

## EPIC A - Candidate Operations Center (P1)

### A1. Listado de candidatos operativo

- Agregar busqueda rapida por texto en listado.
- Agregar filtros por estado, CV disponible y activo/inactivo.
- Agregar ordenacion por columnas clave (nombre, estado, actualizado).
- Agregar paginacion y contador total.

Criterio de aceptacion:

- RRHH puede encontrar un candidato en menos de 30 segundos en dataset de validacion.

### A2. Acciones masivas y seguridad UX

- Permitir seleccion multiple para baja logica masiva.
- Requerir confirmacion explicita para acciones destructivas.
- Mostrar resumen de cambios aplicados y errores por fila.

Criterio de aceptacion:

- Ninguna accion destructiva se ejecuta sin confirmacion explicita.

## EPIC B - Search Productivity (P1)

### B1. Busquedas guardadas

- Guardar filtros frecuentes por usuario.
- Cargar la ultima busqueda al entrar en pantalla.
- Permitir renombrar y eliminar busquedas guardadas.

### B2. Resultados enriquecidos

- Mostrar contador total, pagina actual y tiempo de respuesta.
- Mostrar chips de filtros activos con accion de quitar rapido.
- Mantener accion de abrir CV seguro desde resultados.

Criterio de aceptacion:

- Usuario puede ejecutar una busqueda recurrente en 1 clic.

## EPIC C - Import/Export Operations (P0)

### C1. Importacion por lotes completa

- Flujo `dry-run -> commit` con idempotencia.
- Historial de lotes con estado (`validated`, `loaded`, `failed`).
- Descarga de errores de importacion en CSV.

### C2. Exportacion robusta

- Export asincrona para lotes grandes.
- Historial de exportaciones y caducidad de descargas.
- Restriccion de tamano/filas por configuracion de entorno.

Criterio de aceptacion:

- RRHH/Admin puede auditar que se importo/exporto, cuando, por quien y con que resultado.

## EPIC D - Security And Governance Hardening (P0)

### D1. Cobertura de seguridad faltante

- Completar pruebas SQL de RLS auth y storage.
- Completar pruebas de integracion de Edge Functions.
- Completar E2E de documentos, export e import.

### D2. Error catalog y observabilidad

- Catalogo de errores estables para frontend y functions.
- `request_id` en respuestas y logs estructurados.
- Correlacion de auditoria para operaciones sensibles.

Criterio de aceptacion:

- 100% de casos de acceso no autorizado fallan en modo cerrado y con errores controlados.

## EPIC E - Admin UX And Controls (P1)

### E1. Gestion de usuarios y roles productiva

- Invitar usuario (no solo crear local).
- Reenvio de invitacion y reset MFA administrado.
- Historial de cambios de rol y activacion.

### E2. Restricciones de integridad

- Bloquear eliminacion de catalogos en uso (ya implementado en local; llevar a backend).
- Bloquear downgrade de ultimo admin activo (ya implementado en local; llevar a backend).

Criterio de aceptacion:

- Admin puede operar usuarios/roles sin riesgo de bloqueo operativo.

## EPIC F - Production Release Readiness (P0)

### F1. Runbook y rollback

- Runbook de incidentes, backup/restore y rotacion de secretos.
- Validacion de restore en staging.
- Checklist de salida a produccion firmado.

### F2. Calidad no funcional

- Smoke tests automatizados de staging.
- Validacion de objetivos de rendimiento de busqueda/listado.
- Validacion de accesibilidad minima en pantallas principales.

Criterio de aceptacion:

- Entrega aprobada por RRHH y responsable tecnico con evidencia reproducible.

## Dependencias principales

1. `C` depende de contratos Edge Functions y migraciones auditables.
2. `D` depende de contratos y scripts de seguridad cerrados.
3. `A` y `B` pueden avanzar en paralelo tras modelo de busqueda estable.
4. `F` depende de cierre de `C` y `D`.

## Orden sugerido de ejecucion

1. EPIC C
2. EPIC D
3. EPIC A
4. EPIC B
5. EPIC E
6. EPIC F
