---
id: CP-006
titulo: Partida de desarrollo separada y medición de los lugares de Leah (V-09) 🎮
estado: hecha
prioridad: P2
hito: F0
depende_de: [CP-003]
tipo: analysis
complejidad: baja
duracion_min: 90
sensibilidad: public
vivo: si
fecha: 2026-11-21
---

## Objetivo

Una partida dedicada al desarrollo y las áreas reales de cada lugar del léxico de Leah, medidas en vivo.

## Contexto

ADS §16 V-09. El horario real de Leah da ubicaciones y casillas de partida para varios lugares ([notas](../../docs/notas-juego.md) §3.1); la pasarela del lago no está en él y hay que medirla. Voyager (SV-030) separó la partida de desarrollo de la de campaña; aquí conviene igual, con copias de guardado como escenarios (Leah en la pasarela en otoño, con lluvia, de noche…).

## Alcance

- Partida DEV con copia de seguridad y escenarios con nombre.
- Comando de depuración que imprime ubicación, casilla y orientación del jugador para medir áreas.
- Áreas reales para los lugares del perfil inicial; lugares por estación si el horario de Leah lo pide.

## Criterios de aceptación

- [x] `leah.json` valida sin avisos de áreas pendientes. Evidencia: [`leah.json`](../../CorazonPelican/assets/personajes/leah.json) (`pasarela_lago`: `Mountain [57, 26, 1, 5]`, `orilla_norte`: `Mountain [64, 14, 3, 1]`), [`docs/notas-juego.md`](../../docs/notas-juego.md) §14, [`PerfilValidatorTests.cs`](../../CorazonPelican.Tests/Perfil/PerfilValidatorTests.cs) (`Leah_en_desarrollo_y_en_modo_normal_valida_sin_avisos_ni_errores`: 0 errores, 0 avisos) y arranque en vivo en modo `Normal` en SMAPI sin avisos ni errores.
- [x] Cada área se ha comprobado con Leah o el jugador de pie dentro y la casilla es transitable. Evidencia: comandos `cp_posicion` y `cp_lugares` en [`ConsolaModule.cs`](../../CorazonPelican/Modules/ConsolaModule.cs) + comprobación física en vivo (`CorazonDev_454975123`) recorriendo a pie la pasarela `(57, 30) -> (57, 26)` (`cp_posicion` en `(57, 28)` `transitable=True`), pisando `(65, 14)` dentro de `[64, 14, 3, 1]` (`transitable=True`) y verificando de pie las 7 áreas existentes (`adjusted=False`, `OK`), además de 3 casillas estacionales auxiliares documentadas en `docs/notas-juego.md` §14.
- [x] Los escenarios se restauran con un comando documentado en `AGENTS.md`. Evidencia: [`tools/escenarios.py`](../../tools/escenarios.py), [`tools/tests/test_escenarios.py`](../../tools/tests/test_escenarios.py), [`AGENTS.md`](../../AGENTS.md), escenarios reales guardados fuera del repo (`base_primavera` y `otono_dia12` desde `CorazonDev_454975123`), verificados por SHA-256 y restaurados y cargados en vivo como `CorazonDev_RestoreCheck`.

Verificación sin juego vivo tras el último ajuste de código y datos:
- `check_harmony_patches.py` → `errors=0, warnings=0`
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores)
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 368, Con error: 0, Omitido: 0, Total: 368
- `python3 -m pytest -q` → 116 passed in 1.67s

## Fuera de alcance

- Lugares de otros personajes.
