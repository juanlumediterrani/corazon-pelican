---
id: CP-911
titulo: Intenciones propias potenciadas con un ejecutor al estilo del arnés de Stardew Voyager
estado: dormida
prioridad: profunda
hito: Futuro
depende_de: [CP-302, CP-306]
tipo: planning
complejidad: alta
duracion_min: 120
sensibilidad: public
---

## Objetivo

Que las intenciones particulares del personaje (las que no salen de su rutina) puedan ser más ambiciosas sin que la mente elija casillas ni rutas.

## Contexto

Dirección futura (GSS §14). Referencias concretas de Voyager en [notas](../../docs/notas-juego.md) §10. El vocabulario v1 (GSS §5.2) basta para ir, sentarse y dibujar. Para intenciones más ricas hacen falta funciones parecidas a las que Stardew Voyager ya resolvió para su granjero: acciones registradas con precondiciones, parámetros y postcondiciones observables (`@skill`); ruta y colisión nativas (SV-025); «¿puedo actuar aquí y qué me falta?» (SV-026); resolver en vez de rechazar (SV-028); y una lista de objetivos priorizados que un ejecutor determinista cumple con presupuesto (refactor v4, `DayRunner`). Voyager opera un granjero desde Python por un puente; aquí el cuerpo es un NPC en el host, en C#, así que se comparte el diseño, no el código, y nunca el contexto entre mentes (pilar 3).

## Alcance (a concretar al despertarla)

- Revisar qué piezas de Voyager aplican a un NPC y cuáles no (inventario, herramientas, energía).
- Ejecutor determinista de la agenda del día con objetivos y presupuesto, encima del vocabulario de acciones.
- Nuevas acciones con contrato de pre/postcondiciones y su guarda en FakeWorld.

## Criterios de aceptación

- [ ] Por definir al pasar a `pendiente`.

## Fuera de alcance

- Cualquier canal entre la mente del personaje y el agente de Voyager.
