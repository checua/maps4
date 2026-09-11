# RSMaps — Árbol de avance

> Documento vivo para registrar lo terminado, lo activo, lo pendiente y las nuevas ideas sin perder el orden de ejecución.

## Cómo leer este árbol

Estados:

- ✅ Terminado y verificado
- 🟢 Implementado, falta validación final
- 🟡 En progreso / parcialmente resuelto
- 🔵 Planificado
- ⚪ Idea / backlog todavía sin programar
- 🔴 Bloqueo o problema conocido

El porcentaje mide **preparación para producción**, no cantidad de código escrito. Un módulo no llega a 100% hasta tener implementación, prueba y criterio de aceptación cumplido.

## Indicador general

**Preparación global estimada: 70%**

- Funcionamiento demostrable actual: ~84%
- Preparación para producción estable: ~70%

Esta cifra es provisional y se recalculará cuando cambien ramas, requisitos o criterios de aceptación.

---

# 0. Objetivo raíz — RSMaps listo para uso real

Objetivo: una plataforma inmobiliaria estable, escalable y publicable en Azure, con mapa, inventario, fotografías centralizadas, flujo de propiedades y RADAR integrado.

## 0.1 Arquitectura operativa definitiva — 78%

- ✅ RSMaps Web/API → Azure App Service (`rsmap.azurewebsites.net`).
- ✅ Base de datos → Azure SQL `mapsMarkers`.
- ✅ Fotografías → Azure Blob Storage.
- ✅ Storage Account específico de RSMaps creado en North Central US.
- ✅ Contenedor privado `rsmap-images` creado.
- ✅ App Service configurado con `ImageStorageProvider=AzureBlob`.
- ✅ Connection string de Blob guardada fuera de Git en configuración de Azure.
- ✅ Desarrollo `localhost:5103` probado contra el mismo Blob central.
- 🟡 Intelligence/Matching debe quedar servido por RSMaps Azure para operación normal.
- 🟡 RADAR Listener debe cambiar su servidor central de localhost a RSMaps Azure.
- 🔵 Prueba final: apagar `localhost:5102` y `localhost:5103` y confirmar que RADAR Listener sigue operando contra Azure.

Arquitectura objetivo:

```text
WhatsApp Web
   ↕
RADAR Listener en un PC agente
   │ HTTPS
   ▼
RSMaps Web/API/Matching en Azure App Service
   ├── Azure SQL mapsMarkers
   └── Azure Blob Storage / rsmap-images
```

`localhost` queda reservado para desarrollo y diagnóstico, no para operación normal del sistema.

---

# 1. Núcleo de producto y experiencia — 78% — peso 20%

### 1.1 Mapa principal — 85%
- ✅ Google Maps operativo.
- ✅ Tipos de inmueble con iconos diferenciados.
- ✅ Apertura de modal de inmueble.
- 🟢 Navegación Inventario → Ver en mapa con inmueble enfocado.
- 🟢 Marker temporal con icono correcto cuando el marker normal aún no está cargado.
- 🟢 Geolocalización no debe robar el centro cuando existe un inmueble solicitado explícitamente.
- 🟡 Validar inmueble #147 directamente en Azure y en navegación normal.
- 🔵 Revisar comportamiento móvil/iOS/Android.

### 1.2 Inventario privado — 80%
- ✅ Vista de inventario.
- ✅ Estados y visibilidad.
- ✅ Filtros y búsqueda.
- ✅ Ver inmueble en mapa.
- ✅ Edición de inmuebles propios activos.
- 🟡 Completar pruebas de permisos por usuario/cuenta/equipo.
- 🔵 Auditoría final de acciones sensibles.

### 1.3 Ciclo de vida del inmueble — 70%
- ✅ Borrador / publicado / pausado / retirado.
- ✅ Flujo de cierre de operación.
- 🟡 Verificar edición, publicación, venta/renta y reingreso en todos los casos legacy.
- 🔵 Definir pruebas de regresión del ciclo completo.

---

# 2. Mapa escalable y rendimiento — 84% — peso 20%

### 2.1 Carga por viewport — 92%
- ✅ Repositorio `IMapaViewportRepository`.
- ✅ Implementación `MapaViewportRepository`.
- ✅ `map-viewport.js`.
- ✅ Endpoint `Home/listaInmueblesViewport`.
- ✅ Consulta de inmuebles según área visible.
- ✅ Script SQL `54_map_viewport_index.sql`.
- 🟡 Verificar endpoint y movimiento/zoom directamente en Azure tras despliegue.
- 🟡 Verificar índice en la base productiva.
- 🔵 Prueba de carga con miles / decenas de miles de inmuebles.

