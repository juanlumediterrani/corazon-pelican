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

GSS §9.4, ADS §9. P-6 fija la persistencia del diario en F1. CP-403 ampliará el mismo estado con obras en F4. Véase el [registro de decisiones](../../docs/decisiones.md). Pelican tiene el patrón de `ServerWorldState` con migraciones.

## Alcance

- `PersistenciaModule` con `WriteSaveData("CorazonPelican.Estado", …)` y guarda `Context.IsMainPlayer`.
- `version_esquema` y migradores puros en Core con test por versión.
- Día del contador y gasto por personaje de cupo ordinario, reservas y bolsa extra de compromisos (P-12, CP-202).

## Criterios de aceptación

- [ ] `MemoriaConPartidaTests` (FakeWorld): guardar, añadir episodios, recargar sin guardar → los episodios nuevos no están.
- [ ] Un estado v1 serializado a mano migra a la versión actual en un test.
- [ ] En FakeWorld, dos días con episodios distinguibles recuperan las entradas guardadas en su día y orden. La comprobación en DEV corresponde a CP-108.

## Fuera de alcance

- Portafolio y texturas (CP-403).
