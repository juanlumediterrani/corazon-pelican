---
id: CP-207
titulo: Presupuesto de tokens, caché de prompt y telemetría de costes
estado: hecha
prioridad: P2
hito: F2
depende_de: [CP-204]
tipo: code-light
complejidad: baja
duracion_min: 90
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

La segunda barrera de coste: tokens por día real, con métricas de caché y coste en `costes.jsonl`.

## Contexto

ADS §6 y §12.2. Subordinado al presupuesto de pulsos del motor (CP-202). Las cadenas hacen varias llamadas por pulso: su coste se registra aparte.

## Alcance

- Contabilidad por llamada: tokens de entrada, caché, salida y coste estimado.
- Al agotarse, la mente responde solo a interlocución o deja que el motor degrade.

## Criterios de aceptación

- [x] `costes.jsonl` registra cada llamada con época, pulso y modelo. Evidencia: `TestRegistroCostesYPresupuesto.test_costes_jsonl_registra_epoca_pulso_modelo_cadenas_y_reintentos`, `test_reformulacion_tras_rechazo_motor_y_cadenas_con_presupuesto_separado` y `test_modelo_sin_ultimo_uso_estima_tokens_y_cache_en_registro_costes` en `mente/tests/test_costes.py` (incluye `timestamp_utc`, `epoca`, `pulso_id`, `motivo`, `es_cadena`, `modelo`, `tokens_entrada`, `tokens_cache`, `tokens_salida`, `coste_usd`, `en_cache`).
- [x] Con presupuesto agotado (FakeLLM), los pulsos no interlocutorios no llegan al modelo. Evidencia: `TestRegistroCostesYPresupuesto.test_presupuesto_agotado_bloquea_no_interlocutorios_y_permite_interlocucion` en `mente/tests/test_costes.py` (cubre `deriva`, `hito`, `pico`, `fin_intencion`, `compromiso` y `transicion` omitidos con `estado="presupuesto_agotado"` y sin incrementar `modelo.llamadas`, mientras `interlocucion` sí llega al modelo).
- [x] Informe de coste estimado por día de juego vanilla y por día Pelican con el modelo elegido. Evidencia: `TestInformeCosteDiario.test_estimar_coste_diario_vanilla_y_pelican` y `TestTarifasYCache.test_tokens_cache_reducen_el_coste_estimado` en `mente/tests/test_costes.py`.

Verificación final: `python3 -m pytest -q` (86 passed) y `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (316/316).

## Estimado diario vanilla vs Pelican

Parámetros base (`docs/notas-juego.md` §3.2 y §7, `REGLAS_SISTEMA` + `CorazonPelican/assets/personajes/leah.voz.md`): 5 pulsos ordinarios/día + 2 cadenas (`despertar` y `acostarse`, 4 llamadas/cadena = 8 llamadas) + interlocuciones estimadas (2 en vanilla ≈ 14 min reales; 6 en Pelican 1:1 = 1440 min reales), prefijo estable de 902 tokens con 85 % de acierto de caché, 250 tokens dinámicos de entrada y 200 tokens de salida por llamada (`generar_informe_coste_diario`).

| Modo | Modelo | Llamadas/día (ord/cad/inter) | Tokens totales | Con caché (USD/día) | Sin caché (USD/día) | USD / hora real |
|---|---|---|---:|---:|---:|---:|
| vanilla | `claude-sonnet-4-5` | 15 (5/8/2) | 20,280 | $0.0652 | $0.0968 | $0.2793 |
| pelican | `claude-sonnet-4-5` | 19 (5/8/6) | 25,688 | $0.0837 | $0.1227 | $0.0035 |
| vanilla | `gemini-2.5-flash` | 15 (5/8/2) | 20,280 | $0.0100 | $0.0127 | $0.0431 |
| pelican | `gemini-2.5-flash` | 19 (5/8/6) | 25,688 | $0.0128 | $0.0161 | $0.0005 |
| vanilla | `gpt-4o-mini` | 15 (5/8/2) | 20,280 | $0.0035 | $0.0044 | $0.0151 |
| pelican | `gpt-4o-mini` | 19 (5/8/6) | 25,688 | $0.0045 | $0.0056 | $0.0002 |

## Fuera de alcance

- Modelo local para reflejos.
