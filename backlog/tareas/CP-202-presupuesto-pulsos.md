---
id: CP-202
titulo: Presupuesto de pulsos aplicado por el motor, por modo de reloj, con reserva para las cadenas
estado: hecha
fecha: 2026-11-19
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

- [x] `PresupuestoPulsionesTests`: con una plaza ordinaria libre y una extra, dos compromisos consecutivos gastan una de cada tipo, en ese orden. El tercero da lugar a una solicitud de prórroga en CP-502. Una interlocución sigue pasando sin gastar de ninguna bolsa (R-04).
- [x] Ni deriva ni hitos ordinarios gastan de la bolsa extra. Ningún compromiso consume las dos reservas de cadena. El cambio de día restablece cada saldo y los gastos de un personaje no afectan al otro.
- [x] La mente no tiene ningún campo en el contrato que modifique el presupuesto (test sobre el esquema).
- [x] Con `ModoReloj` vanilla y pelican se aplica el presupuesto del modo correspondiente.

## Evidencia

- `PresupuestoPulsionesTests.Compromisos_gastan_primero_ordinario_luego_extra_y_despues_piden_prorroga` prueba el orden de gasto, el aviso `CompromisoSinCupo` con el id para CP-502 y la interlocución con ambos saldos agotados.
- `Una_deriva_y_un_hito_denegados_no_gastan_reservas_ni_reinician_la_atencion` protege la reserva y verifica que el integrador y la marca de la última pulsión conservan su estado al denegar la emisión.
- `Guardar_y_cargar_conserva_plazas_y_cadenas_y_otro_personaje_tiene_su_propio_saldo` restaura los contadores serializados y comprueba el nuevo día. `Un_compromiso_pendiente_de_ayer_no_descuenta_una_plaza_de_hoy` cubre el límite entre días.
- `El_esquema_rechaza_campos_de_presupuesto_en_la_respuesta` cubre cuatro campos en la respuesta y en la intención. `El_bucle_elige_el_cupo_del_modo_del_reloj` prueba ambos modos con FakeWorld.
- `extra_compromisos` es opcional por modo y vale 1 por defecto, valor inicial hasta medir en CP-208. `El_perfil_anterior_que_omite_la_bolsa_tiene_una_plaza_y_cero_es_configurable` cubre los perfiles existentes, el cero y el rechazo de un valor negativo.
- Compilación sin despliegue ni zip: 0 advertencias y 0 errores. `dotnet test` con ambas opciones desactivadas: 165 superadas, 0 omitidas. `python3 -m pytest -q`: 43 superadas.

Guardar la instantánea con la partida corresponde a CP-107. Tramitar la prórroga tras el aviso corresponde a CP-502 y CP-509.

## Fuera de alcance

- Presupuesto de tokens de la mente (CP-207). Calibrar valores (CP-208).
