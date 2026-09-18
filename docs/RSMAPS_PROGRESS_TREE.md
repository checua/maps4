# RSMaps + RADAR — Árbol Maestro General

> Referencia principal del alcance, estado, prioridades y visión de RSMaps, RADAR, RADAR Intelligence y RADAR Agent.

## Leyenda visual

- 🟢 **80–100%** — sólido, muy avanzado u operativo.
- 🟡 **60–79%** — avanzado o en desarrollo.
- 🟠 **30–59%** — parcial o intermedio.
- 🔴 **0–29%** — inicial o planificado.

Los porcentajes son estimaciones de gestión del proyecto, no métricas automáticas de cobertura de código.

## Dashboard ejecutivo

- **Avance simple:** 924 / 19 = **48.63% ≈ 49%**.
- **Avance general ponderado:** suma de `peso × avance / 100` = **59.89% ≈ 60%**.
- **Avance del núcleo operativo:** 52.15 / 66 × 100 = **79.02% ≈ 79%**.
- **Suma de pesos:** **100%**. **Bloques maestros:** **19**.

| ID | Bloque | Peso | Avance | Indicador | Contribución | Estado | Responsable | Siguiente acción |
|---:|---|---:|---:|:---:|---:|---|---|---|
| 0 | Visión del producto | 2% | 40% | 🟠 | 0.80% | Parcial | JJ | Priorizar alcance comercial |
| 1 | RSMaps — Núcleo | 9% | 78% | 🟡 | 7.02% | Avanzado | JJ + Codex | Cerrar permisos y ciclo completo |
| 2 | RSMaps — Mapa | 7% | 68% | 🟡 | 4.76% | Avanzado | JJ + Codex | Validar escala y viewport |
| 3 | Zonas / Geointeligencia | 4% | 18% | 🔴 | 0.72% | Inicial | JJ + Codex | Integrar zonas al matching |
| 4 | RADAR — Captura | 7% | 82% | 🟢 | 5.74% | Operativo con brechas | JJ + Codex | Capturar reply/quote |
| 5 | RADAR — Interpretación | 7% | 72% | 🟡 | 5.04% | Avanzado | JJ + Codex | Separar textos y accionabilidad |
| 6 | RADAR Intelligence | 8% | 86% | 🟢 | 6.88% | Central operativo | JJ + Codex | Fortalecer validación/confianza |
| 7 | RADAR — Matching | 8% | 84% | 🟢 | 6.72% | Operativo | JJ + Codex | Revisar mínimos y multi-cuenta |
| 8 | RADAR — Delivery / Alertas | 7% | 80% | 🟢 | 5.60% | Operativo seguro | JJ + Codex, checkpoint humano | Política final de alternativas |
| 9 | RADAR Agent | 7% | 85% | 🟢 | 5.95% | Productivo | JJ + Codex, checkpoint humano | Health y multi-Agent |
| 10 | Deduplicación | 6% | 32% | 🟠 | 1.92% | Parcial | JJ + Codex | Deduplicación durable cross-chat |
| 11 | Estadísticas / Analytics | 4% | 12% | 🔴 | 0.48% | Inicial | JJ + Codex | Definir métricas y dashboard |
| 12 | Inteligencia de mercado | 3% | 8% | 🔴 | 0.24% | Visión | JJ | Priorizar señales comerciales |
| 13 | Prospectos / CRM | 3% | 22% | 🔴 | 0.66% | Inicial | JJ | Definir ciclo de lead |
| 14 | Cuentas / Organizaciones | 4% | 28% | 🔴 | 1.12% | Inicial | JJ + Codex | Validar aislamiento |
| 15 | Administración / Control | 4% | 35% | 🟠 | 1.40% | Parcial | JJ + Codex | Diseñar panel RADAR |
| 16 | QA / Observabilidad | 6% | 74% | 🟡 | 4.44% | Avanzado | JJ + Codex | Health y regresión integral |
| 17 | Expansión | 2% | 15% | 🔴 | 0.30% | Planificada | JJ | Criterios de expansión |
| 18 | Futuro / I+D | 2% | 5% | 🔴 | 0.10% | Visión | JJ | Mantener backlog priorizado |
|  | **Total** | **100%** | **48.63% simple** |  | **59.89%** |  |  |  |

