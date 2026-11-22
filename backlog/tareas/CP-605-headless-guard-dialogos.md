---
id: CP-605
titulo: Pelican 4Ever · HeadlessEventGuardModule respeta diálogos y cartas activos de Corazón Pelican
estado: pendiente
prioridad: P1
hito: F6
depende_de: [CP-601]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
proyecto_destino: Stardew Valley MORPG
---

## Objetivo

Evitar que `HeadlessEventGuardModule` avance, cancele o cierre cajas de diálogo o cartas de prórroga abiertas por Corazón Pelican en el jugador anfitrión cuando `AutoHostSaveName` está configurado.

## Contexto

Cuando Pelican 4Ever arranca con `AutoHostSaveName` y `AutoResolveHostEvents = true`, `HeadlessEventGuardModule` clasifica `DialogueBox` y `LetterViewerMenu` en `Game1.activeClickableMenu` como menús bloqueantes en el host y los avanza/cierra tras `HostEventAutoResolveDelaySeconds` (5 s por defecto). Si un desarrollador o anfitrión híbrido abre un diálogo de `DialogoModule` o una carta de `CorreoProrrogaModule` (`CorazonPelican.Prorroga.*`) sin un `RenderLease` o `HostEventRelay` activo, el guardián los cerrará automáticamente.

## Alcance

- **Lado Pelican 4Ever (`Stardew Valley MORPG` · `TASK-2602`):** extender `HostEventGuardLogic.Decide` y `HeadlessEventGuardModule` para respetar un indicador explícito de diálogo/carta activa de Corazón Pelican (p. ej. prefijo `CorazonPelican.Prorroga.` en `LetterViewerMenu.mailTitle`, clave en `NPC.modData` o marca equivalente sin acoplamiento directo al mod).
- **Lado Corazón Pelican:** marcar/exponer el turno de diálogo activo o pregunta de carta mientras esté abierto en el anfitrión (si hace falta más allá de `CorazonPelican.Control` y `CorazonPelican.Prorroga.`).

## Criterios de aceptación

- [ ] Test en `Pelican4Ever.Tests` (`HostEventGuardLogicTests`): un `DialogueBox` o `LetterViewerMenu` perteneciente a Corazón Pelican no emite `AdvanceDialogue` ni `ExitActiveMenu`.
- [ ] Eventos y diálogos vanilla desatendidos en servidores con `AutoHostSaveName` siguen desbloqueándose igual que hasta ahora.

## Fuera de alcance

- Modificar `HostEventRelayModule` ni la política de timeouts de eventos vanilla en servidores desatendidos.
