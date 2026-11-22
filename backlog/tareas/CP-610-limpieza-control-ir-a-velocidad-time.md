---
id: CP-610
titulo: Limpieza de control tras bloqueo de AccionIrA, velocidad residual de micro-wandering y saltos de reloj de prueba
estado: hecha
fecha: 2026-11-22
prioridad: P3
hito: F6
depende_de: [CP-303, CP-601]
tipo: code-light
complejidad: baja
duracion_min: 75
sensibilidad: public
proyecto_destino: ambos (Corazón Pelican + Stardew Valley MORPG)
---

## Objetivo

Corregir las tres fricciones de control físico detectadas entre `CuerpoNpc` / `ReclamacionNpc` y `Pelican4Ever` (`WorldLifeModule` / `RealTimeClockModule`) durante la prueba en vivo de hoy.

## Contexto

Hallazgos concretos en la auditoría y en la sesión en vivo (`CorazonDev_454975123`):
1. **Velocidad `npc.speed = 1` residual:** `WorldLifeModule` asigna `npc.speed = 1` durante *micro-wandering* (`!NPCSchedule`) y solo restaura `2` en `isExternallyControlled` si `controller == null || controller.NPCSchedule`. `ReclamacionNpc.Reclamar` no resetea `npc.speed = 2` ni limpia el controlador previo, por lo que una ruta de `CuerpoNpc` (`!NPCSchedule`) puede quedar caminando a mitad de velocidad si coincide con un micro-paso.
2. **Atasco de `CorazonPelican.Control` al expirar `AccionIrA`:** en la sesión real, `AccionIrA` reclamó a Leah en `LeahHouse@{X:2 Y:4}` a las `2550` (`EstaEnAnimacionActividad`), expiró los `180_000 ms` sin llegar a lanzar ruta nativa (`fin_intencion` falló en reformulación local), y `CorazonPelican.Control` siguió puesto hasta el cierre impidiendo también que `CatchUpSchedule` actuara.
3. **Saltos manuales de debug (`p4e_time` / `time`):** cuando un desarrollador salta el reloj con `p4e_time` / `time` en Pelican 4Ever mientras Leah tiene `CorazonPelican.Control`, `CatchUpSchedule` la omite en silencio (correcto en juego real, pero desconcertante al probar en desarrollo si no se avisa o libera antes).

## Alcance

- **Corazón Pelican (`ReclamacionNpc.cs` / `Cuerpo.cs` / `MotorHabitante.cs`):**
  - En `ReclamacionNpc.Reclamar`, restablecer `npc.speed = 2` y limpiar `npc.controller` / `npc.Halt()` cuando proceda si el controlador previo no es propio.
  - Revisar por qué `AccionIrA` en `LeahHouse@{X:2 Y:4}` en animación nocturna no salió de `esperandoSalidaAnimacion` ni liberó `CorazonPelican.Control` al expirar/fallar `fin_intencion` sin plan alternativo.
- **Pelican 4Ever (`RealTimeClockModule.cs` · `TASK-2604`):**
  - En `CatchUpVillagerSchedules` / `p4e_time` (`time`), registrar en el resumen de consola cuántos NPCs externos fueron omitidos por `CorazonPelican.Control` (p. ej. `27 aldeanos recolocados, 1 omitido por control externo: Leah`), o añadir flag opcional de debug si se desea forzar prueba.

## Criterios de aceptación

- [x] Test en `CorazonPelican.Tests` (FakeWorld + núcleo): tras expirar `AccionIrA` por bloqueo o por salida de animación fallida sin nuevo plan en el pulso de `fin_intencion`, la intención degrada a rutina y retira `CorazonPelican.Control`. (`LimpiezaControlCP610Tests.AccionIrA_bloqueada_sin_reemplazo_libera_control_tras_tiempo_max_respuesta_CP610`, `419/419` superados; `MaxEsperaSalidaAnimacionMs = 2_000` en `AccionIrA` y degradación por `bloqueoSinReemplazoDesdeMs` en `MotorHabitante`)
- [x] `ReclamacionNpc.Reclamar` garantiza velocidad estándar (`speed = 2`) frente a micro-wandering previo (`speed = 1`). (`ReclamacionNpc.Reclamar` restablece `npc.speed = 2` y limpia controladores `!NPCSchedule` previos)
- [x] `Pelican4Ever`: en `p4e_time` / `CatchUpVillagerSchedules`, si algún NPC se omite por `CorazonPelicanControlKey`, se informa explícitamente en el log de consola SMAPI. (`RealTimeClockModule.CatchUpVillagerSchedules`, `1828/1828` superados)

## Fuera de alcance

- Cambiar las reglas de `CP-601` en operación normal 1:1 (en juego real, `CatchUpSchedule` y `WorldLifeModule` deben seguir respetando `CorazonPelican.Control`).
