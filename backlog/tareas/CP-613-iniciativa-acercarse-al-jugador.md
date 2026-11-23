---
id: CP-613
titulo: Iniciativa esporádica del personaje para acercarse al jugador y entregarle el diálogo preparado
estado: pendiente
fecha: 2026-11-23
prioridad: P2
hito: F6
depende_de: [CP-302, CP-303, CP-612]
tipo: code
complejidad: media
duracion_min: 120
sensibilidad: public
proyecto_destino: Corazón Pelican
---

## Objetivo

Permitir que un personaje gobernado (p. ej. Leah), al detectar a un jugador y tener listo su diálogo precalculado (`CP-612`), pueda **elegir por iniciativa propia** acercarse físicamente al jugador para decirle ese mensaje o plantearle la pregunta con opciones, funcionando de manera **esporádica y no muy recurrente**.

## Contexto y comportamiento esperado

- En `CP-612`, al detectar al jugador entrando en su entorno, el motor precalcula el diálogo (`afirmacion` o `pregunta` + `opciones` + `respuesta`/vuelta) para ofrecerlo al instante si el jugador decide pulsar hablar.
- Esta característica añade la iniciativa inversa: el propio personaje, en su respuesta de `mente` (o como elección propia validada por el motor), puede decidir **tomar la iniciativa** y caminar hacia el jugador detectado para iniciar la conversación espontáneamente.
- **No debe resultar invasivo ni frecuente:** es un suceso excepcional que refuerza la sensación de autonomía del personaje («hoy vino ella a decirme algo»), no un comportamiento de seguimiento constante.

## Alcance

- **Contrato y mente (`schemas/comun.schema.json`, `schemas/respuesta.schema.json`, `ensamblador.py`):**
  - Nueva acción/iniciativa cerrada (p. ej. `acercarse_y_hablar` / `acercarse_a(objetivo)` o campo explícito de iniciativa en `dialogo` cuando la mente elige iniciar el encuentro) solo ofrecida en `acciones_disponibles` cuando las guardas de frecuencia lo permitan.
- **Guardas de recurrencia esporádica (`CorazonPelican.Core` + perfil en `assets/personajes/*.json`):**
  - Parámetros configurables por personaje (enfriamiento largo entre iniciativas —p. ej. mínimo N días de juego o cooldown prolongado—, apertura mínima/relación, y tope estricto de iniciativas).
  - Nunca ofrecer la acción si el personaje ya consumió diálogo hoy con ese jugador, si hay otro diálogo activo, durante eventos/festivales, o si ya está ejecutando otra intención prioritaria.
- **Ejecución física en `CuerpoNpc` / `DialogoModule`:**
  - Ruta nativa (`PathFindController`) hacia casilla adyacente al jugador dentro del mismo mapa con tiempo límite/abandono limpio si el jugador cambia de ubicación o se aleja.
  - Al llegar a rango de conversación, entregar al instante el `DialogoPreparado` (`Afirmacion` o `Pregunta` + `vuelta`, host y cliente vía mensaje dirigido si el objetivo es *farmhand*), registrar `ultimoMensaje` del día para evitar repetición, y liberar control/reclamación de inmediato.

## Criterios de aceptación

- [ ] Guardas de enfriamiento/frecuencia en `CorazonPelican.Core` demuestran en tests (`FakeWorld`) que `acercarse` solo se habilita de forma esporádica y nunca repite en el mismo día ni tras haberse consumido el diálogo.
- [ ] Si el jugador sale del mapa o se aleja demasiado durante el trayecto, el cuerpo cancela el acercamiento y libera `CorazonPelican.Control` sin quedarse persiguiendo al jugador.
- [ ] Al alcanzar al jugador con diálogo preparado válido, abre inmediatamente la afirmación o pregunta con opciones y registra la *vuelta* igual que `CP-612` (incluyendo soporte *farmhand* en multijugador).
- [ ] `dotnet build` (`0` advertencias), `dotnet test` y `pytest` en verde sin regresar pruebas existentes.

## Fuera de alcance

- Perseguir al jugador entre distintos mapas o teletransportarse para alcanzarlo.
- Usar esta iniciativa cuando la mente está caída/deshabilitada o cuando `ValleyTalk` controla al personaje.
