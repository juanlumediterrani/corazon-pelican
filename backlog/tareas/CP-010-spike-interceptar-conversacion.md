---
id: CP-010
titulo: Spike V-01 · interceptar la conversación sin perder amistad, regalos ni misiones 🎮
estado: hecha
fecha: 2026-11-21
prioridad: P1
hito: S
depende_de: [CP-006]
tipo: analysis
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Saber exactamente dónde y cómo tomar la conversación con Leah sin romper nada de lo que el juego hace al hablar con ella.

## Contexto

Según el decompilado ([notas](../../docs/notas-juego.md) §2): `NPC.checkAction(Farmer, GameLocation)` es `virtual` y hace mucho más que mostrar diálogo. Dentro ocurren `tryToReceiveActiveObject` (regalos), `who.NotifyQuests(q => q.OnNpcSocialized(this))` (misiones como *Introductions*), la lógica de cónyuge y `grantConversationFriendship` (+20 diarios). Un prefijo Harmony que devuelva `false` sin más perdería la amistad diaria y el progreso de misiones.

## Alcance

- Prototipo en la partida DEV con un prefijo sobre `NPC.checkAction` que intercepta solo cuando vanilla mostraría diálogo ordinario.
- Tabla de casos: objeto regalable en mano, misión de socializar activa, evento en curso, Leah dormida/invisible, jugador sin poder moverse, cónyuge, festival.
- Decidir qué llama el motor por su cuenta (p. ej. `grantConversationFriendship`) y qué deja al juego.
- Comprobar en un cliente multijugador dónde se ejecuta `checkAction` (alimenta CP-404/CP-503).

## Criterios de aceptación

- [x] Nota `docs/spikes/v01-conversacion.md` con la tabla de casos, el punto de enganche elegido y fragmentos del decompilado citados (`docs/spikes/v01-conversacion.md`).
- [x] En vivo: tras una conversación interceptada, los puntos de amistad suben lo mismo que en vanilla (`[V01] Conversación interceptada npc=Leah | amistad 0->20 | habladoHoy False->True`, y segunda conversación del día `amistad 20->20`).
- [x] En vivo: regalar con un objeto en la mano sigue el camino nativo y aparece como suceso (`[V01] Paso a vanilla por regalo/objeto activo (npc=Leah, item=(O)196)`, `amistad 20->100`, `regalosHoy=1`, `suceso:regalo:0:-3354531154322050897:1`).
- [x] En vivo: la misión *Introductions* (o una equivalente de socializar) progresa al hablar con Leah interceptada (`SocializeQuest.whoToGreet` pasa de `remaining=26` con `Leah` a `remaining=25` sin `Leah`).

## Fuera de alcance

- La conversación con LLM (CP-503). Solo se usa una línea fija de prueba.
