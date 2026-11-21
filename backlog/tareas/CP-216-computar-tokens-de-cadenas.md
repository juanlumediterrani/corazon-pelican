---
id: CP-216
titulo: Contabilizar las cadenas contra el límite diario de tokens
estado: hecha
prioridad: P2
hito: F2
depende_de: [CP-207]
tipo: code-light
complejidad: media
duracion_min: 60
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Contabilizar las cadenas contra el límite diario de tokens.

## Evidencia

Con solo el límite diario configurado, la admisión de una cadena consulta el gasto ordinario mientras sus llamadas incrementan el contador de cadenas. Con un tope de 100, cuatro llamadas de 100 dejaron el gasto en 400 y se seguía admitiendo otra. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [x] Con límite diario de 100 y sin cupo separado para cadenas, una llamada de cadena que consume 100 impide admitir la siguiente ese día. Cubrir también el gasto mixto de llamadas ordinarias y cadenas. Evidencia: [`PresupuestoTokens.puede_gastar`](../../mente/costes.py) + `test_cadenas_computan_contra_limite_diario_cuando_no_hay_cupo_separado` en [`mente/tests/test_costes.py`](../../mente/tests/test_costes.py).
- [x] Verificar los casos de cupo de cadenas configurado, reinicio diario e interlocución conforme a la política de CP-207. Evidencia: `test_cadenas_computan_contra_limite_diario_cuando_no_hay_cupo_separado` en [`mente/tests/test_costes.py`](../../mente/tests/test_costes.py).

Verificación al cerrar (sin juego vivo):
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 359, Con error: 0, Omitido: 0, Total: 359.
- `python3 -m pytest -q` → 113 passed.
