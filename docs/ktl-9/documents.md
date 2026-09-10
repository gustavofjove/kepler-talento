# KTL-9: documentos privados de candidato

## Flujo

1. La SPA envía el archivo sin transformarlo en un formulario multipart.
2. La API autoriza `documents.upload`, limita el cuerpo, escribe con clave opaca en
   cuarentena, inspecciona tipo y contenido, guarda SHA-256 y crea una operación durable.
3. La respuesta `202` devuelve solo metadatos y estado `PendingScan`.
4. El worker analiza el objeto. Únicamente `Clean` lo promociona al área disponible.
5. La SPA consulta el estado con retroceso acotado y habilita la descarga solo al quedar
   `Available`.

El contenido máximo es 20 MiB. El proxy admite 21 MiB para el framing multipart. Se aceptan
PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF y BMP cuando extensión y contenido coinciden.

## Contrato HTTP

| Método   | Ruta                                                   | Capacidad            | Resultado                          |
| -------- | ------------------------------------------------------ | -------------------- | ---------------------------------- |
| `POST`   | `/api/candidates/{candidateId}/documents`              | `documents.upload`   | `202` con metadatos y estado       |
| `GET`    | `/api/candidates/{candidateId}/documents`              | `candidates.read`    | colección de metadatos y estados   |
| `GET`    | `/api/candidates/{candidateId}/documents/{id}`         | `candidates.read`    | metadatos y estado para sondeo     |
| `PUT`    | `/api/candidates/{candidateId}/documents/{id}/primary` | `documents.upload`   | documento designado principal      |
| `GET`    | `/api/candidates/{candidateId}/documents/{id}/content` | `documents.download` | stream adjunto solo si está limpio |
| `DELETE` | `/api/candidates/{candidateId}/documents/{id}`         | `documents.upload`   | `204`; elimina fila y objeto       |

El multipart usa los campos `file`, `documentType` e `isPrimary`. Ninguna respuesta incluye
`StorageKey`, raíz, ruta física, firma del escáner o bytes. La descarga usa
`Cache-Control: private, no-store`, `X-Content-Type-Options: nosniff`, nombre saneado en
`Content-Disposition: attachment` y no admite rangos.

## Estados para la interfaz

| Estado API          | Texto                                               |
| ------------------- | --------------------------------------------------- |
| `Pending`           | `En análisis`                                       |
| `Available`         | `Disponible`                                        |
| `Refused`           | `No disponible` y explicación no técnica            |
| `Error`             | `Error de análisis` y opción de reintento posterior |
| `LegacyUnavailable` | `Documento heredado sin archivo asociado.`          |

Eliminar el principal deja al candidato sin principal. El usuario debe elegir otro de forma
explícita. Los eventos `document.upload.accepted`, `document.scan`,
`document.primary.changed`, `document.downloaded` y `document.removed` no contienen nombre,
contenido ni ubicación del archivo.
