# KTL-15 release notes

## Cambios visibles

- Se incorpora la sección **Posiciones**, con listado paginado, alta, detalle y edición.
- Las posiciones pueden cerrarse y reabrirse sin perder sus requisitos.
- La descripción admite párrafos, negrita, cursiva y listas; el servidor elimina cualquier HTML
  no permitido.
- Los requisitos reutilizan los criterios de búsqueda de candidatos. Un preset se copia al
  borrador y no queda vinculado, por lo que cambios posteriores en el preset no modifican la
  posición.
- Los candidatos compatibles se consultan en tiempo real solo si la persona tiene permiso para
  leer candidatos.

La migración debe ejecutarse antes de desplegar la nueva aplicación. Añade los permisos
`positions.read` y `positions.manage` a los roles de sistema definidos, sin modificar roles
personalizados. No existe borrado de posiciones ni se guardan asociaciones o snapshots de
candidatos.
