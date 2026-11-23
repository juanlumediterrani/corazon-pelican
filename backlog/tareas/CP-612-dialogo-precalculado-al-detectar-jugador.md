---
id: CP-612
titulo: Diálogo precalculado al detectar jugador (afirmación o pregunta con opciones y vuelta) sin esperas hardcoded
estado: hecha
fecha: 2026-11-23
prioridad: P1
hito: F6
depende_de: [CP-503, CP-507, CP-606]
tipo: code
complejidad: media
duracion_min: 90
sensibilidad: public
proyecto_destino: Corazón Pelican
---

## Objetivo

Eliminar el mensaje de espera `"Un segundo... estaba pensando en algo."` y el bucle en cliente multijugador cuando `mente` aún no ha respondido, sustituyéndolo por un flujo de diálogo **precalculado al detectar al jugador** (afirmación o pregunta con opciones y *vuelta* precalculada para cada opción), respetando el idioma de juego (`Pulso.Idioma`) y devolviendo la *vuelta* (último mensaje) si se vuelve a hablar el mismo día, o cediendo limpiamente a vanilla si no hay diálogo precalculado.

## Alcance

- **Contrato (`schemas/pulso.schema.json`, `schemas/respuesta.schema.json`, `Pulso.cs`, `Respuesta.cs`, `ValidadorMenciones.cs`):**
  - `Pulso.Idioma` opcional y `Respuesta.Dialogo` (`DialogoPreparado`: `para`, `tipo`=`afirmacion|pregunta`, `texto`, `opciones[]` con `id`, `texto`, `respuesta` como vuelta).
- **Motor y diálogo (`Disparadores.cs`, `GestorDialogo.cs`, `MotorHabitante.cs`, `ProtocoloMultijugador.cs`):**
  - `Disparadores.DetectarAnticipacion` dispara también cuando un granjero entra directamente dentro de `Banda.Mencion` y omite a quien ya tiene diálogo listo o consumido en el día (`DebeOmitirAnticipacion`).
  - `GestorDialogo` almacena el `DialogoPreparado` del día, resuelve al instante sin bloquear (`Afirmacion`, `Pregunta`, `RepetirUltimaVuelta`, `Ninguno`) y registra la elección y *vuelta* por `(día, jugador)`.
  - `ProtocoloMultijugador` serializa `ClaveDialogoPreparadoModData` / `ClaveUltimoDialogoJugador` en `npc.modData` y `MsgDialogoConsumido` cliente → anfitrión.
- **Adaptador vivo (`DialogoModule.cs`, `HabitanteModule.cs`) y `mente/` (`ensamblador.py`):**
  - `DialogoModule` elimina la burbuja de espera en castellano; muestra afirmación o `createQuestionDialogue` con las opciones y su *vuelta* en 0 ms, repite la *vuelta* dentro del mismo día, o devuelve `true` a vanilla si no hay diálogo precalculado.
  - Sincronización de idioma (`LocalizedContentManager` + `MsgClienteIdioma`) hacia `mente/ensamblador.py`.

## Criterios de aceptación

- [x] `DialogoPrecalculadoCP612Tests`: verifica disparo al detectar entrada en rango, entrega instantánea de pregunta con opciones en idioma del juego (`en`), registro de la *vuelta*, repetición de la *vuelta* en el mismo día (tanto en anfitrión como en instantánea de cliente) y cesión limpia a vanilla sin diálogo preparado.
- [x] Esquema JSON y Python (`schemas/` + `mente/`): `test_mente.py` y `ContratoTests` en verde con `dialogo` precalculado e `idioma`.
- [x] Compilación limpia (`0` advertencias, `0` errores), `422/422` tests C# y `pytest` en verde.
