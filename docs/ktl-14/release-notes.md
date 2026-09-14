# Nota de versión KTL-14

Las búsquedas guardadas pasan a ser una **biblioteca compartida**. La mantiene el equipo de
administración desde la nueva sección **Admin › Presets**, y cualquier persona con acceso a la
búsqueda puede aplicarlas desde **Búsqueda**.

## Búsquedas guardadas: se reinician

> **Las búsquedas guardadas creadas hasta ahora se eliminan con esta versión.** Si alguna
> debe seguir disponible, una persona con el permiso `manage_presets` tiene que volver a
> crearla en Admin › Presets.

Hasta ahora cada búsqueda guardada era privada de quien la creó. Convertirlas automáticamente en
búsquedas compartidas podría mostrar a todo el equipo términos de búsqueda que su autor nunca
quiso compartir, y además varias personas podían tener búsquedas con el mismo nombre. Por eso no
se migran.

## Qué cambia para quien usa la aplicación

- **En Búsqueda solo se aplican presets.** El selector «Preset guardado» sigue igual: al elegir
  uno se cargan sus filtros y se lanza la búsqueda. Desaparecen el campo «Nombre para guardar»
  y los botones «Guardar actual» y «Eliminar».
- **Todas las personas ven los mismos presets.** Un preset creado por administración aparece
  para cualquiera que tenga acceso a la búsqueda.
- **Gestionar presets.** Quien tenga el permiso `manage_presets` ve el enlace «Gestionar
  presets» en Búsqueda y la entrada **Presets** dentro del menú **Admin**.

## Admin › Presets

- **Listado** con nombre, resumen de criterios, última actualización y último uso («Nunca» si
  no se ha aplicado), con filtro por nombre, ordenación y paginación.
- **Ver los criterios sin salir del listado.** El icono del ojo junto al nombre abre una ventana
  con el resumen de criterios, las fechas y el acceso a editar.
- **Crear, editar y eliminar** presets. El formulario de criterios es exactamente el mismo que el
  de Búsqueda, y el resumen de solo lectura también.
- **Eliminar pide confirmación** y la eliminación es definitiva.
- **Nombres únicos** sin distinguir mayúsculas ni tildes: «Inglés B2» e «ingles b2» son el mismo
  nombre, y la aplicación avisa en lugar de crear un duplicado.
- **Cambios simultáneos.** Si otra persona modifica un preset mientras lo editas, al guardar se
  muestra un aviso para recargar en lugar de sobrescribir su cambio. Aplicar un preset desde
  Búsqueda no cuenta como modificación.

## Permisos

- Nuevo permiso **`manage_presets`**: crear, editar y eliminar presets. Lo incluyen por defecto
  los roles `rrhh_admin` y `system_admin`.
- Ver y aplicar presets sigue requiriendo **`view_candidates`**.
- Los permisos se comprueban en el servidor; ocultar el menú o el enlace no es el control.

## Privacidad

Los presets son visibles para todas las personas con acceso a la búsqueda. **No incluyas datos
personales** en el nombre ni en el texto libre de un preset; el formulario lo recuerda. Los
nombres y filtros de los presets no se registran en ningún log, y la aplicación no guarda quién
creó o usó cada preset.
