---
id: CP-602
titulo: IntegracionPelicanModule · sucesos del servidor como percepción
estado: hecha
fecha: 2026-11-22
prioridad: P3
hito: F6
depende_de: [CP-101]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

Con Pelican presente, `player.connected`, festivales y similares entran al sensorio como sucesos.

## Contexto

ADS §11.1. Hoy existe `GET /api/v1/host/events?since=N` en Pelican (`docs/host-api.md`). Preferible a medio plazo una API de mod vía `Helper.ModRegistry.GetApi`, sin HTTP dentro del mismo proceso.

## Alcance

- Lectura en hilo de fondo y conversión a entidades/sucesos del sensorio.
- Decidir HTTP o API de mod (y, si es API de mod, abrir la tarea en Pelican).

## Criterios de aceptación

- [x] Sin Pelican configurado, el módulo no arranca ni registra errores. (`IntegracionPelicanModule.Initialize` sale sin arrancar el worker ni escribir en consola cuando `PelicanApiUrl` está vacío; `SucesosPelicanTests.Sin_Pelican_configurado_no_se_habilita_integracion`)
- [x] Un jugador que se conecta aparece como suceso en la siguiente escena relevante. (`SucesosPelicanTests.Jugador_que_se_conecta_en_Pelican_aparece_como_suceso_en_la_siguiente_escena_relevante`)
- [x] Ningún HTTP saliente se hace desde el hilo principal. (`IntegracionPelicanModule` sondea `GET /api/v1/host/events?since=N` exclusivamente dentro de `Task.Run` en `BucleSondeoAsync` y vuelca los eventos en `ColaSucesosPelican`)

## Fuera de alcance

- Reloj (el motor solo lee `Game1.timeOfDay`).
