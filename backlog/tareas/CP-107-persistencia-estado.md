---
id: CP-107
titulo: Estado del personaje persistido con la partida, con versión y migraciones
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-106]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-10]
---

## Objetivo

`EstadoPersonaje` se escribe en `Saving` y se lee en `SaveLoaded`, solo en el host, de modo que memoria y mundo nunca divergen.

## Contexto

GSS §9.4, ADS §9. La ADS sitúa R-10 en la fase 4, pero el diario existe desde la fase 1; se propone persistirlo ya (propuesta P-6, pendiente en [decisiones](../../docs/decisiones.md)) y ampliar el estado con obras en CP-403. Pelican tiene el patrón de `ServerWorldState` con migraciones.

## Alcance

- `PersistenciaModule` con `WriteSaveData("CorazonPelican.Estado", …)` y guarda `Context.IsMainPlayer`.
- `version_esquema` y migradores puros en Core con test por versión.
- Contador de pulsos del día y día del contador.

## Criterios de aceptación

- [ ] `MemoriaConPartidaTests` (FakeWorld): guardar, añadir episodios, recargar sin guardar → los episodios nuevos no están.
- [ ] Un estado v1 serializado a mano migra a la versión actual en un test.
- [ ] En vivo: guardar en DEV, cerrar, cargar → `cp_estado` muestra el mismo número de entradas de diario.

## Fuera de alcance

- Portafolio y texturas (CP-403).
