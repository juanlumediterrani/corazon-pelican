---
id: CP-106
titulo: Búfer de prosa de acción, huella de atención y diario con consolidación temporal
estado: hecha
fecha: 2026-11-19
prioridad: P2
hito: F1
depende_de: [CP-104]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

El búfer guarda lo que llegó a la banda de mención del personaje y se consolida con más detalle para lo reciente; la huella resume a qué ha atendido desde el último pensamiento; el diario recibe los episodios.

## Contexto

GSS §4.4 y §9.2. Sin LLM resumidor: la consolidación es la fóvea/periferia aplicada al tiempo. Búfer y huella son propios de cada personaje: salen de su saliencia (CP-102), sus bandas (CP-103) y sus plantillas (CP-104). La huella hace que lo vivido entre hitos, aunque no dispare nada, llegue al siguiente pensamiento y tiña su perspectiva.

## Alcance

- `Bufer.cs`: inserción (solo desde la banda de mención), consolidación, tamaño máximo.
- `Huella.cs`: atención acumulada por entidad (tiempo en foco o mención × saliencia, con fuga), reiniciada en cada pulso; minutos de juego desde el último pensamiento; frase con las plantillas del perfil.
- Diario de episodios con id estable por personaje, fecha de juego, saliencia y entidades. Conservar el id en el guardado para las referencias de CP-211.
- Envejecimiento y poda con tope (`Consolidacion.cs`).

## Criterios de aceptación

- [x] El búfer consolidado nunca supera N entradas configurables y conserva siempre las de mayor saliencia de la última hora de juego. Evidencia: `Bufer_solo_admite_percepciones_desde_mencion_y_acciones_propias` y `Bufer_consolidado_respeta_tope_N_y_conserva_mayor_saliencia_de_la_ultima_hora` en `CorazonPelican.Tests/Atencion/BuferHuellaTests.cs`.
- [x] Ningún texto del búfer consolidado no aparecía literalmente en el búfer original (no se inventa). Evidencia: `Bufer_consolidado_no_inventa_texto_y_valida_en_pulso` en `CorazonPelican.Tests/Atencion/BuferHuellaTests.cs`.
- [x] El diario respeta `TamanoMaxMemoriaKB` con un test de un año simulado. Evidencia: `Diario_respeta_tamano_maximo_memoria_kb_tras_un_ano_simulado` (año de Stardew de 112 días con tope de 64 KB y año de 365 días con el tope por defecto de `CorazonConfig`, 512 KB) y `Poda_prioriza_episodios_de_hoy_y_mayor_saliencia_envejecida` en `CorazonPelican.Tests/Memoria/DiarioConsolidacionTests.cs`.
- [x] Huella: tras una tarde simulada mirando el lago con un paso breve del jugador, el lago encabeza la huella; con la misma tarde y los rasgos de otro perfil (p. ej. `persona` alto), el orden cambia. Evidencia: `Huella_tarde_mirando_el_lago_con_paso_breve_del_jugador_cambia_orden_segun_rasgos_del_perfil` en `CorazonPelican.Tests/Atencion/BuferHuellaTests.cs`.
- [x] La huella solo nombra entidades que estuvieron en la observación y se reinicia al emitir un pulso. Evidencia: `Huella_solo_nombra_entidades_observadas_en_mencion_o_foco_y_se_reinicia_al_emitir_pulso` y `Huella_aplica_fuga_temporal_parametrizable` en `CorazonPelican.Tests/Atencion/BuferHuellaTests.cs`.

## Notas de cierre

- `EstadoPersonaje` y `Consolidacion` (`CorazonPelican.Core/Memoria/`) asignan ids estables monotónicos por personaje (`ep-1`, `ep-2`, ...) sin reutilizar ids tras la poda (`Episodios_reciben_id_estable_unico_que_no_se_reutiliza_tras_podar`) y proyectan los episodios del día hacia `Pulso.EpisodiosDiario` cumpliendo `schemas/pulso.schema.json` y `ValidadorOrigenDiario` (`Proyeccion_hacia_episodios_diario_cumple_esquema_pulso_y_validador_cp211`).
- Verificación: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (0 advertencias, 0 errores), `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (296 superados, 0 con error, 0 omitidos) y `python3 -m pytest -q` (44 passed).

## Fuera de alcance

- Reflexión diario → recuerdos (CP-206).
