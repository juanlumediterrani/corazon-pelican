---
name: brain-cierre-tarea
description: >-
  Checklist obligatorio de cierre, verificación sin juego vivo y write-back al
  Project Brain de Corazón Pelican (EST-001 v1.2 de Niu D'Ocells) antes de dar
  por terminada una tarea, commitear o abrir una PR.
---

# Cierre de Tarea y *Write-Back* — Corazón Pelican

Ejecuta este protocolo antes de declarar cerrada cualquier tarea o preparar un commit/PR en `Corazón Pelican`.

---

## 1. Verificación Técnica Obligatoria

Ejecuta siempre sin despliegue ni zip sobre `Mods/`:

```bash
dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
python3 -m pytest -q
```

- Cita exactamente los resultados reales observados (no omitas tests fallidos ni saltados).
- Si cambiaste archivos en `schemas/` o contratos en `CorazonPelican.Core/Contrato/` o `mente/contrato.py`, comprueba tanto `CorazonPelican.Tests` como `mente/tests/test_contrato.py`.

---

## 2. Protocolo de *Write-Back* al Brain (`EST-001` v1.2)

Comprueba si el cambio altera decisiones o contratos vigentes:

| Si modificaste... | Qué debes actualizar en el mismo commit |
| --- | --- |
| `schemas/*.schema.json`, `CorazonPelican.Core/Contrato/*`, `mente/contrato.py` | [`ADR-0005`](../../../docs/adr/0005-contrato-y-epoca.md) o nuevo `0007+` si cambia invariantes de época/contrato |
| `CorazonPelican/Framework/IPelicanApi.cs`, `IntegracionPelicanModule.cs`, `ProtocoloMultijugador.cs` | [`docs/contrato-pelican4ever.md`](../../../docs/contrato-pelican4ever.md) y revisar contraparte en `MCP Pelican` |
| Límites de `CorazonPelican.Core` (pureza, memoria, diálogo, atención) | ADR afectado (`0001..0006`) o nuevo ADR en [`docs/adr/README.md`](../../../docs/adr/README.md) |
| Acuerdos de diseño o reglas `R-xx` | [`docs/decisiones.md`](../../../docs/decisiones.md) y tarea correspondiente en [`backlog/`](../../../backlog/README.md) |
| Nueva ruta relevante o cambio estructural de carpetas | Tabla Código → Memoria en [`AGENTS.md`](../../../AGENTS.md) |

---

## 3. Higiene Corporativa (`Niu D'Ocells`)

1. **Sin documentación dispersa:** si generas un informe fechado o evidencia de pruebas en `CorazonDev_*`, guárdalo en `docs/verifications/YYYY-MM-DD-<tema>.md` (nunca en la raíz de `docs/`).
2. **Sin binarios ni datos del juego:** verifica `git status -s` para asegurar que no hay `.xnb`, volcados `xnbdump`, `telemetria/` ni `local.props` preparados para commit.
3. **Relación con ERPNext y `produccio/` (`DEC-0001` / `DEC-0002`):** las tareas de desarrollo siguen trazadas en `backlog/` y sincronizadas con ERPNext; cualquier zip distribuible final del mod pertenece únicamente a `~/Documentos/Niu D'Ocells/produccio/`.
