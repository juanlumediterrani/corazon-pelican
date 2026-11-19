---
id: CP-204
titulo: Mente en Python que recibe pulsos y escribe pensamientos (sin cuerpo)
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-203, CP-205]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

El proceso `mente/` sigue el bucle de ADS §6 y deja en telemetría qué pensaría Leah en cada pulso, sin que el motor aplique nada todavía.

## Contexto

ADS §6. Modelos: un modelo grande para pensamiento y diálogo; el prompt de sistema (voz + reglas) estable para la caché del proveedor.

## Alcance

- `cliente_motor.py` (long-poll, cursor + época, reanudación), `ensamblador.py`, `modelos.py`.
- `estrechar(respuesta.schema.json, pulso)`: esquema del pulso con listas cerradas de acciones, lugares, entidades, hitos y obras sacadas del propio pulso (ADS §6, P-11).
- Salida estructurada contra ese esquema, con `pensamiento` primero; un reintento si no valida.
- Reformulación a partir de la `prosa` de los motivos de rechazo.
- El motor guarda cada `pensamiento` en la telemetría y como entrada del diario.
- `FakeLLM` para tests.

## Criterios de aceptación

- [x] pytest: con `FakeLLM`, un pulso produce una respuesta válida contra el esquema. Evidencia: `TestCicloMente.test_fakellm_produce_respuesta_valida_y_registra_pensamiento` y `TestHumoLoopbackHttp.test_humo_httpx_contra_servidor_loopback_real` en `mente/tests/test_mente.py`.
- [x] pytest: una salida inválida provoca un reintento y luego un rechazo registrado, nunca un envío inválido. Evidencia: `TestCicloMente.test_salida_invalida_provoca_un_reintento_y_luego_rechazo_sin_enviar_al_motor` y `TestCicloMente.test_salida_invalida_se_recupera_en_el_reintento` en `mente/tests/test_mente.py`.
- [x] pytest: el esquema del pulso valida como subconjunto del general, y rechaza un lugar o una ref que no están en el pulso. Evidencia: `TestEsquemaEstrechado.test_estrechar_esquema_valida_como_subconjunto_del_general`, `test_rechaza_lugar_y_ref_que_no_estan_en_el_pulso` y `test_cierra_acciones_hitos_obras_y_episodios_diario` en `mente/tests/test_mente.py`.
- [x] pytest con FakeLLM: tras un rechazo, el prompt de reformulación contiene la prosa del motivo. Evidencia: `TestCicloMente.test_tras_rechazo_del_motor_el_prompt_de_reformulacion_contiene_la_prosa_del_motivo` en `mente/tests/test_mente.py`.
- [x] Tras reiniciar la mente, retoma desde el cursor sin reprocesar pulsos; con época nueva, reinicia el cursor. Evidencia: `TestCicloMente.test_reinicio_retoma_cursor_sin_reprocesar_y_epoca_nueva_reinicia_a_cero` en `mente/tests/test_mente.py` (cubre reanudación desde `since`, reinicio por `409` y reinicio por `GET /salud`).
- [x] El prefijo del prompt (sistema + voz) es idéntico entre dos pulsos distintos (test de caché). Evidencia: `TestEnsambladorYModelos.test_prefijo_sistema_identico_byte_a_byte_entre_pulsos` en `mente/tests/test_mente.py`.

Verificación final: `python3 -m pytest -q` (71 passed) y `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (296/296).

## Notas de cierre

- `mente/modelos.py`: `estrechar_esquema` (con alias `estrechar`), `validar_respuesta_estrechada`, `ModeloMente`, `FakeLLM`, `ConfigModelo`, `ModeloProveedorConfigurado` y `crear_modelo` (sin exigir claves reales en tests).
- `mente/ensamblador.py`: `ensamblar_prompt`, `construir_prefijo_sistema`, `PromptEnsamblado` y `cargar_voz`, manteniendo el prefijo `sistema` (reglas + voz) invariante byte a byte entre pulsos.
- `mente/cliente_motor.py`: `ClienteMotor` (`httpx`), `AlmacenCursor`, `RegistroTelemetria` y `CicloMente` (prioridad interlocución primero, validación local con 1 reintento, reformulación con `prosa` tras rechazo del motor, reinicio de cursor y descarte de caché ante cambio de época).
- `mente/tests/test_mente.py`: suite de pruebas unitarias, de contrato estrechado y de humo HTTP contra servidor loopback real.

## Fuera de alcance

- Recuperación de memoria y reflexión (CP-206).
