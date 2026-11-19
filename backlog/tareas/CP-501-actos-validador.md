---
id: CP-501
titulo: Actos de habla y validador «todos válidos o ninguno»
estado: pendiente
prioridad: P1
hito: F5
depende_de: [CP-107, CP-301]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-07, R-08]
---

## Objetivo

Una frase de Leah solo se muestra si todos sus actos son válidos ([ADR-0003](../../docs/adr/0003-actos-antes-de-la-frase.md)).

## Contexto

GSS §8.1–8.2, ADS §4.1.

## Alcance

- `Actos.cs`: `promete`, `regala`, `cita`, `enseña`, `rechaza`, `acepta_regalo`.
- `ValidadorActos.cs` con motivos en dos formas (`codigo` para el arnés, `prosa` para la mente) devueltos para reformular una vez.
- Orden de aplicación: intención → actos → memoria → frase.

## Criterios de aceptación

- [ ] `ActosAntesDeFraseTests`: un acto inválido entre varios válidos impide mostrar la frase (R-07).
- [ ] `ActoRegalaTests`: obra inexistente, ya regalada o en curso → inválido (R-08).
- [ ] `cita` con lugar fuera del léxico o fecha pasada → inválido.

## Fuera de alcance

- Interfaz de diálogo (CP-503).
