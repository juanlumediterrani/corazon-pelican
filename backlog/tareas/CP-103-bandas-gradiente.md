---
id: CP-103
titulo: Bandas de detalle foco/mención/periferia/ausente
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-102]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-03]
---

## Objetivo

Asignar cada entidad a una banda según los umbrales del perfil.

## Contexto

GSS §3.3. Consecuencia buscada: una Leah absorta puede no enterarse de quien llega.

## Alcance

- `Bandas.cs` con histéresis opcional para evitar parpadeo entre bandas en observaciones consecutivas.

## Criterios de aceptación

- [ ] `GradienteDetalleTests`: entidades bajo periferia no llegan a la escena (R-03).
- [ ] Con histéresis activada, una entidad oscilando ±0,01 en torno a un umbral no cambia de banda en cada observación.
- [ ] Umbrales leídos del perfil; ninguno codificado en Core.

## Fuera de alcance

- Redacción de la prosa (CP-104).
