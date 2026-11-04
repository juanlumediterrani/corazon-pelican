---
id: CP-505
titulo: Convivencia con ValleyTalk · nunca dos voces para la misma persona
estado: pendiente
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

- [ ] UniqueID de ValleyTalk documentado con su versión y fuente (V-08).
- [ ] `ConvivenciaValleyTalkTests`: con el mod presente y Leah no listada, no se intercepta (R-12).
- [ ] La configuración de ValleyTalk no se lee ni se modifica.

## Fuera de alcance

- Integración con ValleyTalk.
