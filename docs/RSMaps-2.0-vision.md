# RSMaps 2.0 — visión de producto y preguntas de analítica

## Idea central

RSMaps debe evolucionar de un mapa de propiedades a una plataforma inmobiliaria con cuatro funciones coordinadas:

1. **Inventario privado multiusuario** para asesores independientes e inmobiliarias.
2. **Marketplace público** donde cualquier persona pueda consultar y filtrar las propiedades que sus propietarios decidan publicar.
3. **Memoria histórica y analítica de mercado** basada en propiedades publicadas, retiradas, rentadas y vendidas.
4. **Base de datos y API del asistente inmobiliario**, para que canales como web, Facebook, Instagram y WhatsApp consulten inventario real y posteriormente gestionen prospectos y seguimiento.

La aplicación debe distinguir claramente entre:

- cuenta individual y cuenta inmobiliaria;
- propiedad publicada, pausada, retirada, vendida y rentada;
- precio publicado y precio de cierre;
- valor nominal histórico, valor actualizado por inflación y valor estimado por comportamiento inmobiliario;
- datos históricos confiables y datos legados que todavía requieren clasificación.

## Principio de datos históricos

Una propiedad que se vende o renta no debe desaparecer del sistema. Se convierte en un dato histórico de mercado.

Para cada operación deben conservarse, cuando estén disponibles:

- precio de publicación;
- precio de cierre;
- fecha de publicación;
- fecha de cierre;
- tipo de operación;
- ubicación;
- características del inmueble;
- asesor y cuenta responsables;
- historial de cambios de precio.

El precio histórico debe conservarse sin modificación. Los valores actualizados deben calcularse dinámicamente usando índices, por ejemplo:

**Valor actualizado = Precio histórico × (Índice actual / Índice en la fecha de operación)**

Se deben distinguir al menos tres lecturas:

- valor equivalente por inflación general (INPC);
- valor equivalente por evolución del mercado de vivienda (por ejemplo SHF cuando aplique);
- estimación local generada con datos propios de RSMaps cuando exista volumen suficiente de comparables.

RSMaps no debe presentar una estimación como avalúo profesional. Las estimaciones deberán incluir rango y nivel de confianza según cantidad, calidad, ubicación y antigüedad de los comparables.

## Preguntas que RSMaps deberá poder responder con el tiempo

### Mercado y apreciación

- ¿Qué colonias o zonas están aumentando más su valor nominal?
- ¿Qué colonias están aumentando más su valor real después de descontar inflación?
- ¿Qué zonas solamente siguen la inflación y cuáles muestran apreciación inmobiliaria adicional?
- ¿Qué tipos de propiedad se aprecian más en cada ciudad o zona?
- ¿Cómo cambia el precio por metro cuadrado por colonia, ciudad, estado y tipo de inmueble?
- ¿Cuál es la tendencia mensual, trimestral y anual de los precios?

### Venta y negociación

- ¿Cuál es el precio promedio y la mediana de cierre por zona y tipo de propiedad?
- ¿Cuál es la diferencia promedio entre precio publicado y precio de cierre?
- ¿Cuánto debe reducirse normalmente una propiedad antes de venderse?
- ¿Cuánto tarda una propiedad en venderse según precio, zona y características?
- ¿Las propiedades inicialmente sobrevaluadas permanecen más tiempo en el mercado?
- ¿Qué zonas tienen mayor rotación o absorción de inventario?

### Renta

- ¿Cuál es la renta promedio y mediana por zona y tipo de propiedad?
- ¿Cómo evolucionan las rentas frente al precio de venta?
- ¿Qué zonas tienen mejor relación renta/precio?
- ¿Qué tipo de inmueble genera mayor rendimiento bruto estimado por renta?
- ¿En qué zonas las rentas están creciendo más rápido?

### Oferta e inventario

- ¿Cuántas propiedades activas existen por ciudad, zona, tipo y rango de precio?
- ¿Qué segmentos tienen exceso o escasez de inventario?
- ¿Qué inmobiliarias o asesores tienen mayor inventario activo?
- ¿Qué tipos de inmueble se retiran sin venderse o rentarse con mayor frecuencia?

### Estimación de valor

- ¿Qué propiedades comparables existen dentro de un radio determinado?
- ¿Cuál es el valor estimado de una propiedad según ubicación, terreno, construcción, recámaras, baños, estacionamientos y tipo?
- ¿Cuál sería un rango razonable de valor y con qué nivel de confianza?
- ¿Cómo cambia la estimación si se usan valores nominales, valores ajustados por inflación o comparables actuales?

### Desempeño y seguimiento comercial

- ¿Qué asesores tienen mejor tasa de cierre?
- ¿Cuánto tiempo promedio tarda cada asesor o inmobiliaria en cerrar una operación?
- ¿Qué fuentes de prospectos generan más ventas o rentas?
- ¿Qué características solicitadas por los prospectos aparecen con mayor frecuencia?
- ¿Qué propiedades nuevas coinciden con prospectos ya registrados?

## Arquitectura conceptual

```text
RSMaps
├── Marketplace público
│   └── propiedades públicas de múltiples cuentas
├── Inventario privado
│   ├── cuenta individual
│   └── cuenta inmobiliaria
│       ├── administrador
│       ├── asesor
│       └── capturista
├── Datos históricos
│   ├── historial de precios
│   ├── operaciones de venta
│   ├── operaciones de renta
│   └── estados de publicación
├── Analítica
│   ├── precios y precio/m²
│   ├── tiempo en mercado
│   ├── negociación
│   ├── inflación e índices
│   └── comparables y estimaciones
└── API / asistente
    ├── búsqueda de propiedades
    ├── prospectos
    ├── seguimiento
    └── automatización multicanal
```

## Estado actual que debe preservarse durante la migración

La aplicación actual utiliza las tablas principales con prefijo `RSMAPS_` dentro del schema `dbo`. El rediseño debe ser incremental y evitar romper el mapa o perder las propiedades existentes.

Las tablas legadas o copias antiguas no deberán eliminarse hasta terminar la clasificación y la migración.

El histórico actual de `RSMAPS_InmuebleVendido` no puede asumirse automáticamente como ventas reales, porque el procedimiento legado de eliminación movía cualquier inmueble eliminado a esa tabla. Esos registros deberán considerarse **histórico legado por clasificar** antes de usarlos en estadísticas de cierre.

## Roadmap de referencia

1. Asegurar respaldo y diagnóstico del modelo actual.
2. Crear cuentas, membresías y roles sin alterar todavía el funcionamiento actual.
3. Migrar la inmobiliaria existente como primera cuenta grupal.
4. Implementar inventario privado por cuenta y asesor.
5. Mejorar captura de propiedades e imágenes.
6. Incorporar estados de publicación y dejar de tratar toda eliminación como venta.
7. Incorporar historial de precios y transacciones reales de venta/renta.
8. Construir marketplace público con filtros por cuenta, asesor y mercado general.
9. Crear API de propiedades y búsqueda.
10. Incorporar analítica, índices, comparables y estimaciones.
11. Conectar asistente inmobiliario y automatizaciones multicanal.
