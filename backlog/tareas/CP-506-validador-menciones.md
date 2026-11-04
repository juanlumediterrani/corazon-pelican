---
id: CP-506
titulo: Validador de menciones · la mente no nombra lo que no existe para Leah
estado: pendiente
prioridad: P2
hito: F5
depende_de: [CP-501, CP-206]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

Medir y hacer cumplir el criterio «sin alucinación de mundo»: 100 % de las entidades que nombra la mente existen en su escena o su memoria.

## Contexto

GSS §8.5 y §12. R-02 cubre la escena que genera el motor; nada cubría lo que dice la mente. Decisión P-4: regla **R-15** en la GSS §13, con esta tarea como guarda.

## Alcance

- Extraer de la frase y la intención nombres propios, ids del léxico y nombres de objetos conocidos del juego.
- Contrastarlos con escena + memoria recuperada; tratar como acto inválido (reformular una vez) o como aviso según configuración.
- Métrica en telemetría.

## Criterios de aceptación

- [ ] Tests: «la garza del muelle» sin garza en escena ni memoria → detectado; «Ana» presente → aceptado.
- [ ] La tasa de menciones no ancladas aparece por día en `rechazos.jsonl`.
- [ ] `MencionesAncladasTests` verde: R-15 entra en vigor.

## Fuera de alcance

- Entidades genéricas sin nombre («alguien», «el viento»).
