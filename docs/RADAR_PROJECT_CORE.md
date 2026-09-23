# RADAR Project Core

> El estado, porcentajes, roadmap y Ruta activa se mantienen en `docs/RSMAPS_PROGRESS_TREE.md`, Árbol Maestro General de RSMaps + RADAR.

Este documento es la referencia canónica para continuar el proyecto RADAR entre chats, equipos y etapas de desarrollo.

## 1. Visión

RADAR debe evolucionar como una plataforma multiusuario de inteligencia inmobiliaria integrada a RSMaps.

La arquitectura objetivo es:

WhatsApp -> RADAR Agent local -> RADAR Intelligence central -> RSMaps -> matching contra inventario autorizado -> respuesta/alerta -> RADAR Agent -> WhatsApp

El Agent local es un conector liviano de entrada/salida con WhatsApp. La lógica de negocio, la inteligencia, los permisos, la configuración, el matching y la persistencia deben vivir del lado de RSMaps/RADAR Intelligence.

## 2. Principios no negociables

- No distribuir `OPENAI_API_KEY` ni otros secretos de IA en las computadoras de los Agents.
- Centralizar modelos, prompts, conocimiento, fallback, costos y observabilidad en RADAR Intelligence/RSMaps.
- RSMaps es la fuente de verdad para inventario, cuentas, usuarios, roles, permisos, zonas y matching.
- El Agent no debe decidir qué inventario puede ver; solo se autentica y el servidor resuelve su alcance.
- Un mensaje puede producir 0, 1 o N solicitudes interpretadas.
- Conservar mensaje original, interpretación, confianza, matching, resultado y trazabilidad suficiente para auditoría.
- Cuando la IA no tenga certeza suficiente, debe existir manejo de confianza/fallback y no inventar datos.
- Priorizar exactitud y seguridad sobre micro-optimizaciones prematuras.

## 3. Forma de trabajo

La colaboración debe ser incremental, concreta y verificable.

- Cambios pequeños y reversibles.
- Una hipótesis por prueba cuando sea posible.
- Medir antes de optimizar.
- No romper una ruta estable para perseguir una mejora marginal.
- Mantener una rama/línea estable y usar RADAR LAB para experimentos aislados.
- Compilar y probar después de cambios relevantes.
- Antes de consolidar una optimización, comparar contra una línea base medida.
- Cuando un experimento empeora el resultado, descartarlo y volver al último estado bueno.
- Dar instrucciones exactas de PowerShell/Git cuando el usuario ejecute pruebas.
- Evitar construir de golpe multiusuario, panel, persistencia, Intelligence y distribución. Avanzar por capas coherentes.

## 4. Arquitectura de responsabilidades

### RADAR Agent local

Responsabilidades objetivo:

- Mantener la sesión de WhatsApp Web.
- Detectar mensajes nuevos en chats configurados.
- Enviar mensaje + metadatos al backend.
- Recibir el resultado procesado.
- Enviar alertas a WhatsApp cuando corresponda.
- Mantener heartbeat, identidad del dispositivo y sincronización de configuración.

No debe contener secretos globales de IA ni lógica central de negocio.

### RADAR Intelligence central

Responsabilidades objetivo:

- Interpretar lenguaje natural.
- Separar 0..N solicitudes.
- Normalizar operación, tipo, subtipo, zona, presupuesto y características.
- Usar conocimiento centralizado.
- Gestionar modelo principal, fallback, prompts, versiones y costos.
- Producir nivel de confianza y evidencia suficiente para decisiones posteriores.

### RSMaps

Responsabilidades objetivo:

- Autenticación y autorización.
- Cuenta, asesor, rol y alcance de inventario.
- Catálogo de zonas y alias.
- Inventario autorizado.
- Matching y ranking.
- Persistencia de solicitudes, interpretaciones, coincidencias, envíos y auditoría.
- Configuración central de Agents y chats.
- Paneles operativos y métricas.

## 5. Roadmap base recuperado

La secuencia conceptual acordada desde el inicio fue:

1. Laboratorio de interpretación y matching.
2. Separar Listener, Intelligence y backend.
3. Configuración central del Agent desde RSMaps.
4. Persistencia/historial SQL y auditoría.
5. Agents identificables y vinculados a cuenta/usuario.
6. Validar segundo usuario/segundo Agent y aislamiento real.
7. Panel de RSMaps y experiencia operativa.
8. Centralizar completamente RADAR Intelligence en servidor.
9. Empaquetado, actualización, telemetría y operación multi-Agent.

