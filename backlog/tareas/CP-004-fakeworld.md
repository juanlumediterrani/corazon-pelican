---
id: CP-004
titulo: FakeWorld para probar el bucle completo sin juego
estado: pendiente
prioridad: P1
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
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

- [ ] Un test recorre un día simulado completo en < 2 s de tiempo real.
- [ ] La mente lenta y la caída se pueden inyectar sin hilos reales ni `Thread.Sleep`.
- [ ] El mismo escenario con la misma semilla produce la misma secuencia de observaciones.

## Fuera de alcance

- Adaptador real `ObservadorJuego` (CP-101).
