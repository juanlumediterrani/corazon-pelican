---
id: CP-301
titulo: Intención con ciclo de vida, pesos de atención y validador de precondiciones
estado: hecha
prioridad: P1
hito: F3
depende_de: [CP-105, CP-203]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-06, R-13]
fecha: 2026-11-19
---

## Objetivo

Las intenciones de la mente se validan contra la observación vigente, modulan la saliencia y terminan siempre en un estado final.

## Contexto

GSS §5.1, ADS §4.1. La capa de intención de la saliencia (CP-102) deja de valer 1.

## Alcance

- `Intencion.cs`: propuesta → validada → activa → cumplida | bloqueada | caducada | sustituida; rechazada.
- `ValidadorIntencion.cs`: lugares del léxico alcanzables, acciones del vocabulario, plazos futuros.
- Fin de intención dispara pulsión (CP-201).

## Criterios de aceptación

- [x] `ValidadorIntencionTests`: lugar inexistente, plazo pasado y acción fuera del perfil se rechazan con motivo (R-06), cada uno con `codigo`, `campo` y `prosa` legible por el personaje. Evidencia: `ValidadorIntencionTests.Lugar_inexistente_o_fuera_de_lugares_ofrecidos_se_rechaza_con_motivo`, `Lugar_con_area_pendiente_no_es_alcanzable`, `Sentarse_en_lugar_no_sentable_se_rechaza`, `Plazo_pasado_o_actual_se_rechaza_con_motivo`, `Accion_fuera_del_perfil_o_inventada_se_rechaza_con_motivo`, `Mirar_objetivo_ausente_se_rechaza_y_objetivo_en_escena_se_acepta`, `Plan_vacio_y_argumentos_faltantes_o_invalidos_se_rechazan`.
- [x] `EsquemaIntencionTests` en verde también contra el validador de Core (R-13). Evidencia: `PasoPlan.CamposExtra` (`[JsonExtensionData]`) + `ValidadorIntencion` y `ValidadorCambiosRutina`; verificado en `EsquemaIntencionTests.Plan_con_coordenadas_es_invalido_en_esquema_y_en_core` y `EsquemaIntencionTests.Plan_solo_con_lexico_es_valido_en_esquema_y_en_core`.
- [x] Una intención activa con plazo vencido pasa a caducada y emite pulsión de fin. Evidencia: `IntencionPersonaje.CaducarSiVencida` transiciona a `EstadoIntencion.Caducada` y llama a `Disparadores.FinIntencion(Id, "caducada", observacion, ahoraMs)`; verificado en `ValidadorIntencionTests.Intencion_activa_con_plazo_vencido_pasa_a_caducada_y_emite_pulsion_de_fin` (y ciclo de vida completo en `Ciclo_de_vida_cubre_todos_los_estados_y_bloquea_transiciones_invalidas_o_posteriores_a_terminal`, más modulación real de `Saliencia` en `Pesos_atencion_de_la_intencion_activa_modulan_realmente_la_saliencia`).

Verificación final: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 0 advertencias / 0 errores; `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 311/311; `python3 -m pytest -q` 71 passed.

## Notas de cierre

- `CorazonPelican.Core/Intencion/Intencion.cs`: `EstadoIntencion`, `IntencionPersonaje` con máquina de estados, comparación de plazos contra `(Observacion.Dia, Observacion.Hora)`, `CaducarSiVencida` con emisión en `Disparadores.FinIntencion`, y `PesosAtencionActivos` integrado con `ContextoSaliencia` (`Saliencia.cs`: `ConIntencion` y `DesdePerfil`).
- `CorazonPelican.Core/Intencion/ValidadorIntencion.cs`: `ValidadorIntencion` y `ResultadoValidacionIntencion` validando contra `Observacion`, `PerfilPersonaje`, `LugaresOfrecidos` (R-14), `Acciones` + formatos/tipos de `PasoPlan` (R-13) y `ValidadorCambiosRutina` si se aporta agenda.
- `CorazonPelican.Core/Contrato/Comunes.cs` y `Rutina.cs`: captura de campos extra (`CamposExtra`) en `PasoPlan` para rechazar coordenadas/rutas también si llegan directamente a Core.

## Fuera de alcance

- Ejecución física (CP-302).
