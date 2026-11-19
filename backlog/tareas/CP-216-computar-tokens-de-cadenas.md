---
id: CP-216
titulo: Contabilizar las cadenas contra el límite diario de tokens
estado: pendiente
prioridad: P2
hito: F2
depende_de: [CP-207]
tipo: code-light
complejidad: media
duracion_min: 60
sensibilidad: public
---

## Objetivo

Contabilizar las cadenas contra el límite diario de tokens.

## Evidencia

Con solo el límite diario configurado, la admisión de una cadena consulta el gasto ordinario mientras sus llamadas incrementan el contador de cadenas. Con un tope de 100, cuatro llamadas de 100 dejaron el gasto en 400 y se seguía admitiendo otra. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [ ] Con límite diario de 100 y sin cupo separado para cadenas, una llamada de cadena que consume 100 impide admitir la siguiente ese día. Cubrir también el gasto mixto de llamadas ordinarias y cadenas.
- [ ] Verificar los casos de cupo de cadenas configurado, reinicio diario e interlocución conforme a la política de CP-207.
