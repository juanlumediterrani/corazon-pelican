---
id: CP-215
titulo: Conectar un proveedor de modelo al ciclo de la mente
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-204]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Conectar un proveedor de modelo al ciclo de la mente.

## Evidencia

Los proveedores configurados llegan a `ModeloProveedorConfigurado.generar`, que lanza una excepción incluso con credenciales presentes. Para evaluar la voz y el humor en CP-208 hace falta al menos un transporte operativo. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [x] Con la configuración normal del proveedor elegido y un transporte HTTP simulado, comprobar que se envían voz, escena y esquema estrechado y que el ciclo consume una respuesta válida. Evidencia: [`ModeloProveedorConfigurado`](../../mente/modelos.py) + `test_openai_envia_voz_escena_y_esquema_estrechado_y_ciclo_acepta_respuesta` y `test_anthropic_y_gemini_conectan_con_ciclo_sin_claves_reales` en [`mente/tests/test_proveedor_modelo.py`](../../mente/tests/test_proveedor_modelo.py).
- [x] Cubrir respuesta mal formada y fallo del proveedor. La suite debe poder ejecutarse sin credenciales reales. Registrar en CP-208 la evaluación posterior con un modelo real. Evidencia: `test_respuesta_mal_formada_y_fallo_de_proveedor_sin_credenciales_reales` en [`mente/tests/test_proveedor_modelo.py`](../../mente/tests/test_proveedor_modelo.py) y nota añadida en [CP-208](CP-208-aceptacion-pulsion.md).

Verificación al cerrar (sin juego vivo):
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 355, Con error: 0, Omitido: 0, Total: 355.
- `python3 -m pytest -q` → 112 passed.
