---
id: CP-401
titulo: Taller · generador enchufable y script de asset de la prosa a la PNG
estado: pendiente
prioridad: P2
hito: F4
depende_de: [CP-013, CP-002]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
---

## Objetivo

`taller/` convierte una decisión de obra (prosa) en una PNG del formato fijado en CP-013.

## Contexto

ADS §7 tras la decisión P-3. El generador principal y sus respaldos son los elegidos en CP-013.

## Alcance

- Interfaz `Generador` con la implementación principal (Space de Hugging Face o modelo de imagen) y los respaldos en orden, con revisión del modelo fijada en configuración.
- `taller/asset.py` endurecido desde el borrador de CP-013: recorte, reducción, cuantización a `paleta.py`, limpieza, marco y metadatos.
- Hash de contenido para la idempotencia de `POST obras/{id}/sprite`.
- Fallo, cola o límite del generador → siguiente respaldo o boceto, sin bloquear a la mente.

## Criterios de aceptación

- [ ] pytest: toda salida usa solo colores de la paleta y mide exactamente lo que dicta el formato.
- [ ] pytest: misma decisión + misma semilla → mismo hash.
- [ ] Un SVG que excede el máximo de elementos se rechaza antes de rasterizar.
- [ ] pytest con generador falso caído: se usa el respaldo y, sin respaldos, la obra queda como boceto.
- [ ] El prompt de imagen enviado al generador no contiene nombres de jugadores.

## Fuera de alcance

- Autocrítica (CP-402).
