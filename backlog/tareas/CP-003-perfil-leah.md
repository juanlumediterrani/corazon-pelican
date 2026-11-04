---
id: CP-003
titulo: Perfil de personaje cargado como asset y validado; perfil inicial de Leah
estado: pendiente
prioridad: P1
hito: F0
depende_de: [CP-002]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
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

- [ ] Tests: umbrales desordenados, acción fuera del vocabulario, lugar duplicado y área `[0,0,0,0]` producen errores con mensaje legible.
- [ ] Un perfil con un cambio desconocido, una fuente de `contexto` desconocida o una anotación sin `si` (salvo `despertar`/`acostarse`) no valida.
- [ ] El perfil de Leah valida salvo por las áreas marcadas «pendiente de medir», que el validador reporta como aviso en modo desarrollo y como error en modo normal.
- [ ] `grep -ri leah CorazonPelican.Core` no devuelve nada (el motor no contiene a Leah).

## Fuera de alcance

- Plantillas de prosa (CP-104) y voz (CP-205).
