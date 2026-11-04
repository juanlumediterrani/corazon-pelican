---
id: CP-103
titulo: Bandas de detalle foco/mención/periferia/ausente
estado: hecha
prioridad: P1
hito: F1
depende_de: [CP-102]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-03]
fecha: 2026-11-04
---

## Objetivo

Asignar cada entidad a una banda según los umbrales del perfil.

## Contexto

GSS §3.3. Consecuencia buscada: una Leah absorta puede no enterarse de quien llega.

## Alcance

- `Bandas.cs` con histéresis opcional para evitar parpadeo entre bandas en observaciones consecutivas.

## Criterios de aceptación

- [x] `GradienteDetalleTests`: entidades bajo periferia no llegan a la escena (R-03). Evidencia: `Entidades_bajo_periferia_no_llegan_a_la_escena` (3 min de FakeWorld con jugador alejándose y NPC lejano: ninguna entidad con saliencia < periferia en `EnEscena`) y `Ausente_nunca_llega_a_la_escena_aunque_haya_histeresis`. **R-03 entra en vigor** para el filtro de bandas; CP-104 debe construir la escena solo con `ClasificadorBandas.EnEscena`.
- [x] Con histéresis activada, una entidad oscilando ±0,01 en torno a un umbral no cambia de banda en cada observación. Evidencia: `Con_histeresis_oscilar_un_centesimo_no_hace_parpadear_la_banda` (40 observaciones en cada umbral), frente a `Sin_histeresis_la_misma_oscilacion_si_parpadea`; un cambio claro sí cambia de banda.
- [x] Umbrales leídos del perfil; ninguno codificado en Core. Evidencia: `Las_bandas_salen_de_los_umbrales_del_perfil` y `Bandas_no_codifica_ningun_umbral` (ningún literal decimal fuera de comentarios en `Bandas.cs`).

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 117/117, `python3 -m pytest -q` 40 passed.

## Notas de cierre

- La histéresis es un umbral más del perfil: `umbrales.histeresis` (opcional en el esquema, 0 = sin histéresis; Leah 0,02). El validador exige que sea menor que la mitad del menor hueco entre umbrales, para no tapar una banda.
- Para subir de banda hay que superar el umbral más el margen; para bajar, quedar por debajo del umbral menos el margen. Lo que desaparece de la observación se olvida.

## Fuera de alcance

- Redacción de la prosa (CP-104).
