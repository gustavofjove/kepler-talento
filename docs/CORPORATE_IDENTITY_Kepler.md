# Identidad corporativa del ecosistema Kepler

## Proposito

Kepler es el ecosistema de aplicaciones operativas de JOVE Group para gestion de servicios,
activos, conocimiento, usuarios, seguridad y automatizaciones. Su identidad debe transmitir
control, claridad y fiabilidad: herramientas de trabajo diario para equipos que necesitan
resolver incidencias, consultar informacion y tomar decisiones sin friccion.

Esta guia define la base comun para aplicar el mismo estilo visual, tono y comportamiento en
KeplerDesk y en cualquier otra aplicacion del ecosistema Kepler.

## Arquitectura de marca

- **Marca corporativa:** JOVE Group.
- **Familia de producto:** Kepler.
- **Aplicacion actual:** KeplerDesk.
- **Patron de naming:** `Kepler` + descriptor funcional corto, por ejemplo `KeplerDesk`,
  `KeplerAssets`, `KeplerFlow`, `KeplerPortal`.
- **Relacion visual:** las aplicaciones Kepler usan el isotipo o logotipo de JOVE Group como
  sello corporativo y el nombre Kepler como identificador del producto.

## Personalidad

- **Profesional:** prioriza orden, trazabilidad y confianza.
- **Clara:** reduce el ruido visual y explica los estados sin ambiguedad.
- **Operativa:** favorece densidad util, escaneo rapido y acciones evidentes.
- **Serena:** evita dramatizar errores, alertas o estados criticos.
- **Escalable:** debe funcionar igual de bien en modulos pequenos y en entornos multiempresa.

## Principios de experiencia

- Mostrar primero la informacion que permite decidir o actuar.
- Mantener flujos predecibles: listado, filtro, detalle, accion, confirmacion.
- Usar componentes compactos y consistentes antes que composiciones decorativas.
- Reservar el color de acento para llamadas a la accion, estados activos y foco visual.
- Confirmar acciones destructivas y dejar rastro cuando afecten a datos o permisos.
- Evitar textos promocionales dentro de herramientas internas: la interfaz debe trabajar, no vender.

## Paleta corporativa

### Colores principales

| Uso                     | Token CSS            | Hex       | Nota                                                      |
| ----------------------- | -------------------- | --------- | --------------------------------------------------------- |
| Azul marino corporativo | `--fj-navy`          | `#162136` | Navegacion, titulos, acciones sobrias, fondos de sidebar. |
| Azul marino 90          | `--fj-navy-90`       | `#233149` | Hover y variaciones sobre fondos oscuros.                 |
| Azul marino 80          | `--fj-navy-80`       | `#33425c` | Texto secundario oscuro o apoyos.                         |
| Naranja JOVE            | `--fj-orange`        | `#ba541a` | Acento principal, CTA, estados activos.                   |
| Naranja oscuro          | `--fj-orange-dark`   | `#9a4415` | Hover de CTA y enfasis.                                   |
| Naranja brillante       | `--fj-orange-bright` | `#e0843f` | Indicadores activos sobre fondos oscuros.                 |
| Verde corporativo       | `--fj-green`         | `#476a30` | Exito, cumplimiento, estados positivos.                   |

### Colores neutrales

| Uso                 | Token CSS    | Hex       |
| ------------------- | ------------ | --------- |
| Texto principal     | `--fg-1`     | `#162136` |
| Texto secundario    | `--fg-2`     | `#3a4a60` |
| Texto atenuado      | `--fg-3`     | `#7a8699` |
| Fondo blanco        | `--bg-1`     | `#ffffff` |
| Fondo de superficie | `--bg-2`     | `#fafafa` |
| Fondo de aplicacion | `--bg-3`     | `#f4f4f2` |
| Borde suave         | `--border-1` | `#e5e7eb` |
| Borde fuerte        | `--border-2` | `#cdd3db` |

### Colores semanticos

| Estado      | Color     | Fondo     |
| ----------- | --------- | --------- |
| Exito       | `#476a30` | `#e8efe2` |
| Aviso       | `#d97706` | `#fef1de` |
| Error       | `#c8342b` | `#fbe5e3` |
| Informacion | `#2b6ea3` | `#e3eef7` |

## Tipografia

- **Titulos y UI de alto enfasis:** Montserrat, `--font-display`.
- **Texto de cuerpo y datos:** Nunito Sans, `--font-body`.
- **Fallback:** `system-ui, sans-serif`.
- **Tamanos base:** cuerpo de `14px`, tablas y controles de `13px`, labels auxiliares de
  `10px` a `11px`.
- **Jerarquia recomendada:**
  - H1: `24px`, peso `700`, Montserrat.
  - H2: `18px`, peso `700`, Montserrat.
  - H3: `14px`, peso `600`, Montserrat.
  - Cuerpo: `14px`, peso `400` a `500`, Nunito Sans.
- **Espaciado entre letras:** usar `0` en textos normales; solo usar mayusculas espaciadas para
  eyebrows, cabeceras de tabla y labels tecnicos.

## Forma, espaciado y elevacion

- Radios de borde:
  - Pequeno: `2px`.
  - Medio: `4px`.
  - Grande: `6px`.
  - Circular o avatar: `9999px`.
- El ecosistema Kepler evita esquinas muy redondeadas en superficies de trabajo.
- Las sombras deben ser sutiles y funcionales, no decorativas.
- Las paginas usan bandas o layouts sin marco; las tarjetas se reservan para elementos repetidos,
  KPIs, modales y paneles claramente delimitados.
