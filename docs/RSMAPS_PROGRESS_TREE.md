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

**Preparación global estimada: 66%**

- Funcionamiento demostrable actual: ~80%
- Preparación para producción estable: ~66%

Esta cifra es provisional y se recalculará cuando cambien ramas, requisitos o criterios de aceptación.

---

# 0. Objetivo raíz — RSMaps listo para uso real

Objetivo: una plataforma inmobiliaria estable, escalable y publicable en Azure, con mapa, inventario, fotografías centralizadas, flujo de propiedades y RADAR integrado.

## 1. Núcleo de producto y experiencia — 78% — peso 20%

### 1.1 Mapa principal — 85%
- ✅ Google Maps operativo.
- ✅ Tipos de inmueble con iconos diferenciados.
- ✅ Apertura de modal de inmueble.
- 🟢 Navegación Inventario → Ver en mapa con inmueble enfocado.
- 🟢 Marker temporal con icono correcto cuando el marker normal aún no está cargado.
- 🟢 Geolocalización no debe robar el centro cuando existe un inmueble solicitado explícitamente.
- 🟡 Validar inmueble #147 en casa/oficina y navegación normal.
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

# 2. Mapa escalable y rendimiento — 82% — peso 20%

### 2.1 Carga por viewport — 90%
- ✅ Repositorio `IMapaViewportRepository`.
- ✅ Implementación `MapaViewportRepository`.
- ✅ `map-viewport.js`.
- ✅ Consulta de inmuebles según área visible.
- ✅ Script SQL `54_map_viewport_index.sql`.
- 🟡 Verificar índice en la base productiva.
- 🟡 Prueba real al mover/zoom del mapa.
- 🔵 Prueba de carga con miles / decenas de miles de inmuebles.

### 2.2 Foco de inmueble seleccionado — 90%
- ✅ `map-focus-fix.js`.
- ✅ prioridad de navegación explícita sobre geolocalización.
- ✅ icono según tipo de propiedad.
- ✅ acercamiento garantizado.
- 🟡 Prueba completa del #147 en localhost:5103.

### 2.3 Densidad futura del mapa — 45%
- ✅ La carga por viewport reduce la necesidad de traer todo el inventario.
- 🔵 Medir cuántos markers son cómodos por viewport.
- ⚪ Evaluar clustering sólo si las métricas lo justifican.
- ⚪ Evaluar paginación espacial / tiles si RSMaps crece a una escala mucho mayor.

---

# 3. Fotografías y medios — 45% — peso 15%

### 3.1 Compatibilidad de fotos legacy en localhost — 80%
- ✅ Detectado: modal legacy usa rutas `/Cargas/...` locales.
- 🟢 Fallback localhost → `https://rsmap.azurewebsites.net/Cargas/...` implementado en `map-focus-fix.js`.
- 🟡 Validar las miniaturas del inmueble #147 en localhost:5103.

### 3.2 Azure Blob Storage — 70%
- ✅ Existe `AzureBlobInmuebleFotoStorage`.
- ✅ Soporta guardar, leer y eliminar JPEG/PNG/WEBP.
- ✅ Program.cs permite proveedor `Local` o `AzureBlob`.
- 🟡 Crear/verificar `ConnectionStrings:RSMapsImages` de forma segura.
- 🟡 Verificar contenedor `rsmap-images`.
- 🔵 Cambiar producción a `ImageStorageProvider=AzureBlob` cuando termine la migración.

### 3.3 Migración de fotos legacy — 45%
- ✅ Existen herramientas/scripts de migración y auditoría.
- 🟡 Inventariar todas las fotos activas legacy.
- 🟡 Verificar faltantes.
- 🔵 Migrar archivos físicos a Blob Storage.
- 🔵 Validar metadatos contra base de datos.
- 🔵 Retirar dependencia de `/Cargas` sólo después de validación completa.

### 3.4 Arquitectura final de medios — 30%
- 🔵 Localhost y Azure deben leer las mismas fotos desde Blob Storage.
- 🔵 Nuevas cargas deben ir directamente a Blob.
- 🔵 Git nunca debe almacenar el inventario fotográfico de usuarios.

---

# 4. Azure y publicación — 55% — peso 15%

### 4.1 Base de datos Azure SQL — 90%
- ✅ Aplicación conectada a `mapsMarkers` en Azure.
- 🟡 Revisar índices nuevos y scripts pendientes.
- 🔵 Revisión de rendimiento y consultas lentas.

### 4.2 Azure App Service — 75%
- ✅ RSMaps existe publicado en `rsmap.azurewebsites.net`.
- 🟡 La rama nueva aún debe publicarse de forma controlada.
- 🔵 Confirmar configuración de producción antes del despliegue.

### 4.3 Configuración y secretos — 55%
- ✅ Configuración separable por entorno.
- 🟡 Connection strings reales deben permanecer fuera de Git.
- 🔵 Consolidar variables/secretos de Azure App Service.
- 🔵 Revisar API keys expuestas en JavaScript legacy.

