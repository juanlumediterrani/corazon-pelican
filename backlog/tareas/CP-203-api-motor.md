---
id: CP-203
titulo: API loopback del motor con long-poll, cola al hilo principal, idempotencia y época
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-002, CP-005]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
reglas: [R-05]
fecha: 2026-11-19
---

## Objetivo

La mente habla con el motor por `http://127.0.0.1:25560/api/v1/` sin que el juego espere nunca.

## Contexto

ADS §1 (principios), §5. Pelican 4Ever ya resuelve el patrón: `HttpListener` en `AdminApiModule` y `MainThreadActionQueue` en Core; se reutiliza el diseño, no se comparte el puerto. Época (P-1) definida en CP-002.

## Alcance

- `GET salud`, `GET pulsos?since&espera_ms`, `POST respuestas`, `GET memoria/{p}`, `GET eventos?since`, `GET escena/{p}`.
- Solo escucha en loopback; token opcional.
- Toda mutación encolada y drenada en `UpdateTicked`.
- Idempotencia de `POST respuestas` por (`epoca`, `basada_en`); descarte de respuestas obsoletas (ADS §4.1).

## Criterios de aceptación

- [x] `CuerpoNoBloqueaTests` (FakeWorld + mente lenta de 30 s): el tick del motor nunca tarda más de 2 ms (R-05). Evidencia: `CuerpoNoBloqueaTests.Mente_http_lenta_de_30s_no_bloquea_el_tick_del_motor_ni_el_cuerpo`.
- [x] Al drenar una respuesta con reflexión, comprobar sus orígenes sobre el pulso correspondiente con el validador de CP-211 antes de aplicar cambios. Evidencia: `ApiMotor.ProcesarRespuestaEnHiloPrincipal` invoca `ValidadorOrigenDiario.Comprobar(pulso, respuesta)` antes del aplicador; verificado en `ApiMotorTests.Post_respuestas_con_reflexion_invalida_o_ajena_se_rechaza_antes_de_aplicar_cambios`.
- [x] Repetir un `POST respuestas` devuelve el mismo resultado sin reaplicar. Evidencia: idempotencia por `(epoca, basada_en)` en `ApiMotor`; verificado en `ApiMotorTests.Repetir_post_respuestas_devuelve_el_mismo_resultado_sin_reaplicar`.
- [x] Tras simular una recarga (época nueva), un pulso con `id` repetido no se confunde con el anterior. Evidencia: `ApiMotor.IniciarEpoca` reinicia la secuencia de pulsos y califica cursores e idempotencia por época; `GET /api/v1/pulsos` con época obsoleta devuelve `409`; verificado en `ApiMotorTests.Cambio_de_epoca_no_colisiona_con_mismo_id_de_pulso_y_get_pulsos_con_epoca_vieja_da_409` y `ApiMotorTests.Long_poll_despierta_al_emitir_pulso_y_corta_con_409_si_cambia_la_epoca`.
- [x] Un intento de conexión desde una IP no loopback es rechazado (test de configuración del listener). Evidencia: `ServidorApiMotor.ConstruirPrefijoLoopback`, `ValidarPrefijoLoopback` y `EsPeticionLoopbackValida`; verificado en `ApiMotorTests.Configuracion_del_listener_rechaza_direcciones_o_hosts_no_loopback`, `ApiMotorTests.Validador_de_prefijo_rechaza_prefijos_inseguros`, `ApiMotorTests.Validacion_de_ip_y_cabeceras_rechaza_origenes_e_ips_no_loopback` y `ApiMotorTests.Token_bearer_se_valida_cuando_esta_configurado_y_origin_externo_recibe_403`.

Verificación final: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 0 advertencias / 0 errores; `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 259/259; `python3 -m pytest -q` 44 passed.

## Notas de cierre

- `CorazonPelican.Core/Api/`: `MainThreadActionQueue.cs` (cola entre hilos con abandono por timeout y `Drenar`), `ModelosApi.cs` (DTOs de respuesta y error) y `ApiMotor.cs` + `ServidorApiMotor.cs` (`HttpListener` loopback en `http://127.0.0.1:{puerto}/api/v1/`, long-poll asíncrono, token Bearer opcional, idempotencia, obsolescencia y validación con `ValidadorEpoca` y `ValidadorOrigenDiario`).
- `CorazonPelican/Modules/ApiModule.cs`, `ModContext.cs` y `ModEntry.cs`: ciclo SMAPI (`GameLaunched`, `SaveLoaded` con guarda `Context.IsMainPlayer` y generación de ULID de época, `UpdateTicked` para drenar la cola, `ReturnedToTitle` y `Dispose`) y actualización de `EstadoApi`.
- El arranque real de `ApiModule` dentro del proceso SMAPI vivo queda para la sesión 🎮 correspondiente (AGENTS.md regla 4).

## Fuera de alcance

- `POST obras/{id}/sprite` (CP-403).
