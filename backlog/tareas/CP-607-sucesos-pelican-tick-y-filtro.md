---
id: CP-607
titulo: IntegracionPelican · drenar sucesos en tick ordinario, filtrar ruido de admin y soportar varios personajes
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F6
depende_de: [CP-602]
tipo: code
complejidad: media
duracion_min: 90
sensibilidad: public
---

## Objetivo

Corregir el consumo de `SucesosPelican` en `HabitanteModule` y filtrar en `ConversorSucesosPelican` los eventos internos de administración del servidor para que solo entren percepciones diegéticas relevantes y no se pierdan entre personajes.

## Contexto

Auditoría de `CP-602`:
1. `HabitanteModule.OnUpdateTicked` llama a `this.observador.Observar(npc, npc.currentLocation)` directamente sin aplicar `this.context.SucesosPelican.AplicarAObservacion(obs)`; solo `ObservarPersonaje` (usado por `CuerpoNpc`, `GET /api/v1/observacion` y `OnDayEnding`) drena la cola. Así, durante el paseo/rutina ordinaria los sucesos de Pelican se quedan retenidos sin llegar a `ProcesarPaso`.
2. `ColaSucesosPelican.AplicarAObservacion` hace `TryDequeue`, de modo que cuando haya más de un personaje gobernado, el primero consumiría los sucesos dejando sin nada a los demás.
3. `ConversorSucesosPelican.Convertir` convierte cualquier evento de `Pelican4Ever.Core/Admin/HostEventLog.cs` con `Summary` no vacío (`checkpoint.created`, `economy.daily`, `rollover.started`, `host.lockdown`, `host.test.command`...) en `EntidadObservada` con `Distancia = 1.0` y `Visible = true` en la escena de Leah.

## Alcance

- Drenar/distribuir `SucesosPelican` en cada ciclo de `HabitanteModule.OnUpdateTicked` sin duplicar por tick y preservando entrega para todos los `MotoresHabitante` activos (cursor por personaje o snapshot por paso).
- Lista blanca / filtro explícito en `ConversorSucesosPelican` (`player.connected`, `player.disconnected`, `festival.*` y eventos sociales acordados), ignorando telemetría de infraestructura (`checkpoint.*`, `backup.*`, `economy.*`, `rollover.*`, `host.lockdown*`, `host.degraded`, `host.restart.*`, `host.test.command`, `host.command`, `host.render.*`, `player.pairing.*`, `player.blacklisted*`, etc.).
- Valorar migrar la lectura HTTP opcional de `IntegracionPelicanModule` a consulta directa en memoria vía `IPelicanApi` (`CP-602` / ADS §11.1) si se añade el método en Pelican, manteniendo compatibilidad HTTP.

## Criterios de aceptación

- [x] Tests en `CorazonPelican.Tests` (`SucesosPelicanTests`): eventos de `checkpoint.created`, `economy.daily`, `host.lockdown` y similares devuelven `null` (no entran en la escena), mientras `player.connected` y `festival.*` entran tanto en tick ordinario como para múltiples personajes. (`SucesosPelicanTests.Filtra_ruido_administrativo_y_entrega_sucesos_a_multiples_personajes_CP607`, `416/416` superados)
- [x] Verificación de que `HabitanteModule.OnUpdateTicked` entrega el suceso en el siguiente paso de observación sin esperar a `CuerpoNpc` ni `OnDayEnding`. (`HabitanteModule.OnUpdateTicked` y `ObservarPersonaje` aplican `SucesosPelican.AplicarAObservacion(obs, id)` por personaje sin perder sucesos entre múltiples motores)

## Fuera de alcance

- Exponer canales privados de chat de moderación o logs de seguridad a NPCs.
