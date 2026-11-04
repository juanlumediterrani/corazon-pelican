---
id: CP-014
titulo: Spike V-06 · menú nativo para «Decir otra cosa…» operable por humanos y por Voyager
estado: pendiente
prioridad: P3
hito: S
depende_de: [CP-001]
tipo: analysis
complejidad: baja
duracion_min: 60
sensibilidad: public
---

## Objetivo

Elegir el componente para el texto libre del jugador.

## Contexto

[Notas](../../docs/notas-juego.md) §6: `StardewValley.Menus.TextEntryMenu` es el teclado en pantalla para mando (ventana 168×88 con teclas), no un campo de texto de PC. Candidatos: `NamingMenu` (usa `TextBox` y botón OK, el de poner nombre a animales) o un `IClickableMenu` propio con `TextBox`. Voyager opera menús nativos por `myID`/componentes (`docs/menu-contract.md` de Voyager).

## Alcance

- Probar `NamingMenu` con título propio y longitud máxima, y un menú propio mínimo.
- Comprobar teclado físico, IME/acentos y mando.
- Comprobar que el puente de Voyager puede escribir y confirmar.

## Criterios de aceptación

- [ ] `docs/spikes/v06-texto-libre.md` con la decisión y el motivo.
- [ ] Se pueden escribir «¿Me dibujarías el lago?» con tilde y «¿».
- [ ] El componente expone identificadores estables para un agente.

## Fuera de alcance

- Flujo de diálogo completo (CP-504).
