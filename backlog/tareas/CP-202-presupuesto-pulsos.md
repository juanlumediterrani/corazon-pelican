---
id: CP-202
titulo: Presupuesto de pulsos aplicado por el motor, por modo de reloj, con reserva para las cadenas
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-201]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-04]
---

## Objetivo

Ninguna pulsión sale si el presupuesto está agotado, salvo interlocución y las dos cadenas del día, que tienen plaza reservada.

## Contexto

GSS §4.2 y §7 (decisión P-2). La cadencia emerge de los disparadores y se mide (CP-208); el presupuesto es un tope de seguridad, no un ritmo. Como un día de juego dura ~14 min reales en vanilla y ~20 h en Pelican, el perfil tiene un valor por modo de reloj (`ModoReloj` en la configuración). Los valores son provisionales hasta medir.

## Alcance

- `PresupuestoPulsos.cs`: límite por día de juego y modo, con `reservados_cadenas` para despertar y acostarse (cada cadena cuenta como una pulsión).
- Contador persistido con la partida (CP-107) y reinicio por día de juego.

## Criterios de aceptación

- [ ] `PresupuestoPulsionesTests`: agotado el presupuesto, solo pasan interlocución, despertar y acostarse (R-04).
- [ ] Una deriva o un hito ordinario no pueden consumir las plazas reservadas a las cadenas.
- [ ] La mente no tiene ningún campo en el contrato que modifique el presupuesto (test sobre el esquema).
- [ ] Con `ModoReloj` vanilla y pelican se aplica el presupuesto del modo correspondiente.

## Fuera de alcance

- Presupuesto de tokens de la mente (CP-207). Calibrar valores (CP-208).
