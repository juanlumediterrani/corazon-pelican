---
id: CP-209
titulo: Cadenas de pensamiento en los hitos, empezando por despertar y acostarse
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-201, CP-204, CP-206, CP-212]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Leah empieza y cierra cada día pensando en varios pasos encadenados: por la mañana, qué trae de ayer y qué quiere hoy; por la noche, qué ha pasado y qué significa.

## Contexto

GSS §4.1 y ADS §6. Cualquier hito con `forma: cadena` en el perfil abre una cadena; `despertar` y `acostarse` siempre. Cada cadena es una sola pulsión (con plaza reservada en el presupuesto, CP-202) que la mente resuelve en varias llamadas, cada una con la salida de la anterior, sin actuar en el mundo entre ellas. La reflexión diario → recuerdos (CP-206) vive en la cadena de acostarse.

- **Despertar:** ayer y lo pendiente (recuerdos, compromisos) → cómo amanece y cómo se siente → qué quiere hoy: **la agenda del día** (qué entradas de su horario mantiene, cambia o sustituye, y dónde encajan sus intenciones). Después, cada hito la reequilibra (pensamiento en cascada, CP-306).
- **Acostarse:** qué pasó hoy (diario) → qué significó (recuerdos) → qué piensa de quienes vio (relaciones) → qué queda para mañana.

En vanilla el guardado suele llegar antes de que termine la cadena de acostarse: su respuesta se aplica al llegar (misma época) y se persiste en el siguiente guardado.

## Alcance

- Pulsos `despertar` y `acostarse` con su contexto (diario del día; recuerdos y compromisos pendientes).
- Pasos de cada cadena definidos como plantillas de la mente, configurables por perfil y por hito.
- Una respuesta final por cadena: agenda del día e intenciones (despertar), o memoria, relaciones y compromisos (acostarse).
- Orden: el despertar espera a que se haya aplicado el acostarse anterior.

## Criterios de aceptación

- [x] Emitir `acostarse`, avanzar al amanecer y entregar tarde la reflexión en la misma época. Comprobar que se aplica una sola vez antes de resolver el despertar. Una reflexión de otra época sigue recibiendo `epoca_obsoleta`. Caso reproducido en [CP-905](CP-905-revision-argon.md). Evidencia: [`ApiMotor.EstaSuperadaPorPulsoPosterior`](../../CorazonPelican.Core/Api/ApiMotor.cs) + [`CadenasHitosTests.Reflexion_tardia_de_acostarse_tras_despertar_en_misma_epoca_se_aplica_una_vez_y_otra_epoca_da_epoca_obsoleta`](../../CorazonPelican.Tests/Atencion/CadenasHitosTests.cs) + [`_ordenar_prioridad_pulsos`](../../mente/cliente_motor.py).
- [x] pytest con FakeLLM: cada cadena hace los pasos definidos en orden, pasando la salida de cada uno al siguiente, y envía una sola respuesta. Evidencia: [`mente/cadenas.py`](../../mente/cadenas.py) + `test_cadena_despertar_ejecuta_tres_pasos_en_orden_y_envia_una_respuesta` y `test_plantillas_configurables_por_perfil_y_por_hito` en [`mente/tests/test_cadenas.py`](../../mente/tests/test_cadenas.py).
- [x] Todo recuerdo de la cadena de acostarse cita al menos una entrada del diario del día. Evidencia: `test_cadena_acostarse_cuatro_pasos_y_todo_recuerdo_cita_episodios_diario` en [`mente/tests/test_cadenas.py`](../../mente/tests/test_cadenas.py).
- [x] FakeWorld: con el guardado antes de la respuesta de acostarse, los recuerdos se aplican al llegar y aparecen tras el siguiente guardado y recarga. Evidencia: `FakeWorld_guardado_antes_de_respuesta_acostarse_persiste_tras_siguiente_guardado_y_llega_al_despertar` en [`CorazonPelican.Tests/Atencion/CadenasHitosTests.cs`](../../CorazonPelican.Tests/Atencion/CadenasHitosTests.cs).
- [x] El despertar devuelve una agenda que solo contiene entradas del horario de hoy (o planes de intención) y cuyos cambios pasan R-16. Evidencia: `Despertar_valida_agenda_solo_con_horario_de_hoy_o_planes_permitidos_y_rechaza_violacion_r16` en [`CorazonPelican.Tests/Atencion/CadenasHitosTests.cs`](../../CorazonPelican.Tests/Atencion/CadenasHitosTests.cs).
- [x] pytest con FakeLLM: partir de un episodio propio sin interlocutor ni compromiso. Seguir su identificación desde la reflexión hasta el contexto que recibe el despertar siguiente y comprobar que la agenda admite continuar o revisar lo iniciado. Evidencia: `test_episodio_propio_trazable_hasta_despertar_siguiente_con_continuidad_en_agenda` en [`mente/tests/test_cadenas.py`](../../mente/tests/test_cadenas.py).
- [x] El despertar de un día con un compromiso pendiente lo coloca en la agenda o lo descarta de forma explícita. Evidencia: [`MotorHabitante.EmitirPulsoDesdeDisparo`](../../CorazonPelican.Core/Atencion/MotorHabitante.cs) + [`estrechar_esquema`](../../mente/modelos.py) + `test_despertar_con_compromiso_pendiente_lo_coloca_o_descarta_en_la_agenda` en [`mente/tests/test_cadenas.py`](../../mente/tests/test_cadenas.py).
- [x] Si un paso falla, la cadena termina con lo obtenido hasta entonces y lo registra; Leah sigue su rutina. Evidencia: `test_fallo_en_un_paso_termina_cadena_con_lo_obtenido_y_no_altera_la_rutina` en [`mente/tests/test_cadenas.py`](../../mente/tests/test_cadenas.py).

Verificación al cerrar (sin juego vivo):
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 345, Con error: 0, Omitido: 0, Total: 345.
- `python3 -m pytest -q` → 102 passed.

## Fuera de alcance

- Hitos con cadena propios de otros personajes (p. ej. el cierre de la tienda de Pierre).
