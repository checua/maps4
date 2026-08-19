# RSMaps — búsqueda y analítica por área geográfica

## Idea central

El mapa es el núcleo de RSMaps. La selección de un área geográfica debe convertirse en una capacidad reutilizable para búsqueda, comparación y analítica.

El usuario podrá definir una zona mediante:

- polígono dibujado o editable;
- radio alrededor de un punto;
- zona visible actual del mapa;
- colonia o zona administrativa;
- cercanía a una ubicación;
- en una fase posterior, tiempo estimado de traslado.

La consulta conceptual es:

```text
Área seleccionada
      ↓
propiedades cuyo punto geográfico cae dentro del área
      ↓
filtros de precio, tipo, recámaras y amenidades
      ↓
coincidencias reales
```

## Usuario no registrado

Debe poder usar la selección geográfica como parte natural del marketplace, sin exigir registro antes de obtener valor.

Capacidades objetivo:

- dibujar o ajustar una zona;
- buscar propiedades dentro de ella;
- combinar el área con precio, tipo y características;
- ver cantidad de coincidencias;
- recibir sugerencias para ampliar/reducir la zona o flexibilizar filtros;
- consultar métricas públicas básicas cuando exista suficiente información y no se comprometan datos privados.

La filosofía es que el registro no sea una barrera para buscar.

## Usuario registrado

Además de las capacidades públicas, podrá acceder a funciones persistentes y profesionales:

- guardar áreas con nombre;
- guardar filtros asociados al área;
- asociar una zona a un prospecto: “Tengo un cliente buscando dentro de este polígono”;
- recibir alertas cuando una nueva propiedad cae dentro del área y cumple criterios;
- analizar inventario de su cuenta dentro de la zona;
- consultar comparables y estadísticas de mercado;
- comparar la misma zona entre periodos;
- guardar múltiples zonas por cliente;
- compartir internamente una zona con otros asesores autorizados.

## Pulso de zona

Cuando exista calidad y volumen de datos suficiente, una zona seleccionada podrá producir un resumen como:

```text
Propiedades activas
Precio promedio
Precio mediano
Rango de precios
Precio/m² de terreno
Precio/m² de construcción
Tiempo mediano en mercado
Descuento publicación/cierre
Ventas o rentas registradas
Tendencia del periodo
```

La mediana debe mostrarse junto al promedio para reducir distorsión por valores extremos.

## Precio por superficie

No debe mezclarse indiscriminadamente el precio/m² entre tipos de inmueble.

- terreno: precio / m² de terreno;
- departamento: precio / m² de construcción;
- casa: precio / m² de construcción, conservando terreno como variable adicional;
- local/oficina: precio / m² de construcción;
- rancho: considerar precio / hectárea además de otras métricas.

Las métricas deben indicar cantidad de comparables y nivel de confianza cuando evolucionen a estimaciones.

## Privacidad y niveles de información

El polígono puede funcionar para usuarios registrados y no registrados, pero el contenido mostrado dependerá de permisos y contexto.

```text
Visitante
→ inventario público + analítica pública agregada

Usuario registrado
→ lo anterior + zonas guardadas + prospectos + alertas

Asesor / cuenta
→ inventario privado autorizado + analítica de su operación

Analítica de mercado
→ únicamente datos agregados y clasificados como confiables
```

Nunca se deben exponer datos privados individuales mediante una estadística geográfica.

## Arquitectura objetivo

Las propiedades deben poder representarse como puntos geográficos y las búsquedas como geometrías reutilizables por backend/API.

```text
Web       ─┐
Android   ─┼→ Área/polígono/radio → API geoespacial → resultados
IOS       ─┘
```

La lógica espacial no debe quedar atrapada en JavaScript de Google Maps. El cliente dibuja/selecciona; el backend decide qué puntos pertenecen al área y aplica permisos/filtros.

SQL Server permite evolucionar a tipos `geography`/`geometry` para consultas de contención, radio y distancia. La migración debe ser incremental porque actualmente RSMaps conserva latitud y longitud separadas.

## UX

No consultar la base continuamente mientras el usuario arrastra cada vértice. Aplicar debounce o ejecutar la consulta al terminar la interacción.

Experiencia objetivo:

```text
[ Seleccionar zona ]
      ↓
usuario dibuja/ajusta
      ↓
“Analizando zona…”
      ↓
“12 coincidencias exactas”
      ↓
[ Ver propiedades ] [ Aplicar filtros ] [ Guardar zona ]
```

Para visitantes, “Guardar zona” puede convertirse en una invitación contextual a crear cuenta; buscar nunca debe requerir registro obligatorio.

## Relación con prospectos

Una zona guardada debe poder formar parte de la necesidad de un cliente junto con presupuesto y características.

```text
Prospecto
├── una o varias áreas
├── presupuesto
├── tipo de inmueble
├── características
└── prioridades
        ↓
Match automático contra inventario nuevo/existente
```

Esta capacidad es prioritaria para el futuro asistente y las notificaciones.
