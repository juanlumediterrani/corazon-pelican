---
id: CP-501
titulo: Actos de habla y validador «todos válidos o ninguno»
estado: hecha
prioridad: P1
hito: F5
depende_de: [CP-107, CP-301]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-07, R-08]
fecha: 2026-11-19
---

## Objetivo

Una frase de Leah solo se muestra si todos sus actos son válidos ([ADR-0003](../../docs/adr/0003-actos-antes-de-la-frase.md)).

## Contexto

GSS §8.1–8.2, ADS §4.1.

## Alcance

- `Actos.cs`: `promete`, `regala`, `cita`, `enseña`, `rechaza`, `acepta_regalo`.
- `ValidadorActos.cs` con motivos en dos formas (`codigo` para el arnés, `prosa` para la mente) devueltos para reformular una vez.
- Orden de aplicación: intención → actos → memoria → frase.

## Criterios de aceptación

- [x] `ActosAntesDeFraseTests`: un acto inválido entre varios válidos impide mostrar la frase (R-07). Evidencia: `ActosAntesDeFraseTests.Un_acto_invalido_junto_a_varios_validos_rechaza_todo_el_lote_impide_frase_y_no_muta_estado`, `Promete_sin_apertura_o_plazo_pasado_o_sin_tema_es_invalido`, `Ensena_valida_apertura_y_estado_de_obra`, `Rechaza_sin_motivo_y_acepta_regalo_sin_regalo_y_acto_desconocido_se_rechazan`, y orden `intención → actos → memoria → frase` en `Orden_intencion_actos_memoria_frase_se_respeta_y_detiene_ante_fallo`.
- [x] `ActoRegalaTests`: obra inexistente, ya regalada o en curso → inválido (R-08). Evidencia: `ActoRegalaTests.Obra_inexistente_se_rechaza_con_motivo_y_bloquea_la_frase`, `Obra_regalada_en_curso_o_boceto_se_rechaza_con_motivo`, `Regala_sin_apertura_suficiente_o_con_destinatario_ausente_se_rechaza`, `Obra_disponible_con_apertura_y_destinatario_presente_se_regala_y_muestra_la_frase`, `Regalar_dos_veces_la_misma_obra_en_un_lote_rechaza_todo_el_lote`.
- [x] `cita` con lugar fuera del léxico o fecha pasada → inválido. Evidencia: `ActosAntesDeFraseTests.Cita_con_lugar_fuera_del_lexico_o_area_pendiente_o_fecha_pasada_es_invalida`.

Verificación final: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 0 advertencias / 0 errores; `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 338/338; `python3 -m pytest -q` 94 passed.

## Notas de cierre

- `CorazonPelican.Core/Dialogo/Actos.cs`: `TiposActo` (`promete`, `regala`, `cita`, `ensena`, `rechaza`, `acepta_regalo`), `EstadosObra` (`en_curso`, `terminada`, `colgada`, `regalada`, `boceto`, `descartada`, `vendida`), `EtapasTurno`, `AperturaPerfil.TramoPara`, `ContextoValidacionActos`, `ResultadoValidacionActos` y `ResultadoTurnoDialogo`.
- `CorazonPelican.Core/Dialogo/ValidadorActos.cs`: `ValidadorActos` (validación integral «todos válidos o ninguno» con motivos `{codigo, campo, prosa}`) y `AplicadorTurnoDialogo` (validación previa completa + aplicación atómica ordenada `intención → actos → memoria → frase` creando compromisos `promete`/`cita` y marcando obras `regalada` con `Destinatario`).

## Fuera de alcance

- Interfaz de diálogo (CP-503).
