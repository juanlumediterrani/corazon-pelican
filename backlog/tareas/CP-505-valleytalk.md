---
id: CP-505
titulo: Convivencia con ValleyTalk · nunca dos voces para la misma persona
estado: hecha
fecha: 2026-11-22
prioridad: P3
hito: F5
depende_de: [CP-503]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-12]
---

## Objetivo

Si ValleyTalk está cargado y el usuario no ha confirmado que lo desactivó para Leah, el motor no la intercepta y avisa.

## Contexto

GSS §8.6, ADS §11.3, V-08. No se encontró el UniqueID publicado (mod de Nexus 30319, autor dandm1; [notas](../../docs/notas-juego.md) §9): hay que leerlo del `manifest.json` del mod instalado. Alternativa robusta: detectar por nombre/autor en `ModRegistry` además del ID.

## Alcance

- Detección por `Helper.ModRegistry` y clave `ValleyTalkDesactivadoPara`.
- Aviso de consola con instrucciones.

## Criterios de aceptación

- [x] UniqueID de ValleyTalk documentado con su versión y fuente (V-08). (`dandm1.ValleyTalk` v1.4.0, `Nexus:30319`, en `docs/notas-juego.md` §9)
- [x] `ConvivenciaValleyTalkTests`: con el mod presente y Leah no listada, no se intercepta (R-12). (`ConvivenciaValleyTalkTests.Con_ValleyTalk_presente_y_Leah_no_listada_cede_a_vanilla_y_no_intercepta`)
- [x] La configuración de ValleyTalk no se lee ni se modifica. (`DialogoModule.EstaValleyTalkActivo` consulta únicamente `Helper.ModRegistry.GetAll()` y `CorazonConfig.ValleyTalkDesactivadoPara`)

## Fuera de alcance

- Integración con ValleyTalk.
