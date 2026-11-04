---
id: CP-304
titulo: Reflejos del perfil en menos de 100 ms
estado: pendiente
prioridad: P2
hito: F3
depende_de: [CP-302]
tipo: code-light
complejidad: baja
duracion_min: 90
sensibilidad: public
---

## Objetivo

Reglas del perfil que producen gestos inmediatos sin la mente: levantar la vista, saludar, cerrar el cuaderno si alguien mira con pocos corazones.

## Contexto

GSS §2.1 y §6.4 (apertura según relación).

## Alcance

- Reglas declarativas en el perfil: condición sobre bandas/relación/estado → acción breve.
- Enfriamiento por regla para no repetir gestos.

## Criterios de aceptación

- [ ] FakeWorld: el reflejo se aplica en el mismo paso de observación en que se cumple la condición.
- [ ] Con 0–2 corazones y el jugador en foco mientras dibuja, Leah guarda el cuaderno (regla de apertura).
- [ ] Ningún reflejo consume presupuesto de pulsos.

## Fuera de alcance

- Modelo local de reflejos con etiquetas cerradas (opcional, sin tarea).
