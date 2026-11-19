---
id: CP-206
titulo: Recuperación de memoria y reflexión del día
estado: pendiente
prioridad: P2
hito: F2
depende_de: [CP-204, CP-107, CP-211]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

La mente elige qué recordar en cada pulso y sabe condensar el diario del día en recuerdos; la cadena de acostarse (CP-209) usa esa reflexión.

## Contexto

GSS §9, ADS §6 (*Generative Agents*, Park et al., 2023). Una sola verdad: el motor custodia y persiste; la mente solo tiene caché de trabajo.

## Alcance

- `recuperacion.py`: recencia × importancia × relevancia (sin embeddings remotos obligatorios; relevancia por solapamiento de entidades y léxico como base).
- `reflexion.py`: diario → recuerdos con `origen_diario[]`, enviados como `memoria.reflexiones` según CP-211.

## Criterios de aceptación

- [ ] pytest: con 200 entradas de diario, la recuperación cabe en el presupuesto de tokens configurado.
- [ ] Todo recuerdo creado cita al menos una entrada de diario existente. Cubrir el recorrido completo de una referencia desde el pulso hasta la respuesta validada y el estado guardado. Usar los episodios identificados y el validador de CP-211 antes de guardar. Mantener la lectura de recuerdos anteriores en prosa. CP-210 reutiliza estas referencias para el humor.
- [ ] La mente no escribe ningún fichero de memoria propio fuera de la caché descartable (test de sistema de ficheros).

## Fuera de alcance

- Relaciones con impresiones (se alimentan en F5).