### 2.2 Foco de inmueble seleccionado — 90%
- ✅ `map-focus-fix.js`.
- ✅ prioridad de navegación explícita sobre geolocalización.
- ✅ icono según tipo de propiedad.
- ✅ acercamiento garantizado.
- 🟡 Prueba completa del #147 en Azure.

### 2.3 Densidad futura del mapa — 45%
- ✅ La carga por viewport reduce la necesidad de traer todo el inventario.
- 🔵 Medir cuántos markers son cómodos por viewport.
- ⚪ Evaluar clustering sólo si las métricas lo justifican.
- ⚪ Evaluar paginación espacial / tiles si RSMaps crece a una escala mucho mayor.

---

# 3. Fotografías y medios — 63% — peso 15%

### 3.1 Compatibilidad `/cargas` → storage moderno — 88%
- ✅ El modal legacy sigue solicitando rutas `/cargas/{id}_{orden}.jpg`.
- ✅ Existe `ModernImageCompatibilityController` como puente hacia el storage moderno.
- ✅ Se retiró el redireccionamiento temporal localhost → Web App productiva.
- ✅ `localhost:5103/cargas/187_1.jpg` usando Azure Blob devuelve 404 limpio, sin 403/500 ni error de autenticación.
- 🟡 Validar fotos existentes una vez migrados sus payloads al Blob.

### 3.2 Azure Blob Storage — 90%
- ✅ Existe `AzureBlobInmuebleFotoStorage`.
- ✅ Soporta guardar, leer y eliminar JPEG/PNG/WEBP.
- ✅ Storage Account específico de RSMaps creado.
- ✅ TLS mínimo 1.2.
- ✅ Acceso público de blobs deshabilitado.
- ✅ Contenedor privado `rsmap-images` creado.
- ✅ App Service configurado para Azure Blob.
- ✅ localhost:5103 probado con la misma configuración de Blob.
- 🟡 Auditar existencia de blobs contra metadata activa.

### 3.3 Migración de fotos legacy — 45%
- ✅ Existen herramientas/scripts de migración y auditoría.
- ✅ El manifiesto generado del Paso 52 cubre 72 inmuebles / 808 fotos y actualmente termina en el inmueble #169.
- 🟡 Inventariar todas las fotos activas legacy y modernas fuera de Blob.
- 🟡 Verificar faltantes.
- 🔵 Migrar archivos físicos a Blob Storage conservando sus claves de metadata.
- 🔵 Validar tamaños/metadatos contra base de datos.

### 3.4 Fotos modernas fuera de Blob — 35%
- 🔴 #187 tiene 15 registros modernos: 14 activos + 1 inactivo, pero sus archivos físicos no están en casa.
- ✅ El contador legacy 14 coincide con las 14 fotos modernas activas.
- ✅ Metadata moderna de #187 es válida y usa claves GUID bajo `187/...jpg`.
- 🟡 Localizar el equipo origen de los payloads físicos, probablemente oficina.
- 🔵 Subirlos a Blob conservando exactamente las claves existentes.
- 🔵 Preservar inicialmente el archivo inactivo para evitar pérdida de historial.

### 3.5 Prevención de split-brain — 70%
- ✅ `AzureBlob` es el proveedor por defecto en la rama de publicación.
- ✅ Git no almacena inventario fotográfico de usuarios.
- ✅ Azure SQL + Azure Blob son las fuentes centrales.
- 🔵 Añadir auditoría/guard para advertir si una instalación comparte Azure SQL pero usa `Local` para nuevas fotos.

---

# 4. Azure y publicación — 78% — peso 15%

### 4.1 Base de datos Azure SQL — 90%
- ✅ Aplicación conectada a `mapsMarkers` en Azure.
- 🟡 Revisar índices nuevos y scripts pendientes.
- 🔵 Revisión de rendimiento y consultas lentas.

### 4.2 Azure App Service — 92%
- ✅ RSMaps publicado en `rsmap.azurewebsites.net`.
- ✅ App Service confirmado `Running` después del despliegue 2026-09-08/09.
- ✅ Home verificado con HTTP 200 después del deploy.
- ✅ Rama `feature/rsmap-publication-readiness` compilada en Release y desplegada por ZIP.
- 🟡 Smoke tests funcionales de mapa/inventario/login tras el deploy.

### 4.3 Configuración y secretos — 75%
- ✅ Configuración separable por entorno.
- ✅ Connection string de Blob permanece fuera de Git.
- ✅ App Service configurado con `ConnectionStrings__RSMapsImages`.
- 🟡 Consolidar restantes variables/secretos de Azure App Service.
- 🔵 Revisar API keys expuestas en JavaScript legacy.

