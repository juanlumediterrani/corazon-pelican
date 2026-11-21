---
id: CP-502
titulo: Compromisos con plazo cuya saliencia sube hasta provocar una pulsión
estado: hecha
prioridad: P1
hito: F5
depende_de: [CP-501, CP-201, CP-202]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-09]
fecha: 2026-11-21
---

## Objetivo

Las promesas vuelven a la conciencia de Leah por el mismo mecanismo de atención que todo lo demás.

## Contexto

GSS §9.3. Un compromiso incumplido no se oculta: se recuerda y puede disculparse.

## Alcance

- `Compromisos.cs`: curva de saliencia por plazo, estados cumplido / incumplido.
- Al acercarse el plazo, consultar el presupuesto de CP-202. Si quedan plazas ordinarias libres o extra, emitir el pulso. Si ambas se agotan, registrar una petición de prórroga para ese compromiso y destinatario, conservando el plazo vigente (P-12, CP-509). La cadena de despertar también repasa los compromisos del día (CP-209).

## Criterios de aceptación

- [x] `CompromisoSalienciaTests`: al acercarse el plazo se emite un pulso ordinario, uno extra o se registra una petición según los saldos (R-09). Repetir la observación del mismo compromiso pendiente de prórroga conserva una única petición y el plazo original. Evidencia: [`Compromisos.cs`](../../CorazonPelican.Core/Memoria/Compromisos.cs) + [`MotorHabitante.cs`](../../CorazonPelican.Core/Atencion/MotorHabitante.cs) + `Saliencia_crece_monotonicamente_al_acercarse_el_plazo` y `Compromiso_cercano_al_plazo_usa_ordinario_extra_o_registra_prorroga_unica_con_plazo_original` en [`CompromisoSalienciaTests.cs`](../../CorazonPelican.Tests/Memoria/CompromisoSalienciaTests.cs).
- [x] Un compromiso vencido sin cumplir pasa a la memoria como incumplido y aparece en el siguiente pulso con esa persona. Evidencia: `Compromiso_vencido_pasa_a_memoria_como_incumplido_y_aparece_en_el_siguiente_pulso_con_esa_persona` en [`CompromisoSalienciaTests.cs`](../../CorazonPelican.Tests/Memoria/CompromisoSalienciaTests.cs).
- [x] Guardar y cargar conserva compromisos y su estado. Evidencia: `Guardar_y_cargar_conserva_compromisos_estado_y_peticion_de_prorroga` en [`CompromisoSalienciaTests.cs`](../../CorazonPelican.Tests/Memoria/CompromisoSalienciaTests.cs).

Verificación al cerrar (sin juego vivo):
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 359, Con error: 0, Omitido: 0, Total: 359.
- `python3 -m pytest -q` → 112 passed.

## Fuera de alcance

- Generar el plan de cumplimiento (lo hace la mente).
