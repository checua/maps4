# Radar — Contexto canónico del proyecto

## Propósito

Radar es el proyecto unificado para el desarrollo inmobiliario antes repartido entre Promoinmueble, RSMaps y conversaciones del Asesor Personal.

La idea central es detectar oportunidades inmobiliarias, entender solicitudes reales, compararlas contra inventario y presentar pocas coincidencias útiles, reduciendo ruido y pasos manuales.

## Nombre y alcance

- Nombre de producto/proyecto: **Radar**.
- **RSMaps** se conserva como núcleo de inventario, mapas, propiedades, usuarios, cuentas y zonas.
- **RSMaps.Radar.Listener** es el listener actual de WhatsApp Web.
- Promoinmueble queda como proyecto histórico y dejará de usarse; cualquier idea útil debe migrarse aquí antes de eliminarlo.

## Arquitectura conceptual

### RSMaps Core
- Inventario.
- Mapa.
- Zonas.
- Usuarios y cuentas.
- Propiedades.
- Publicación y visibilidad.

### Radar Listener
- WhatsApp Web mediante Playwright.
- Chats configurados.
- Detección de mensajes nuevos.
- Captura de origen, autor, teléfono, hora e ID.
- Envío de alertas al chat **Propiedades**.

### Radar Intelligence
- Clasificación: demanda / oferta / ruido.
- Segmentación: un mensaje puede contener múltiples solicitudes.
- Extracción estructurada.
- Deduplicación entre grupos.
- Matching contra inventario RSMaps.
- Uso de zonas y posteriormente proximidad geográfica.

### Radar Apps
- Web.
- Preparación futura para Android e iOS.

## Modelo normalizado de solicitud

Conservar, cuando existan:

- Operación: Renta / Venta.
- Tipo de propiedad.
- Zona o zonas.
- Precio mínimo y máximo.
- Recámaras.
- Baños.
- Terreno mínimo.
- Construcción mínima.
- Mascotas.
- Amueblado.
- Una planta.
- Vigilancia / acceso controlado.
- Cochera mínima.
- Forma de pago o crédito: contado, efectivo, Infonavit, Infonavit Total, FOVISSSTE, Banjercito, bancario u otro.
- Mensaje original.
- Chat origen.
- Autor.
- Teléfono.
- Fecha/hora.
- ID único del mensaje.

## Flujo operativo objetivo

1. Capturar mensaje nuevo.
2. Limpiar el mensaje y separar contenido citado/respuesta cuando aplique.
3. Determinar si realmente es inmobiliario.
4. Si contiene varias solicitudes, segmentarlas.
5. Extraer campos de cada solicitud.
6. Validar coherencia y confianza.
7. Deduplicar publicaciones repetidas entre grupos.
8. Consultar RSMaps.
9. Comparar primero con operación, tipo, zonas, precio y requisitos duros.
10. Clasificar resultado como coincidencia alta, media, aproximada o datos insuficientes.
11. Decidir si vale la pena notificar.
12. Enviar a **Propiedades** y volver al chat origen.

## Principios de matching

- La existencia de propiedades parecidas nunca debe convertir un mensaje no inmobiliario en inmobiliario.
- Clasificación de intención y matching son problemas distintos.
- Una zona explícita debe pesar fuertemente.
- Datos faltantes no deben premiarse como si fueran coincidencias.
- Cuando la zona no pueda confirmarse, debe mostrarse como **no verificable**, no como una coincidencia alta.
- Deben mostrarse coincidencias y diferencias relevantes.
- Usar las zonas estructuradas de RSMaps antes de depender sólo de texto libre en dirección u observaciones.
- Lat/Lng debe quedar disponible para proximidad geográfica y zonas cercanas.

## Zonas — capacidad que debe protegerse

El trabajo de Zonas forma parte de Radar y no es un proyecto separado.

En master existen actualmente capacidades como:

- ZonaPrincipalCodigo.
- ZonaPrincipalNombre.
- ZonasCsv.
- TieneZona.
- InventarioZonasController.
- ZonaAdminController.
- Repositorio y administración de zonas.
- SQL de fundamento espacial, clasificación automática y alias.
- Integración de zonas en la búsqueda del inventario.

La integración de Radar debe conservar íntegramente esta capacidad y conectar el matching con ella.