- Espaciado base recomendado:
  - Pagina desktop: `24px` a `28px`.
  - Pagina mobile: `12px` a `14px`.
  - Separacion entre controles relacionados: `8px` a `12px`.
  - Padding de tarjetas: `14px` a `16px`.

## Componentes base

### Navegacion

- Sidebar en azul marino corporativo.
- Logo o isotipo JOVE junto al nombre de la app Kepler.
- Items compactos con icono, label y contador opcional.
- Item activo con fondo `--fj-navy-90`, texto naranja brillante e indicador lateral.

### Botones

- Primario: fondo naranja JOVE, texto blanco.
- Secundario oscuro: fondo azul marino, texto blanco.
- Fantasma: fondo blanco, borde suave, texto principal.
- Altura recomendada: `32px`.
- Tipografia: Montserrat, `13px`, peso `600`.
- Los iconos deben ayudar a reconocer la accion, especialmente en acciones repetidas.

### Formularios

- Inputs de `34px` de alto, fondo blanco y borde `--border-1`.
- Foco con borde azul marino o naranja solo cuando la accion este muy asociada a marca.
- Validacion inline, cercana al campo.
- Labels claros, sin microcopy excesivo.

### Tablas y listados

- Tablas densas, con cabeceras en Montserrat, mayusculas y texto atenuado.
- Hover de fila con `--bg-2`.
- Acciones por fila agrupadas y previsibles.
- Filtros visibles cuando cambian el resultado de forma relevante.

### Badges y estados

- Badges compactos de `22px` de alto.
- Usar color semantico solo para comunicar estado, prioridad, riesgo o categoria importante.
- Evitar mezclar mas de tres colores de estado en una misma zona sin necesidad real.

### KPIs

- Tarjetas simples con borde suave, fondo blanco y dato protagonista.
- Label en mayusculas pequenas.
- Valor en Montserrat, azul marino, peso alto.

## Voz y tono

- Directo, preciso y tranquilo.
- Usar verbos de accion: Crear, Guardar, Asignar, Resolver, Reabrir, Exportar.
- Evitar mensajes ambiguos como "Algo salio mal" cuando se pueda explicar la causa.
- En errores: indicar que ocurrio y que puede hacer el usuario.
- En exito: confirmar la accion sin ocupar mas atencion de la necesaria.
- En confirmaciones destructivas: nombrar el objeto afectado y la consecuencia.

Ejemplos:

- Correcto: "Ticket cerrado correctamente."
- Correcto: "No se pudo guardar la plantilla. Revisa los campos obligatorios."
- Correcto: "Eliminar esta categoria afectara a los tickets asociados."
- Evitar: "Operacion realizada con exito!!!"
- Evitar: "Oops, algo exploto."

## Iconografia y assets

- Asset principal actual: `src/assets/brand/jove-isotipo.svg`.
- Logo completo actual: `src/assets/brand/jove-logo.svg`.
- Los iconos de interfaz deben ser lineales, simples y consistentes.
- El isotipo JOVE puede usarse como marca de cabecera, favicon, pantalla de login y sello de
  producto.
- No deformar, recolorear ni encerrar el logotipo en fondos que reduzcan contraste.
- Mantener area de seguridad alrededor del isotipo equivalente al menos al 25% de su ancho.

## Aplicacion a nuevas apps Kepler

Cada nueva aplicacion del ecosistema debe partir de estos elementos:

```css
:root {
  --fj-navy: #162136;
  --fj-navy-90: #233149;
  --fj-navy-80: #33425c;
  --fj-orange: #ba541a;
  --fj-orange-dark: #9a4415;
  --fj-orange-bright: #e0843f;
  --fj-orange-soft: #f6dcc8;
  --fj-orange-pale: #fbeee3;
  --fj-green: #476a30;
  --fj-green-dark: #3a5727;
  --fj-green-bg: #e8efe2;
  --fg-1: #162136;
  --fg-2: #3a4a60;
  --fg-3: #7a8699;
  --bg-1: #ffffff;
  --bg-2: #fafafa;
  --bg-3: #f4f4f2;
  --border-1: #e5e7eb;
  --border-2: #cdd3db;
  --font-display: 'Montserrat', system-ui, sans-serif;
  --font-body: 'Nunito Sans', system-ui, sans-serif;
  --r-sm: 2px;
  --r-md: 4px;
  --r-lg: 6px;
  --r-pill: 9999px;
}
```

## Checklist de consistencia

- La app muestra la relacion JOVE Group + Kepler desde el primer nivel de navegacion.
- La sidebar o navegacion principal usa azul marino corporativo.
- El naranja se reserva para accion primaria, foco o estado activo.
- Los titulos usan Montserrat y el cuerpo usa Nunito Sans.
- Las tablas, filtros y formularios mantienen densidad operativa.
- Las tarjetas no se anidan dentro de otras tarjetas.
- Los estados usan colores semanticos de forma consistente.
- Los textos son concretos, sobrios y orientados a la accion.
- Los radios de borde no superan `6px` salvo avatares, pills o indicadores circulares.
- La interfaz mantiene contraste accesible y navegacion por teclado.

## Gobernanza

- Cualquier nueva aplicacion Kepler debe heredar estos tokens antes de definir variaciones locales.
- Las excepciones de marca deben documentarse en la guia UX/UI de la aplicacion concreta.
- Los cambios globales de identidad deben actualizar primero este documento y despues los tokens CSS.
- Los nombres historicos `--fj-yellow*` pueden mantenerse como alias tecnicos por compatibilidad,
  pero el nombre canonico del acento es `--fj-orange*`.
