---
id: CP-303
titulo: Reclamación, volver_a_rutina y degradación a la Leah vanilla
estado: pendiente
prioridad: P1
hito: F3
depende_de: [CP-011, CP-302]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-11]
---

## Objetivo

Pase lo que pase con la mente, Leah vuelve a su rutina canónica y nunca queda congelada, muda o rota (pilar 5).

## Contexto

GSS §5.3 y §11. Mecanismo validado en CP-011.

## Alcance

- `ReclamacionNpc.cs` con `modData["CorazonPelican.Control"]`.
- `volver_a_rutina` al tramo vigente.
- Degradación automática: mente caída, sin presupuesto, `cp_mente off`, error en un módulo.

## Criterios de aceptación

- [ ] `DegradacionVanillaTests`: con la mente apagada, FakeWorld no registra ninguna acción del motor sobre el NPC (R-11).
- [ ] Matar el proceso de la mente con Leah reclamada la devuelve a la rutina en ≤ 1 tramo de 10 min de juego.
- [ ] En vivo: `cp_mente off` a mitad de una intención devuelve a Leah a su horario.

## Fuera de alcance

- Integración con el *micro-wandering* de Pelican (CP-601).
