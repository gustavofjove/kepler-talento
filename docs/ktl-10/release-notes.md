# Nota de versión KTL-10

La búsqueda avanzada ahora se resuelve en el servidor. El navegador ya no descarga los
candidatos para filtrarlos: envía los filtros, y PostgreSQL devuelve una página de resultados
junto con el total de coincidencias.

## Qué cambia para quien usa la aplicación

- **Resultados por páginas.** Bajo la tabla aparece el número total de candidatos encontrados
  y los botones **Anterior** y **Siguiente**. El total corresponde a toda la búsqueda, no solo
  a las filas visibles.
- **La exportación CSV exporta la página mostrada**, no el conjunto completo de coincidencias.
  El mensaje de confirmación lo indica.
- **Los filtros se comportan igual que antes.** Mismos campos, mismos modos ANY y ALL, misma
  combinación entre familias y ningún candidato duplicado. Un criterio sin nivel sigue
  coincidiendo con cualquier nivel.
- **Búsquedas más rápidas al cambiar de filtro.** Si se lanza una búsqueda nueva mientras otra
  está en curso, la anterior se cancela y sus resultados nunca llegan a la pantalla.

## Búsquedas guardadas: hay que volver a crearlas

> **Los presets guardados anteriormente en el navegador no se conservan.** Es necesario
> volver a crearlos.

Las búsquedas guardadas pasan a almacenarse en el servidor, asociadas a la persona que las
crea. Los presets antiguos vivían únicamente en el navegador y no tienen forma fiable de saber
a quién pertenecían: asignarlos automáticamente a quien abriera después un equipo compartido
podría revelar los términos de búsqueda de una persona o entregar sus búsquedas guardadas a
otra. Por eso no se migran.

Los datos antiguos que puedan quedar en el navegador **se ignoran**: no se leen, no se suben y
tampoco se borran.

Lo que se gana a cambio:

- Cada búsqueda guardada es **privada**: solo la ve quien la creó.
- Está disponible **desde cualquier equipo o navegador**, no solo desde donde se creó.
- El nombre es **único por persona**, sin distinguir mayúsculas de minúsculas. Si ya existe una
  búsqueda con ese nombre, la aplicación avisa en lugar de sobrescribirla en silencio.
- Se puede **renombrar y actualizar**: al guardar con un preset seleccionado se sustituyen su
  nombre y sus filtros.
- La aplicación registra **cuándo se usó por última vez** cada búsqueda guardada.

## Lo que sigue siendo local

Los **últimos filtros utilizados** se siguen recordando en el navegador, como comodidad para
volver a la misma pantalla en el mismo equipo. No se envían al servidor, no son una búsqueda
guardada y no se comparten entre equipos: al abrir la búsqueda en otro navegador, los filtros
aparecen vacíos aunque las búsquedas guardadas sí estén disponibles.

## Privacidad

Los términos de búsqueda, los filtros y los nombres de las búsquedas guardadas no se registran
en ningún log. Los resultados muestran únicamente nombre, apellidos, teléfono, correo, estado,
si hay CV principal y la fecha de actualización; no incluyen notas, datos de consentimiento ni
ninguna referencia a la ubicación de los archivos.

Un candidato aparece como «con CV» cuando tiene un CV principal registrado, esté o no
disponible para descarga. Poder descargarlo sigue dependiendo del análisis de seguridad y de
los permisos, exactamente como en KTL-9.
