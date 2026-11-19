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

El motor asigna cada pulsión al cupo que le corresponde. Reserva despertar y acostarse, da a los compromisos una bolsa extra diaria y deja pasar la interlocución con cualquier saldo (P-2, P-12).

## Contexto

GSS §4.2 y §7 (decisión P-2). La cadencia emerge de los disparadores y se mide (CP-208); el presupuesto es un tope de seguridad, no un ritmo. Como un día de juego dura ~14 min reales en vanilla y ~20 h en Pelican, el perfil tiene un valor por modo de reloj (`ModoReloj` en la configuración). Los valores son provisionales hasta medir.

## Alcance

- `PresupuestoPulsos.cs`: límite por día de juego y modo, con `reservados_cadenas` para despertar y acostarse (cada cadena cuenta como una pulsión).
- Ampliar esquema de perfil, DTO, validador y perfil de Leah con el cupo extra por modo. Documentar el valor por defecto y cubrir perfiles anteriores que omiten el campo.
- Contadores por personaje y día que CP-107 pueda restaurar. Informar a CP-502 cuando un compromiso agota ambas vías de admisión.

## Criterios de aceptación

- [ ] `PresupuestoPulsionesTests`: con una plaza ordinaria libre y una extra, dos compromisos consecutivos gastan una de cada tipo, en ese orden. El tercero da lugar a una solicitud de prórroga en CP-502. Una interlocución sigue pasando sin gastar de ninguna bolsa (R-04).
- [ ] Ni deriva ni hitos ordinarios gastan de la bolsa extra. Ningún compromiso consume las dos reservas de cadena. El cambio de día restablece cada saldo y los gastos de un personaje no afectan al otro.
- [ ] La mente no tiene ningún campo en el contrato que modifique el presupuesto (test sobre el esquema).
- [ ] Con `ModoReloj` vanilla y pelican se aplica el presupuesto del modo correspondiente.

## Fuera de alcance

- Presupuesto de tokens de la mente (CP-207). Calibrar valores (CP-208).