### 4.4 Publicación segura — 72%
- ✅ Build Release correcto.
- ✅ ZIP Release generado (~12.5 MB).
- ✅ Respaldo previo de `wwwroot` descargado (~126 MB).
- ✅ Publicación ZIP realizada con limpieza y reinicio.
- ✅ Estado `Running` y Home HTTP 200 posteriores al despliegue.
- ✅ `/cargas/187_1.jpg` devuelve 404 esperado porque el blob físico aún no existe.
- 🟡 Smoke tests de funcionalidades principales.
- 🔵 Documentar rollback final y conservar respaldo predeploy.

---

# 5. RADAR — 78% — peso 20%

### 5.1 Listener WhatsApp — 85%
- ✅ Recorrido de chats.
- ✅ Descubrimiento de fuentes.
- ✅ Resiliencia de navegación.
- ✅ Evitar bloqueos del input de búsqueda.
- 🟡 Pruebas prolongadas sin intervención.
- 🟡 Cambiar endpoint central de localhost a RSMaps Azure cuando el backend Azure quede validado.

### 5.2 Inteligencia central — 85%
- ✅ Extracción estructurada de solicitudes.
- ✅ Motor OpenAI central.
- ✅ Recuperación después de fallos temporales.
- 🟡 Confirmar que la operación normal quede centralizada en RSMaps Azure.
- 🟡 Medir errores 502/timeouts y reducirlos.

### 5.3 Persistencia y recuperación — 85%
- ✅ Processing durable.
- ✅ ACK terminal.
- ✅ Recuperación de workflow.
- 🟡 Pruebas de reinicio/fallo real prolongado.

### 5.4 Matching inventario ↔ solicitudes — 70%
- ✅ Motor de matching existente.
- ✅ No enviar alerta cuando no existe coincidencia útil.
- ✅ Bug crítico #184 resuelto: un inmueble de $15,000 ya no puede superar una solicitud con máximo $13,000.
- ✅ `PrecioMaximo` opera como restricción dura estricta.
- ✅ Si existe máximo de precio y el inmueble no tiene precio verificable, no se genera candidato.
- ✅ Restricciones duras no verificables operan fail-closed.
- ✅ `NO ORILLAS` detectado por RuleBased y OpenAI.
- ✅ `SIN AMUEBLAR` protegido fail-closed mientras el inventario no pueda diferenciar `No amueblado` de `sin dato`.
- ✅ Regresiones automáticas de matching y E2E incorporadas.
- ✅ PR #3 fusionado en `feature/rsmap-publication-readiness` (`7113969`).
- 🔴 Hallazgo activo: muchas ejecuciones reportan `Inventario evaluado: 1`.
- 🟡 Determinar por qué RADAR sólo está viendo/evaluando un inmueble en esos casos.
- 🔵 Validar matching con inventario amplio y distintos permisos/visibilidades.
### 5.5 Alertas y experiencia RADAR — 80%
- ✅ Política fail-closed para evitar falsos positivos.
- ✅ Alertas sólo con match útil.
- ✅ Caso real #184 validado E2E: interpretación → procesamiento central → matching → 0 candidatos → NO ALERT.
- ✅ Preferencias blandas como `Lo más nuevo posible` no bloquean el matching.
- 🔵 Afinar ranking de coincidencias.
- 🔵 Definir formato final de alerta al asesor.

---

# 6. Calidad, seguridad y operación — 43% — peso 10%

### 6.1 Pruebas — 45%
- ✅ Build Release correcto de la rama de publicación.
- 🟡 Smoke test inicial de Azure: App `Running`, Home HTTP 200.
- 🔵 Suite mínima de smoke tests.
- 🔵 Regresión mapa/inventario/fotos/login/RADAR.
- 🔵 Prueba móvil.

### 6.2 Seguridad — 40%
- 🟡 Cookies/auth existentes.
- ✅ Blob privado y TLS 1.2 mínimo.
- ✅ Connection string de Blob fuera de Git.
- 🔴 Revisar secretos y API keys legacy visibles en código cliente.
- 🔵 Revisión de autorización por endpoints.
- 🔵 Revisión de subida de archivos.

### 6.3 Observabilidad — 35%
- 🟡 Logs de RADAR útiles.
- 🔵 Health checks web/DB/Blob/RADAR.
- 🔵 Métricas de tiempos de consulta y errores.
- 🔵 Registro de versión desplegada.

### 6.4 Respaldo y recuperación — 60%
- ✅ Git y ramas de trabajo disponibles.
- ✅ Respaldo predeploy de `wwwroot` conservado localmente.
- 🔵 Política de backup de BD/fotos.
- 🔵 Procedimiento de rollback de Azure documentado.

---

