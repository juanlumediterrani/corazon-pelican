---
id: CP-504
titulo: Opciones del jugador desde affordances reales y texto libre
estado: en-curso
prioridad: P2
hito: F5
depende_de: [CP-503, CP-014]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

Tras la frase de Leah, 3–4 opciones generadas a partir de lo que realmente se puede hacer, más «Decir otra cosa…».

## Contexto

GSS §8.4, ADS §8.3. Pregunta nativa con respuestas (`responses`), operable por humanos y por el puente de Voyager.

## Alcance

- `OpcionesJugador` en Core: inventario regalable, encargos según apertura, lugares para citas.
- Texto libre con `MenuTextoLibre` según [`docs/spikes/v06-texto-libre.md`](../../docs/spikes/v06-texto-libre.md) (mitigar `limitWidth = false`, `textLimit` en pegado, `Keys.E` de `menuButton` mientras `Selected == true`, filtro de glifos en `_font.Characters` y apertura automática de `TextEntryMenu` en mando) más la comprobación interactiva en vivo de «¿Me dibujarías el lago?» y los identificadores estables para Voyager (`CP-014`).

## Criterios de aceptación

- [x] Tests: nunca se ofrece regalar algo que no está en el inventario ni pedir un encargo que la apertura no permite. (`OpcionesJugadorTests.Nunca_ofrece_regalar_algo_que_no_esta_en_el_inventario`, `OpcionesJugadorTests.Nunca_ofrece_pedir_encargo_si_la_apertura_no_lo_permite`)
- [x] Las opciones tienen identificadores estables para un agente. (`OpcionesJugadorTests.Las_opciones_tienen_identificadores_estables_e_incluyen_decir_otra_cosa`; `MenuTextoLibre` expone `myID = 104 / 101 / 102`)
- [x] El texto libre llega a la mente tal cual, con su longitud máxima aplicada. (`OpcionesJugadorTests.El_texto_libre_llega_a_la_mente_tal_cual_con_longitud_maxima_aplicada`; comprobado en vivo `MenuTextoLibre` en `Dr4coVoyagerBridge /menu_components` con `myID = 104/101/102` y cierre por `POST /menu_click`; pendiente teclear «¿Me dibujarías el lago?» con teclado/IME físico)

## Fuera de alcance

- Moderación de temas (la validación acota consecuencias, no temas: GSS §14).
