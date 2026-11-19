---
id: CP-014
titulo: Spike V-06 · menú nativo para «Decir otra cosa…» operable por humanos y por Voyager
estado: en-curso
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

- [x] `docs/spikes/v06-texto-libre.md` con la decisión y el motivo. Redactado en [`docs/spikes/v06-texto-libre.md`](../../docs/spikes/v06-texto-libre.md) a partir del decompilado `1.6.15.24356`, los assets `Fonts/SpriteFont1*.xnb` y el puente de Stardew Voyager (`Dr4coVoyagerBridge`): recomienda `MenuTextoLibre : IClickableMenu` con `TextBox`, `limitWidth = false`, `textLimit` explícito y apertura automática de `TextEntryMenu` en mando.
- [ ] Se pueden escribir «¿Me dibujarías el lago?» con tilde y «¿». **Análisis estático completado** (`KeyboardDispatcher.Event_TextInput`, `SpriteFont1.xnb == SpriteFont1.es-ES.xnb`, `TextEntryMenu.letterMaps[2]` incluye `"áéíóúü¡!¿?"` y `"ñ"`, y se detectaron las trampas de `limitWidth = true`, `menuButton` con `Keys.E` y pegado en `TextBox.RecieveTextInput(string)`). **Falta prueba interactiva en vivo** (regla 4: sin juego vivo en esta sesión; la ficha no tiene `vivo: si`).
- [ ] El componente expone identificadores estables para un agente. **Especificado estáticamente** en [`docs/spikes/v06-texto-libre.md`](../../docs/spikes/v06-texto-libre.md) §3–§4 (`textBoxCC` `myID = 104`, `okButton` `myID = 101`, `cancelButton` `myID = 102`), y auditado contra `NativeMenuInput.GetActiveMenuComponents()`, `/menu_click`, `/menu_confirm` y `/name` de Voyager; la implementación C# y la prueba viva contra `Dr4coVoyagerBridge` quedan para `CP-504` (y Hito 6 para la ruta genérica `/menu_text` en el puente de Voyager).

## Evaluación del estado

La investigación estática queda cerrada sin entrar en juego vivo (regla 4 de `AGENTS.md`). Se deja la ficha en `en-curso` porque los criterios 2 y 3 incluyen verificación interactiva en ventana y con el puente vivo que no se ha ejecutado todavía. Para desbloquear `CP-504` (`depende_de: [CP-503, CP-014]`), el coordinador puede:

1. Cerrar `CP-014` como `hecha` en calidad de spike estático trasladando explícitamente las dos comprobaciones interactivas en vivo (teclado físico/IME en Linux + `TextEntryMenu` con mando + `Dr4coVoyagerBridge`) a `CP-504` (o `CP-508` 🎮); o
2. Mantenerla `en-curso` hasta ejecutar esa verificación rápida en vivo cuando se implemente `MenuTextoLibre` en `CP-504`.

## Fuera de alcance

- Flujo de diálogo completo (CP-504).