### Núcleo operativo

Bloques 1, 2, 4, 5, 6, 7, 8, 9 y 16: pesos 66%, contribuciones 52.15; avance normalizado **79.02%**.

## Ruta activa

1. Reply / Quote.
2. Captura estructurada.
3. `TextoPropio` / `TextoCitado`.
4. Regresiones.
5. `SolicitudAccionable`.
6. Deduplicación durable cross-chat.
7. Política final de alternativas.
8. Hard constraints comerciales.
9. Escalabilidad de mapa.
10. Analytics / Inteligencia de mercado.

---

# Árbol Maestro detallado

## 0. 🟠 Visión del producto — 40% — peso 2%
**Estado:** dirección definida parcialmente. **Responsable:** JJ.
- 🟡 Plataforma e inteligencia inmobiliaria unificadas.
- 🟠 Automatización comercial y asistencia a asesores.
- 🔴 Expansión multi-mercado, producto comercial y SaaS.

## 1. 🟡 RSMaps — Núcleo — 78% — peso 9%
**Estado:** núcleo web productivo. **Responsable:** JJ + Codex.
- 🟢 Inventarios, propiedades, usuarios, publicación y APIs.
- 🟡 Prospectos, cuentas, permisos y ciclo de vida.
- 🟢 Fotos legacy, límite 40, Azure, SQL y Blob.
- 🔴 Documentos completos y fotos modernas de #187 no localizadas.

## 2. 🟡 RSMaps — Mapa — 68% — peso 7%
**Estado:** funcional; escala pendiente. **Responsable:** JJ + Codex.
- 🟢 Markers, filtros, navegación, property focus y viewport.
- 🟡 Pan/zoom, cache y medición productiva.
- 🔴 Clustering, 10,000+ inmuebles y optimización futura.

## 3. 🔴 Zonas / Geointeligencia — 18% — peso 4%
**Estado:** fundamentos existentes. **Responsable:** JJ + Codex.
- 🟡 Colonias, fraccionamientos, zonas y alias.
- 🟠 Polígonos y clasificación espacial parcial.
- 🔴 Distancias, proximidad, preferencias, zonas comerciales/de demanda y métricas geográficas.
- 🟠 `NO ORILLAS` fail-closed; comprobación geográfica real pendiente.

## 4. 🟢 RADAR — Captura — 82% — peso 7%
**Estado:** WhatsApp productivo; contexto incompleto. **Responsable:** JJ + Codex.
- 🟢 WhatsApp, chats, `MessageId`, autor y teléfono.
- 🔴 Timestamp real, reply, quote, forward, `TextoPropio` y `TextoCitado`.
- 🔴 Otras fuentes futuras.

## 5. 🟡 RADAR — Interpretación — 72% — peso 7%
**Estado:** extracción avanzada. **Responsable:** JJ + Codex.
- 🟡 Solicitud inmobiliaria; demanda/oferta/otro; segmentación múltiple.
- 🟢 Venta/renta, tipo/subtipo, precio/presupuesto y forma de pago.
- 🟡 Zona, recámaras, baños, plantas, terreno, construcción, cochera y requisitos.
- 🟢 Hard constraints interpretados fail-closed.
- 🔴 `SolicitudAccionable` y protección previa contra texto citado.

## 6. 🟢 RADAR Intelligence — 86% — peso 8%
**Estado:** central productivo. **Responsable:** JJ + Codex.
- 🟢 OpenAI central, interpretación, normalización y validación.
- 🟢 `ResultadoCentralJson`, persistencia durable y replay.
- 🟢 Fallback local deshabilitado.
- 🟡 Confianza, resiliencia, observabilidad y evolución futura.