El orden puede ajustarse, pero los principios anteriores deben conservarse.

## 6. Estado de referencia actual

A 2026-09-22 ya existen, entre otros:

- Listener de WhatsApp funcional.
- Matching contra inventario RSMaps.
- RADAR Intelligence central ejecutándose en RSMaps Azure para la operación productiva.
- Agent autenticado y vinculado a RSMaps.
- Credencial local protegida en Windows.
- Configuración central de chats, destino e intervalo.
- Heartbeat y sincronización de configuración.
- Descubrimiento de chats bajo demanda.
- Modo seguro de laboratorio.
- Salida temprana de estabilización de historial cuando una ronda agrega 0 mensajes.
- Processing, resultado central, Delivery y ACK terminal durables.
- Listener productivo estable en commit `92b994e` (`fix: separate WhatsApp reply quote from own message`), con fallback local deshabilitado.
- Política segura de Delivery: sólo una recomendación automática válida genera Delivery; las alternativas terminan en `ALTERNATIVA_PARA_REVISION` por default.
- Recovery productivo validado: 11/11 workflows históricos cerrados sin Delivery ni WhatsApp; colas finales en cero.
- Reply/quote capturado estructuralmente: la clasificación semántica usa exclusivamente `TextoPropio`; `TextoCitado` nunca origina por sí solo una nueva solicitud; forwarded no se descarta automáticamente; una captura no confirmada conserva Retry.
- `SolicitudAccionable` implementada y validada localmente: `EsDemanda → Intelligence → SolicitudInmobiliaria → Accionabilidad → Matching sólo si Accionable`. El backend central es autoridad y el Listener consumidor; `Accionabilidad = null` conserva compatibilidad legacy.
- Estados de accionabilidad: `Accionable`, `NecesitaMasDatos` e `Inconsistente`. Falta de datos o contradicción termina sin Matching/Delivery mediante ACK específico; interpretación inválida conserva retry/fallo central.
- Auto-B implementado y validado localmente: ser accionable no implica ser auto-recomendable. Exige Operación + Tipo + dos categorías comerciales adicionales, incluyendo Zona o Precio; conserva score, pesos y umbrales, y degrada a alternativa con `ESPECIFICIDAD_INSUFICIENTE`.
- Deep Links implementados y validados localmente: `/i/{id}` y `/m/{id}` autenticados/autorizados por cuenta y equipo; RADAR agrega links estructurados de Inventario y Mapa a recomendaciones y alternativas.

Commits versionados pendientes de deploy productivo: `d5f1d43` (evaluador de accionabilidad), `d333b92` (integración durable), `28c1daa` (Auto-B) y `451c3d9` (Deep Links). Producción permanece en el último Listener estable desplegado: `92b994e`.

La ruta inmediata es desplegar y ejecutar QA productivo de los cambios acumulados; después se abordará la deduplicación durable cross-chat.

Continúan pendientes la captura del timestamp real, la política definitiva de alternativas y la revisión de tolerancias de requisitos mínimos. Estos puntos no deben considerarse resueltos por el deploy de `92b994e`.

## 7. Criterio para decidir qué construir después

Antes de cualquier cambio, preguntar:

1. ¿Aumenta confiabilidad, seguridad o capacidad multiusuario?
2. ¿Reduce acoplamiento del Agent local?
3. ¿Centraliza inteligencia, configuración o datos donde corresponde?
4. ¿Podemos medir el resultado?
5. ¿Podemos revertirlo fácilmente si falla?

Si una optimización solo ahorra unos segundos pero introduce riesgo o complejidad, se posterga frente a mejoras estructurales.

## 8. Estilo de colaboración

La comunicación del proyecto debe conservar el tono de los primeros chats: técnico, directo, cercano y orientado a construir juntos.

- Explicar por qué hacemos cada cambio.
- Evitar saltar etapas sin necesidad.
- Señalar riesgos claramente.
- Celebrar avances reales sin maquillar problemas.
- Mantener continuidad entre chats recuperando este documento y el estado actual del repositorio.
- Cuando haya varias rutas posibles, recomendar una y justificarla, en vez de dejar una lista ambigua.

Este documento debe revisarse cuando cambie una decisión arquitectónica importante.
