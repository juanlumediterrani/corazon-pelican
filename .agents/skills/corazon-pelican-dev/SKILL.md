---
name: corazon-pelican-dev
description: >-
  Guía técnica de desarrollo para Corazón Pelican: separación estricta entre
  CorazonPelican.Core (net6.0 puro) y adaptadores SMAPI/Harmony, contrato
  JSON Schema motor ↔ mente Python, simulación FakeWorld y escenarios
  Controlados CorazonDev_* con tools/escenarios.py.
---

# Desarrollo Técnico — Corazón Pelican

Skill de proyecto para trabajar en C# (`CorazonPelican`, `CorazonPelican.Core`, `CorazonPelican.Tests`) y Python (`mente/`, `taller/`, `tools/`).

---

## 1. Frontera de Capas y Reglas Invariantes

1. **`CorazonPelican.Core` es 100 % puro (`net6.0`):**
   - Prohibido importar `StardewValley`, `StardewModdingAPI`, `HarmonyLib` o `Microsoft.Xna.Framework`.
   - `ArquitecturaTests` falla si entra una dependencia del juego o acoplamiento específico a un personaje (toda particularidad de Leah vive en `CorazonPelican/assets/personajes/`).
2. **`CorazonPelican/` es solo adaptador y módulos (`IModule`):**
   - Toda mutación sobre `NPC`, `Game1` o `modData` debe respetar el hilo principal (`MainThreadActionQueue`), la reclamación no destructiva (`ReclamacionNpc`) y la limpieza al volver al título (`ReturnedToTitle`).
3. **Fuente única del contrato (`schemas/*.schema.json`):**
   - Todo payload intercambiado entre `ServidorApiMotor` (C#) y `mente/cliente_motor.py` / `mente/servicio.py` (Python) pasa por `schemas/{pulso,respuesta,resultado,salud,perfil,obra,comun}.schema.json` con validación de época (`ValidadorEpoca`, `ADR-0005`).

---

## 2. Flujo de Pruebas Deterministas (Sin Juego Vivo)

```bash
# C# Core + Simulación FakeWorld + Escenas Doradas (xUnit net8.0)
dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false

# Mente Python, taller y herramienta de escenarios
python3 -m pytest -q
```

- Antes de añadir un comportamiento nuevo del sensorio o del diálogo, revisa `CorazonPelican.Tests/Sensorio/EscenasDoradas/` y `mente/tests/escenas_caracter/` para mantener determinismo y no alucinar entidades inexistentes (`R-01`, `R-02`).

---

## 3. Escenarios de Desarrollo (`CorazonDev_*`)

Nunca toques una campaña real ni ejecutes juego vivo sin autorización explícita del usuario en la sesión. Cuando la sesión esté autorizada:

```bash
python3 tools/escenarios.py listar
python3 tools/escenarios.py verificar <nombre_escenario>
python3 tools/escenarios.py restaurar <nombre_escenario> --destino CorazonDev_<id> --confirmar
```

Los respaldos residen fuera de git en `~/.local/share/corazon-pelican/escenarios/` y solo restauran sobre carpetas `CorazonDev*`.

---

## 4. Puente con `Pelican 4Ever`

Para cambios en `IPelicanApi`, `IntegracionPelicanModule` o `ProtocoloMultijugador`:
- Lee [`docs/contrato-pelican4ever.md`](../../../docs/contrato-pelican4ever.md) y activa la skill compartida `smapi-harmony-pelican-bridge` (expuesta por `MCP Pelican` vía `.agents/skills.json`).
- Mantén siempre la degradación segura cuando `Pelican4Ever.SVRTMP` no está cargado (`DegradacionVanillaTests`).
