---
id: CP-003
titulo: Perfil de personaje cargado como asset y validado; perfil inicial de Leah
estado: hecha
prioridad: P1
hito: F0
depende_de: [CP-002]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
fecha: 2026-11-04
---

## Objetivo

El motor carga perfiles desde `Mods/CorazonPelican/Personajes` y rechaza los incoherentes antes de gobernar a nadie.

## Contexto

GSS §1.2 (motor vs. personaje) y ADS §3.1. Las áreas de los lugares son marcadores hasta medirlas en vivo (CP-006).

## Alcance

- `PerfilPersonaje` y `PerfilValidator` en Core: rasgos, umbrales ordenados (`periferia < mencion < foco < pico`), refractario, presupuesto, léxico, apertura, acciones ⊆ vocabulario v1, formatos de obra.
- Carga vía `AssetRequested` para que un pack de Content Patcher pueda editar o añadir personajes.
- `rutina` (ADS §3.1): `por_defecto` y `anotaciones` por patrón (`si` sobre ubicación, animación o franja); `cambios` ⊆ {`saltar`, `mover`, `sustituir`}; `contexto` solo con fuentes que el motor conoce. Los hitos no se listan: salen del horario del día.
- `assets/personajes/leah.json` con léxico inicial (pasarela_lago, orilla_norte, tu_casa…) y áreas pendientes de medir marcadas como tales.

## Criterios de aceptación

- [x] Tests: umbrales desordenados, acción fuera del vocabulario, lugar duplicado y área `[0,0,0,0]` producen errores con mensaje legible. Evidencia: `PerfilValidatorTests` (`Umbrales_desordenados_son_error`, `Accion_fuera_del_vocabulario_es_error`, `Lugar_duplicado_es_error`, `Leah_en_modo_normal_falla_solo_por_areas_pendientes`, `Area_mal_formada_es_error_tambien_en_desarrollo`); los mensajes citan el campo y lo esperado.
- [x] Un perfil con un cambio desconocido, una fuente de `contexto` desconocida o una anotación sin `si` (salvo `despertar`/`acostarse`) no valida. Evidencia: `Cambio_de_rutina_desconocido_es_error`, `Fuente_de_contexto_desconocida_es_error`, `Anotacion_sin_patron_es_error`, `Hitos_del_motor_no_llevan_patron_ni_se_cambian`.
- [x] El perfil de Leah valida salvo por las áreas marcadas «pendiente de medir», que el validador reporta como aviso en modo desarrollo y como error en modo normal. Evidencia: `Leah_en_desarrollo_es_valida_con_avisos_solo_por_areas_pendientes` y `Leah_en_modo_normal_falla_solo_por_areas_pendientes` (2 lugares pendientes: `pasarela_lago`, `orilla_norte`); además cumple `perfil.schema.json` en C# y Python.
- [x] `grep -ri leah CorazonPelican.Core` no devuelve nada (salida vacía, código 1). Guarda permanente: `ArquitecturaTests.El_codigo_de_core_no_nombra_personajes`.

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 63/63, `python3 -m pytest -q` 40 passed.

## Notas de cierre

- `PerfilPersonaje` usa `[DataContract]`/`[DataMember]` (BCL) para que SMAPI y Content Patcher (Newtonsoft) lean los nombres del esquema; los tests leen con Newtonsoft para reproducir el camino real.
- `CargadorPerfiles` (mod) publica `Mods/CorazonPelican/Personajes` con `AssetRequested` y valida en `GameLaunched`. **No probado en vivo** (ni la carga ni una edición con Content Patcher): queda para la primera sesión 🎮 (CP-006).
- Hasta CP-005 el mod valida en modo normal, así que hoy rechaza a Leah por las dos áreas sin medir: es el comportamiento esperado.
- Lugares del léxico tomados del horario real de Leah (`docs/notas-juego.md` §3.1) con su casilla como área 1×1; presupuesto 12 pulsos/día según la estimación de `docs/notas-juego.md` §3.2.
- `plantillas` y `voz` apuntan a ficheros que crearán CP-104 y CP-205.

## Fuera de alcance

- Plantillas de prosa (CP-104) y voz (CP-205).