### 4.4 Publicación segura — 35%
- 🔵 Build Release.
- 🔵 Backup / punto de retorno.
- 🔵 Publicación de `feature/rsmap-publication-readiness`.
- 🔵 Smoke tests en Azure.
- 🔵 Rollback documentado.

---

# 5. RADAR — 75% — peso 20%

### 5.1 Listener WhatsApp — 85%
- ✅ Recorrido de chats.
- ✅ Descubrimiento de fuentes.
- ✅ Resiliencia de navegación.
- ✅ Evitar bloqueos del input de búsqueda.
- 🟡 Pruebas prolongadas sin intervención.

### 5.2 Inteligencia central — 85%
- ✅ Extracción estructurada de solicitudes.
- ✅ Motor OpenAI central.
- ✅ Recuperación después de fallos temporales.
- 🟡 Medir errores 502/timeouts y reducirlos.

### 5.3 Persistencia y recuperación — 85%
- ✅ Processing durable.
- ✅ ACK terminal.
- ✅ Recuperación de workflow.
- 🟡 Pruebas de reinicio/fallo real prolongado.

### 5.4 Matching inventario ↔ solicitudes — 55%
- ✅ Motor de matching existente.
- ✅ No enviar alerta cuando no existe coincidencia útil.
- 🔴 Hallazgo actual: muchas ejecuciones reportan `Inventario evaluado: 1`.
- 🟡 Determinar por qué RADAR sólo está viendo/evaluando un inmueble en esos casos.
- 🔵 Validar matching con inventario amplio y distintos permisos/visibilidades.

### 5.5 Alertas y experiencia RADAR — 70%
- ✅ Política fail-closed para evitar falsos positivos.
- ✅ Alertas sólo con match útil.
- 🔵 Afinar ranking de coincidencias.
- 🔵 Definir formato final de alerta al asesor.

---

# 6. Calidad, seguridad y operación — 40% — peso 10%

### 6.1 Pruebas — 40%
- 🟡 Build limpio por ramas activas.
- 🔵 Suite mínima de smoke tests.
- 🔵 Regresión mapa/inventario/fotos/login/RADAR.
- 🔵 Prueba móvil.

### 6.2 Seguridad — 35%
- 🟡 Cookies/auth existentes.
- 🔴 Revisar secretos y API keys legacy visibles en código cliente.
- 🔵 Revisión de autorización por endpoints.
- 🔵 Revisión de subida de archivos.

### 6.3 Observabilidad — 35%
- 🟡 Logs de RADAR útiles.
- 🔵 Health checks web/DB/Blob/RADAR.
- 🔵 Métricas de tiempos de consulta y errores.
- 🔵 Registro de versión desplegada.

### 6.4 Respaldo y recuperación — 45%
- 🟡 Git y ramas de trabajo disponibles.
- 🔵 Política de backup de BD/fotos.
- 🔵 Procedimiento de rollback de Azure.

---

# 7. Ideas futuras / backlog — sin porcentaje de compromiso

Este nivel recibe ideas nuevas sin interrumpir automáticamente la ruta activa.

Cada idea nueva debe registrar:

1. **Nombre**
2. **Rama del árbol** donde pertenece
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

1. **Validar fallback de fotos legacy en localhost:5103.**
2. **Terminar prueba del #147:** carga normal, Inventario → Ver en mapa, marker correcto, Acercar y geolocalización.
3. **Validar viewport:** mover/zoom y confirmar carga sólo del área visible.
4. **Revisar/aplicar índice SQL 54 en Azure SQL.**
5. **Cerrar migración de imágenes a Azure Blob Storage.**
6. **Configurar Blob en desarrollo y producción.**
7. **Pruebas completas de inventario y ciclo de propiedad.**
8. **Corregir `Inventario evaluado: 1` de RADAR.**
9. **Pruebas integrales RSMaps + RADAR.**
10. **Revisión de secretos/API keys/seguridad.**
11. **Publicación controlada en Azure.**
12. **Smoke test de producción + rollback listo.**

---

# Registro de decisiones

## 2026-09-08 — Modelo de seguimiento

Se adopta **Árbol de avance + Ruta activa** en lugar de un índice puramente lineal.

Motivo:

- El árbol permite agregar ideas sin perderlas ni mezclarlas con el trabajo inmediato.
- La ruta activa mantiene disciplina de ejecución y evita saltar continuamente entre ramas.
- Los porcentajes permiten visualizar preparación real para producción.

## 2026-09-08 — Fotografías

Dirección arquitectónica:

- Código → Git/GitHub
- Datos → Azure SQL
- Fotografías → Azure Blob Storage
- Aplicación → Azure App Service

Durante la transición, localhost puede usar como fallback las fotografías legacy que todavía viven en la Web App productiva.