## Casos reales de regresión recopilados

### Falso positivo de WhatsApp
Mensaje sobre búsqueda exacta de palabras en WhatsApp.

Esperado: **Otro/Ruido**. No debe extraer `Todos Los Chats` como zona ni consultar RSMaps.

### Solicitud simple válida
Casa en renta en Jardines de Durango, 3 recámaras, 2 baños, mascotas, máximo $15,000.

Esperado: demanda inmobiliaria válida y matching.

### Referencia geográfica
Casa en renta al sur, cerca de Clínica 49, 3 recámaras, $10,000.

Esperado:
- zona general: Sur.
- referencia: Clínica 49.

### Mensaje con múltiples solicitudes — Blanca Talavera
Un solo mensaje con cuatro solicitudes diferentes.

Esperado: cuatro solicitudes independientes.

### Mensaje con múltiples solicitudes — Qubarum
Un solo mensaje con tres solicitudes diferentes.

Esperado: tres solicitudes independientes; no mezclar precios, zonas ni operaciones.

### Duplicado entre grupos — Rosa Linda / Lupita
Mismo mensaje publicado en dos grupos.

Esperado: detectar duplicado y evitar dos alertas idénticas; conservar los grupos de origen como información.

### Precio expresado en millones
`$2.3 millones` debe interpretarse como `$2,300,000`.

### Texto citado/respuesta
Evitar mezclar requisitos de una solicitud con una respuesta o cita de WhatsApp.

### Valle Verde
Solicitud: Casa en venta, Valle Verde, 3 recámaras, máximo $1,500,000, FOVISSSTE.

Resultado previo a integrar zonas:
- #132: 67%.
- #142: 67%.
- #116: 67%.

Los tres sólo coincidían en Venta + Casa + precio; la zona no estaba confirmada y las recámaras estaban sin dato.

Este caso debe repetirse después de integrar Zonas y sirve como prueba de regresión principal.

## Estado técnico conocido

- Radar probado como **v0.8.0-matching**.
- El Listener recorre chats configurados secuencialmente.
- Chats usados en pruebas:
  - INVENTARIOS Y PROSPECTOS.
  - Leones Inmobiliarios Dgo.
  - Terrenos en venta Dgo.
  - AISE tu socio en el éxito!.
  - José Juan (Tú).
- Chat de alertas: **Propiedades**.
- Matching local: `http://localhost:5102/api/radar/matching/local`.
- `RadarMatching:CorreoInventario` se configura mediante user-secrets.
- Se comprobó end-to-end: WhatsApp -> Radar -> RSMaps -> BD -> ranking -> Propiedades.

## Git y trabajo casa/oficina

### Regla principal
En cada computadora debe existir una sola copia oficial de trabajo del repositorio:

`D:\Repos\checua\RSMaps\maps4`

Evitar copias paralelas como `maps4-master`, `maps4-copia`, etc.

### Comenzando a trabajar en casa/oficina

Verificar antes de modificar:

```powershell
git status
git branch --show-current
git pull
```

### Terminando de trabajar en casa/oficina

Revisar cambios y cerrar con commit descriptivo y push:

```powershell
git status
git add .
git commit -m "<descripcion clara del trabajo>"
git push
```

El mensaje de commit debe describir lo realmente realizado durante la sesión.

## Estrategia actual de integración

- `master` es la base a proteger porque contiene el trabajo reciente de Zonas.
- `feature/rsmap-radar-matching` contiene Listener + matching y campos estructurados.
- Ambas ramas divergieron considerablemente.
- Rama creada para integración segura: **feature/radar-integration**.
- No hacer merge bruto sin revisar archivos compartidos.
- Archivos sensibles de integración:
  - `Models/InventarioInmuebleViewModel.cs`
  - `Repositorios/Implementacion/InventarioRepository.cs`
  - `Program.cs`
  - `maps4.csproj`
- Objetivo: conservar simultáneamente **Zonas + datos estructurados + Radar + matching**.

## Criterio de continuidad

Todo desarrollo inmobiliario nuevo debe continuar bajo **Radar**. Promoinmueble queda como fuente histórica temporal y debe revisarse sólo para rescatar ideas o decisiones útiles antes de eliminarlo.
