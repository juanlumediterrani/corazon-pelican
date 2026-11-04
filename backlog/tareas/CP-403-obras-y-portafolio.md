---
id: CP-403
titulo: ObrasModule · portafolio persistido y obras colocables como muebles
estado: pendiente
prioridad: P2
hito: F4
depende_de: [CP-015, CP-107, CP-401]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
reglas: [R-10]
---

## Objetivo

Cada obra terminada existe como mueble decorativo regalable, con su textura reconstruida al cargar la partida.

## Contexto

GSS §6.2–6.3, ADS §9. Formato de entrada `Data/Furniture` validado en CP-015.

## Alcance

- `POST obras/{obra_id}/sprite` (solo `en_curso`, idempotente por id + hash + época).
- Portafolio en `EstadoPersonaje` con PNG en base64 y tope de tamaño.
- Reconstrucción de texturas e inyección en `Data/Furniture` al cargar; invalidación de caché.

## Criterios de aceptación

- [ ] `MemoriaConPartidaTests` ampliado: obra creada y no guardada desaparece al recargar; obra guardada reaparece con el mismo hash.
- [ ] Superar `TamanoMaxMemoriaKB` rechaza la obra con motivo, sin corromper el estado.
- [ ] En vivo: colgar una obra de Leah, guardar, cargar → sigue colgada con su textura.

## Fuera de alcance

- Clientes multijugador (CP-404).
