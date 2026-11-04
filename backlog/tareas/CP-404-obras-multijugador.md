---
id: CP-404
titulo: Sincronización de obras a clientes multijugador 🎮
estado: pendiente
prioridad: P3
hito: F4
depende_de: [CP-403]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Los clientes con el mod ven las obras; el host avisa de los que no lo tienen.

## Contexto

ADS §10.

## Alcance

- Envío de texturas en `PeerConnected` y al crear cada obra; registro en el cliente.
- Aviso de consola ante pares sin Corazón Pelican.

## Criterios de aceptación

- [ ] En vivo: un cliente que entra después de crear la obra la ve correctamente.
- [ ] Una obra creada con el cliente conectado aparece sin reconectar.
- [ ] El host registra el aviso para un par sin el mod.

## Fuera de alcance

- Diálogo en clientes (CP-503).