## 7. 🟢 RADAR — Matching — 84% — peso 8%
**Estado:** operativo con inventario real. **Responsable:** JJ + Codex.
- 🟢 Inventario, filtros, hard/payment constraints.
- 🟡 Soft constraints, scoring y ranking.
- 🟢 Recomendación, alternativa, cero coincidencias y explicación.
- 🟡 Tolerancias de 80% terreno/construcción y otros mínimos.
- 🟠 Multi-cuenta; 🔴 aprendizaje futuro.

## 8. 🟢 RADAR — Delivery / Alertas — 80% — peso 7%
**Estado:** flujo seguro productivo. **Responsable:** JJ + Codex, checkpoint humano.
- 🟢 Decision flow, durable prepare/complete, idempotencia y Safe Lab.
- 🟢 Recomendación válida → Delivery.
- 🟢 Alternativa → `ALTERNATIVA_PARA_REVISION`, sin Delivery por default.
- 🟢 Cero real → `SIN_COINCIDENCIA_UTIL`; transitorio/incoherente → Retry.
- 🟢 Terminal ACK y política configurable; definición comercial final pendiente.

## 9. 🟢 RADAR Agent — 85% — peso 7%
**Estado:** productivo. **Responsable:** JJ + Codex, checkpoint humano.
- 🟢 Listener, pairing, bearer/DPAPI y configuración.
- 🟢 Launcher, Scheduled Task, `WhatsAppProfile` y logs.
- 🟢 Deployment, backup y rollback.
- 🟡 Health/actualización; 🔴 multi-Agent a escala.

## 10. 🟠 Deduplicación — 32% — peso 6%
**Estado:** retry/durable avanzados; cross-chat pendiente. **Responsable:** JJ + Codex.
- 🟢 `MessageId`, retry, durable y `ClaveEntrega`.
- 🔴 Cross-chat, cross-post y fingerprint.
- 🟠 Autor, teléfono y ventana temporal.
- 🔴 Similitud semántica futura.

## 11. 🔴 Estadísticas / Analytics — 12% — peso 4%
**Estado:** datos base, producto pendiente. **Responsable:** JJ + Codex.
- 🔴 Solicitudes por fecha, chat, asesor, zona, tipo, operación y precio.
- 🔴 Requisitos, pagos, demanda sin inventario y match rate.
- 🔴 Recomendaciones, alternativas, cero match e inmuebles sugeridos.
- 🔴 Conversión y dashboards.

## 12. 🔴 Inteligencia de mercado — 8% — peso 3%
**Estado:** visión. **Responsable:** JJ.
- 🔴 Qué busca la gente; oferta vs demanda; demanda no satisfecha.
- 🔴 Zonas, precios, tipos, características y tendencias.
- 🔴 Captación, señales comerciales e inventario faltante.

## 13. 🔴 Prospectos / CRM — 22% — peso 3%
**Estado:** inicial. **Responsable:** JJ.
- 🟠 Lead e identidad básicos.
- 🔴 Historial, intereses, seguimiento y propiedades sugeridas.
- 🔴 Contacto, estado, conversaciones, actividad, automatizaciones y conversión.

## 14. 🔴 Cuentas / Organizaciones — 28% — peso 4%
**Estado:** base existente. **Responsable:** JJ + Codex.
- 🟠 Multi-cuenta, `IdCuenta`, inventario, permisos y roles.
- 🔴 Multi-asesor, multi-Agent, aislamiento, organizaciones y SaaS.

## 15. 🟠 Administración / Control — 35% — peso 4%
**Estado:** capacidades distribuidas. **Responsable:** JJ + Codex.
- 🔴 Panel RADAR.
- 🟡 Configuración, chats y Agents.
- 🔴 Thresholds, alternativas, reglas, colas, errores, auditoría y operación.
- 🟠 Logs y recovery consultables.

