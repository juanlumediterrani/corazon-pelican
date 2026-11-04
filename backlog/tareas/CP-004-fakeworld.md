---
id: CP-004
titulo: FakeWorld para probar el bucle completo sin juego
estado: hecha
prioridad: P1
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
fecha: 2026-11-04
---

## Objetivo

Un mundo simulado en `CorazonPelican.Tests` que produce observaciones, ejecuta un cuerpo falso y ofrece una mente falsa configurable, con reloj virtual.

## Contexto

ADS §13. Es la base de R-04, R-05, R-11 y de casi todas las pruebas de fases 2–5. Voyager demostró que un `FakeBridge` con `virtual_time()` evita dependencias del juego vivo.

## Alcance

- `FakeWorld`: entidades con identidad estable, posiciones, luz, clima, hora de juego y reloj real virtual (modo vanilla ~7 s/10 min y modo Pelican 1:1).
- `FakeBody`: aplica acciones del vocabulario con duración simulada.
- `FakeMind`: rápida, lenta (latencia configurable), caída y respuestas guionizadas.
- Constructores de escenarios legibles (`Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9)`).

## Criterios de aceptación

- [x] Un test recorre un día simulado completo en < 2 s de tiempo real. Evidencia: `FakeWorldTests.Un_dia_vanilla_completo_se_simula_en_menos_de_dos_segundos` (6:00 → 2:00 vanilla, 3 360 observaciones, con jugador y NPC deambulando); la suite entera tarda ~0,2 s.
- [x] La mente lenta y la caída se pueden inyectar sin hilos reales ni `Thread.Sleep`. Evidencia: `FakeMind.Lenta/Caida/Rapida(guion)`; `Mente_lenta_responde_tras_su_latencia_en_tiempo_virtual` (30 s virtuales en < 1 s reales), `Mente_caida_recibe_pero_nunca_responde`, `Mente_guionizada_responde_lo_que_dice_el_guion`; `grep Thread CorazonPelican.Tests/Simulacion` vacío.
- [x] El mismo escenario con la misma semilla produce la misma secuencia de observaciones. Evidencia: `La_misma_semilla_da_la_misma_secuencia_de_observaciones` (y otra semilla da otra), `Construir_dos_veces_da_mundos_independientes`.

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 75/75, `python3 -m pytest -q` 40 passed.

## Notas de cierre

- En Core: `Sensorio/Observacion.cs` (lo que rellenará CP-101) y `Tiempo/Reloj.cs` (`IReloj`, `ModoReloj`, `HoraJuego`).
- En tests (`Simulacion/`): `RelojVirtual`, `FakeMind`, `FakeBody`, `FakeWorld`, `Escenario`.
- `FakeBody` distingue acciones instantáneas, con duración y continuas (`dibujar`), y emite `PlanTerminado` al acabar la última acción con duración o empezar una continua; no lo emite si el plan se bloquea. Es la base del hito de intención (CP-201) y una propuesta para CP-302, no una decisión sobre el cuerpo real.
- Duraciones simuladas: ~533 ms por casilla, 20 casillas por cambio de ubicación, 1 s sentarse. Las casillas del escenario son inventadas.

## Fuera de alcance

- Adaptador real `ObservadorJuego` (CP-101).
