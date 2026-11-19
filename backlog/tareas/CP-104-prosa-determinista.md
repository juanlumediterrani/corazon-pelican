---
id: CP-104
titulo: Plantillas de prosa deterministas, escena y prosa de acción, con escenas doradas
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-103, CP-105]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
reglas: [R-01, R-02]
---

## Objetivo

Convertir observación y bandas en prosa de segunda persona sin LLM, con el apéndice seco de hechos y acciones.

## Contexto

GSS §3.4 y [ADR-0002](../../docs/adr/0002-percepcion-por-plantillas.md). Riesgo de monotonía perceptiva (GSS §14): las plantillas necesitan variantes, elegidas de forma determinista (semilla = día + entidad), no aleatoria.

## Alcance

- `PlantillasProsa` por categoría y banda, cargadas del perfil (`leah.plantillas.json`).
- `GeneradorEscena` (escena + apéndice), `GeneradorAccion` (frases breves para el búfer) y plantillas de la frase de huella de atención (GSS §4.4).
- El apéndice da pares id — prosa con la misma prosa que usa la escena (ADS §3.3).
- Escenas doradas `.escena.txt` para observaciones fijas (otoño en la pasarela, lluvia, noche, jugador acercándose).
- `cp_escena leah` y `GET escena/{personaje}` usan el mismo generador.

## Criterios de aceptación

- [ ] `EscenaDeterministaTests`: misma entrada → misma salida byte a byte (R-01).
- [ ] `EscenaSinAlucinacionTests`: toda referencia de la escena y el apéndice existe en la observación (R-02).
- [ ] Todo lugar o entidad nombrado en la escena aparece en el apéndice con su id y la misma prosa.
- [ ] Al menos 3 variantes por plantilla de foco; dos días distintos con la misma observación no producen el mismo texto.
- [ ] Las escenas doradas se regeneran con una variable de entorno documentada.

## Fuera de alcance

- Revisión de calidad con un día completo (CP-108).
