---
id: CP-006
titulo: Partida de desarrollo separada y medición de los lugares de Leah (V-09) 🎮
estado: pendiente
prioridad: P2
hito: F0
depende_de: [CP-003]
tipo: analysis
complejidad: baja
duracion_min: 90
sensibilidad: public
vivo: si
---

## Objetivo

Una partida dedicada al desarrollo y las áreas reales de cada lugar del léxico de Leah, medidas en vivo.

## Contexto

ADS §16 V-09. El horario real de Leah da ubicaciones y casillas de partida para varios lugares ([notas](../../docs/notas-juego.md) §3.1); la pasarela del lago no está en él y hay que medirla. Voyager (SV-030) separó la partida de desarrollo de la de campaña; aquí conviene igual, con copias de guardado como escenarios (Leah en la pasarela en otoño, con lluvia, de noche…).

## Alcance

- Partida DEV con copia de seguridad y escenarios con nombre.
- Comando de depuración que imprime ubicación, casilla y orientación del jugador para medir áreas.
- Áreas reales para los lugares del perfil inicial; lugares por estación si el horario de Leah lo pide.

## Criterios de aceptación

- [ ] `leah.json` valida sin avisos de áreas pendientes.
- [ ] Cada área se ha comprobado con Leah o el jugador de pie dentro y la casilla es transitable.
- [ ] Los escenarios se restauran con un comando documentado en `AGENTS.md`.

## Fuera de alcance

- Lugares de otros personajes.
