---
id: CP-104
titulo: Plantillas de prosa deterministas, escena y prosa de acción, con escenas doradas
estado: hecha
fecha: 2026-11-19
prioridad: P1
hito: F1
depende_de: [CP-103, CP-105]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
reglas: [R-01, R-02]
---

## Objetivo

Convertir observación y bandas en prosa de segunda persona sin LLM, con el apéndice seco de hechos y acciones.

## Contexto

GSS §3.4 y [ADR-0002](../../docs/adr/0002-percepcion-por-plantillas.md). Riesgo de monotonía perceptiva (GSS §14): las plantillas necesitan variantes, elegidas de forma determinista (semilla = día + entidad), no aleatoria.

## Alcance

- `PlantillasProsa` por categoría y banda, cargadas del perfil (`leah.plantillas.json`).
- `GeneradorEscena` (escena + apéndice), `GeneradorAccion` (frases breves para el búfer) y plantillas de la frase de huella de atención (GSS §4.4).
- El apéndice da pares id — prosa con la misma prosa que usa la escena (ADS §3.3).
- Escenas doradas `.escena.txt` para observaciones fijas (otoño en la pasarela, lluvia, noche, jugador acercándose).
- `cp_escena leah` y `GET escena/{personaje}` usan el mismo generador.

## Criterios de aceptación

- [x] `EscenaDeterministaTests`: misma entrada → misma salida byte a byte (R-01). Evidencia: `Misma_entrada_produce_misma_salida_byte_a_byte` (UTF-8 byte a byte) y `Prosa_de_accion_y_huella_son_deterministas_y_varian_por_dia`. **R-01 entra en vigor.**
- [x] `EscenaSinAlucinacionTests`: toda referencia de la escena y el apéndice existe en la observación (R-02). Evidencia: `Toda_referencia_en_escena_y_apendice_existe_en_la_observacion_en_recorrido_fakeworld`, `Periferia_usa_prosa_vaga_y_no_revela_el_nombre_propio`, `Referencias_en_bandas_ausentes_de_observacion_se_ignoran_sin_alucinar`, `Huella_sin_entidades_no_alucina_referencias` y `Pulso_creado_desde_generador_cumple_el_esquema_del_contrato`. **R-02 entra en vigor.**
- [x] Todo lugar o entidad nombrado en la escena aparece en el apéndice con su id y la misma prosa. Evidencia: `Lugar_nombrado_en_escena_aparece_en_apendice_con_su_id_y_misma_prosa` y aserciones sobre `Apendice.Entidades` / `Apendice.Lugares` en el recorrido FakeWorld.
- [x] Al menos 3 variantes por plantilla de foco; dos días distintos con la misma observación no producen el mismo texto. Evidencia: `Plantillas_de_leah_son_validas_y_tienen_al_menos_tres_variantes_por_foco` (4 variantes por categoría en `leah.plantillas.json`), `Validador_rechaza_categoria_de_foco_con_menos_de_tres_variantes` y `Dias_distintos_con_misma_observacion_producen_texto_distinto_en_todas_las_categorias`.
- [x] Las escenas doradas se regeneran con una variable de entorno documentada. Evidencia: `Escena_dorada_coincide_con_instantanea` sobre `otono-pasarela`, `lluvia-tarde`, `noche-en-el-lago` y `jugador-acercandose` en `CorazonPelican.Tests/Sensorio/EscenasDoradas/` con `CORAZON_ACTUALIZAR_ESCENAS=1` (documentada en `EscenasDoradas/README.md` y `EscenaDeterministaTests.cs`).

## Notas de cierre

- `PlantillasProsa.ElegirVariante` usa FNV-1a de 32 bits + desplazamiento por `dia` (nunca `Random` ni `string.GetHashCode`), rotando variantes en días consecutivos y manteniendo determinismo entre procesos.
- `GeneradorEscena.Generar` expone el punto de entrada compartido en Core (`EscenaGenerada` con `Escena`, `Apendice`, `TextoCompleto` y `CrearPulsoBase`) listo para `cp_escena` (CP-005) y `GET escena/{personaje}` (CP-203).

Verificación:

- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → 0 advertencias, 0 errores.
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Superado: 230, Con error: 0, Omitido: 0.
- `python3 -m pytest -q` → 44 passed.

## Fuera de alcance

- Revisión de calidad con un día completo (CP-108).