## 16. 🟡 QA / Observabilidad — 74% — peso 6%
**Estado:** regresión avanzada; métricas parciales. **Responsable:** JJ + Codex.
- 🟢 Builds, matching, payment constraints, Cynthia y Delivery regression.
- 🟢 Intelligence E2E, Listener, recovery y colas.
- 🟠 Health y métricas; 🟡 logs y alertas técnicas.
- 🟡 `e2e-hard-no-orillas` / “Lo más nuevo posible” sigue independiente.

## 17. 🔴 Expansión — 15% — peso 2%
**Estado:** planificada. **Responsable:** JJ.
- 🟠 Durango inicial.
- 🔴 Otras ciudades/estados/inmobiliarias, múltiples Agents/cuentas.
- 🔴 Gran inventario, alto volumen, otras fuentes, producto comercial y SaaS.

## 18. 🔴 Futuro / I+D — 5% — peso 2%
**Estado:** visión. **Responsable:** JJ.
- 🔴 Preferencias, personalización y ranking adaptativo.
- 🔴 Aprendizaje, predicción y detección de oportunidades.
- 🔴 Asistentes, automatización, seguimiento inteligente e IA de mercado.

---

# Hitos cerrados

- ✅ Backend Azure, Azure SQL y Blob privado operativos.
- ✅ Mapa con carga por viewport, property focus y prioridad sobre geolocalización.
- ✅ Fotos legacy recuperadas/validadas 72/72, límite público de 40 y `150_34.jpg` confirmado.
- ✅ Metadata moderna de #187 identificada: 14 activas + 1 inactiva; payloads aún pendientes.
- ✅ Bug #184 corregido: precio máximo estricto y propiedades sin precio verificable descartadas.
- ✅ Hard constraints, payment constraints y Cynthia baseline.
- ✅ Intelligence E2E, durable processing, replay y terminal ACK.
- ✅ Delivery Flow seguro y commit productivo `60a49eb`.
- ✅ Deploy 2026-09-18 sin rollback; Azure/central/fallback `0`; `WhatsAppProfile` preservado.
- ✅ Recovery 11/11 con `ALTERNATIVA_PARA_REVISION`, 0 Delivery, 0 WhatsApp.
- ✅ Colas finales processing/downstream/delivery: 0/0/0.
- ✅ Backup: `C:\Users\jenny\AppData\Local\RSMaps\RadarAgent\Backups\pre-60a49eb-20260918-002253`.

# Pendientes conocidos

- 🔴 Reply/quote contaminando solicitudes; `TextoPropio` / `TextoCitado`; timestamp real.
- 🔴 Deduplicación cross-chat/cross-post y `SolicitudAccionable`.
- 🟡 Política definitiva de alternativas.
- 🟡 Tolerancia 80% terreno/construcción y otros mínimos comerciales.
- 🟠 Multi-cuenta en matching y escalabilidad del mapa.
- 🟡 Validación productiva de #147, viewport/pan/zoom e índice SQL 54.
- 🟡 Auditoría general de metadata moderna contra Blob.
- 🔴 Estadísticas, inteligencia de mercado y CRM.
- 🔴 Fotos modernas de #187 no localizadas.
- 🟡 `e2e-hard-no-orillas` / “Lo más nuevo posible”, independiente del Delivery Flow.

# Reglas de actualización

Al cerrar un bloque, incorporar una idea, cambiar prioridad, descubrir un bug o completar un deploy, actualizar: subbloque, bloque, avance simple, ponderado, núcleo operativo, Ruta activa, hitos y pendientes. Los pesos deben sumar 100% y los colores derivar del porcentaje.

# Ruta activa — cierre

1. Reply / Quote. 2. Captura estructurada. 3. `TextoPropio` / `TextoCitado`. 4. Regresiones. 5. `SolicitudAccionable`. 6. Deduplicación durable cross-chat. 7. Política final de alternativas. 8. Hard constraints comerciales. 9. Escalabilidad de mapa. 10. Analytics / Inteligencia de mercado.