# 7. Ideas futuras / backlog — sin porcentaje de compromiso

Este nivel recibe ideas nuevas sin interrumpir automáticamente la ruta activa.

Cada idea nueva debe registrar:

1. **Nombre**
2. **Ramal del árbol** donde pertenece
3. **Motivo / valor para el usuario**
4. **Dependencias**
5. **Prioridad**: A = necesaria para producción, B = siguiente versión, C = futura
6. **Criterio de terminado**
7. **Estado**

Ideas ya identificadas:

- ⚪ Clustering de markers si la densidad del viewport lo exige.
- ⚪ Escalado espacial adicional si el inventario llega a decenas/cientos de miles.
- ⚪ Mejoras de UX móvil.
- ⚪ Ranking avanzado de RADAR.
- ⚪ Panel de salud/operación de RADAR.

---

# Ruta activa recomendada

Ésta es la parte lineal del Árbol de avance. El árbol organiza; la Ruta activa decide qué hacemos primero.

1. **Smoke test directo en Azure de la versión recién desplegada:** mapa, endpoint viewport, login/inventario y foco de propiedad.
2. **Validar inmueble #147 directamente en Azure:** carga normal, Inventario → Ver en mapa, marker correcto, Acercar y prioridad sobre geolocalización.
3. **Validar viewport en Azure:** mover/zoom y confirmar carga sólo del área visible.
4. **Revisar/aplicar índice SQL 54 en Azure SQL.**
5. **Localizar en oficina los payloads modernos del inmueble #187 y cualquier foto activa fuera de Blob.**
6. **Migrar fotos legacy y modernas a `rsmap-images` preservando claves existentes.**
7. **Crear auditoría metadata → Blob (Missing / SizeMismatch / OK).**
8. **Localizar configuración del RADAR Listener que apunta a `localhost:5102` y preparar cambio a RSMaps Azure.**
9. **Prueba de corte: apagar 5102/5103 y confirmar Listener → RSMaps Azure.**
10. **Corregir `Inventario evaluado: 1` de RADAR — siguiente bloqueo activo después del cierre del bug #184.**
11. **Pruebas integrales RSMaps + RADAR.**
12. **Revisión de secretos/API keys/seguridad y observabilidad.**

---

# Registro de decisiones

## 2026-09-08 — Modelo de seguimiento

Se adopta **Árbol de avance + Ruta activa** en lugar de un índice puramente lineal.

Motivo:

- El árbol permite agregar ideas sin perderlas ni mezclarlas con el trabajo inmediato.
- La ruta activa mantiene disciplina de ejecución y evita saltar continuamente entre ramales.
- Los porcentajes permiten visualizar preparación real para producción.

## 2026-09-08 — Arquitectura definitiva de RSMaps y RADAR

Se fija la arquitectura operativa:

- RSMaps Web/API/Matching → Azure App Service.
- Datos estructurados → Azure SQL.
- Fotografías → Azure Blob Storage.
- RADAR Listener → PC agente local únicamente por necesidad de controlar WhatsApp Web.
- RADAR Listener se comunicará por HTTPS con RSMaps Azure.
- `localhost` queda sólo para desarrollo/diagnóstico.

## 2026-09-08/09 — Azure Blob operativo

Se creó un Storage Account dedicado a imágenes de RSMaps en North Central US, con TLS 1.2 mínimo, blobs públicos deshabilitados y contenedor privado `rsmap-images`.

RSMaps App Service quedó configurado con:

- `RSMaps__ImageStorageProvider=AzureBlob`
- `RSMaps__ImageStorageContainer=rsmap-images`
- `ConnectionStrings__RSMapsImages` almacenada fuera de Git.

La rama de publicación fue probada primero en `localhost:5103` contra Azure Blob y arrancó correctamente.

## 2026-09-08/09 — Publicación controlada en Azure

- Build Release correcto.
- Paquete ZIP generado (~12.5 MB).
- Respaldo predeploy de `wwwroot` descargado (~126 MB).
- ZIP desplegado a `rsmap.azurewebsites.net` con limpieza/reinicio.
- App Service confirmado `Running`.
- Home respondió HTTP 200.
- `/cargas/187_1.jpg` respondió HTTP 404 esperado: metadata existe, payload físico aún no ha sido migrado al Blob.

## 2026-09-08 — Diagnóstico de fotos #187

El inmueble #187 no es un caso legacy fantasma. Tiene metadata moderna válida: 14 fotos activas + 1 inactiva bajo claves GUID. El contador legacy de 14 coincide exactamente con las 14 activas. Los archivos físicos no están presentes en los worktrees de casa; deben localizarse en el equipo donde se cargaron originalmente y subirse a Blob conservando sus claves actuales.
