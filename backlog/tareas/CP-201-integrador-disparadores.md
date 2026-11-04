---
id: CP-201
titulo: Integrador con fuga, refractario y disparadores de pulsión, incluidos los hitos de rutina
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-102, CP-004, CP-003]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
fecha: 2026-11-04
---

## Objetivo

El motor decide cuándo algo merece pensamiento: pico, interlocución, deriva, hito, fin de intención, compromiso y transición.

## Contexto

GSS §4.1–4.2 y §5.3 (decisión P-2). La cadencia de pensamiento no se programa: emerge de estos disparadores y se mide en CP-208. **Los hitos los dan las rutinas, literalmente**: el horario que el juego elige cada día para el personaje (cambia con estación, día, clima, festivales, corazones, matrimonio y avance de la partida). Cada entrada de hoy es un plan finito («abrir la tienda», «ir al Saloon»), igual que un plan que pidió la mente («ir a la pasarela y sentarse»). El perfil no los lista: los anota por patrón para decir qué hechos acompañan al pulso y si se omiten cuando ya pensó por el camino. `despertar` y `acostarse` son hitos comunes que abren cadenas (CP-209). Refractario en tiempo real; presupuesto en CP-202.

## Alcance

- `Integrador.cs`, `Refractario.cs`, `Disparadores.cs` en Core.
- Prioridades entre disparadores; la interlocución siempre gana.
- Hitos de intención a partir del evento «plan terminado» del cuerpo (CP-302) y de rutina en el momento de disponerse con la siguiente entrada del horario de hoy (una entrada dura de su hora a la de la siguiente; CP-011), enriquecidos con la anotación del perfil que coincida.
- Cada pulso de hito lleva la agenda pendiente del día (pensamiento en cascada, GSS §4.1).
- Regla `omitir_si_ya_penso` por hito.
- Despertar al empezar el día; acostarse al irse a dormir o en el fin de día, lo que llegue antes.
- Transiciones: cambio de luz y de clima.

## Criterios de aceptación

- [x] En FakeWorld, un festival simulado (50 entidades moviéndose) no produce más de una pulsión por periodo refractario salvo interlocución. Evidencia: `Un_festival_no_produce_mas_de_una_pulsion_por_periodo_refractario_salvo_interlocucion` (5 min con multitud, jugador con 10 corazones e interlocución en medio).
- [x] Muchas saliencias menores sostenidas acaban en una pulsión de deriva; una sola menor, no. Evidencia: `Muchas_saliencias_menores_sostenidas_acaban_en_deriva`, `Una_sola_saliencia_menor_nunca_llega_a_deriva` (20 000 pasos).
- [x] Un plan de la mente `ir_a` + `sentarse` produce un único pulso de hito al sentarse; si a mitad de camino salta una deriva y el hito tiene `omitir_si_ya_penso`, no se emite. Evidencia: `Un_plan_ir_a_y_sentarse_produce_un_unico_hito_al_sentarse`, `Si_a_mitad_de_camino_salta_una_deriva_el_hito_omitible_no_se_emite`.
- [x] Un hito de rutina sin `omitir_si_ya_penso` se emite aunque haya habido otra pulsión durante el plan, y lleva los hechos de su `contexto`. Evidencia: `Un_hito_de_rutina_no_omitible_sale_aunque_haya_pensado_y_lleva_su_contexto` (Saloon de Leah: `personas_cerca`, agenda pendiente `Fri:2340`) y, en contraste, `Un_hito_omitible_de_la_rutina_se_omite_si_penso_durante_su_plan`. El `Disparo` lleva las fuentes de contexto; reunir sus hechos es del sensorio (CP-104).
- [x] El pulso de un hito de rutina sale antes de que el cuerpo empiece la entrada siguiente, de modo que la respuesta todavía puede cambiarla. Evidencia: `El_hito_de_rutina_sale_antes_de_la_hora_de_su_entrada` (`fall:1900` sale a las 18:50).
- [x] Cada día simulado produce exactamente un despertar y un acostarse, también si el día termina antes de que Leah se acueste. Evidencia: `Cada_dia_tiene_exactamente_un_despertar_y_un_acostarse` (3 días), `Si_el_dia_termina_antes_de_acostarse_la_cadena_sale_entonces_y_una_sola_vez`, `Un_dia_sin_horario_tambien_se_acuesta_al_cambiar_de_dia`.
- [x] Core no contiene ningún nombre de hito concreto salvo `despertar` y `acostarse`. Evidencia: `Core_no_nombra_hitos_concretos` (saloon, esculpir, dibujar_fuera, volver_a_casa) y la guarda de personajes de `ArquitecturaTests`.
- [x] FakeWorld con dos horarios distintos (otoño soleado y lluvia) para el mismo perfil: los hitos emitidos siguen al horario de cada día sin cambiar el perfil. Evidencia: `Los_hitos_siguen_al_horario_de_cada_dia_sin_cambiar_el_perfil` (`fall:*` el día 68, `Fri:*` el 69; `fall:2200` omitido por la puesta de sol).

## Notas de cierre

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 135/135, `python3 -m pytest -q` 40 passed.

- Core: `Atencion/Integrador.cs`, `Refractario.cs`, `Disparadores.cs` (devuelve `Disparo`: motivo, momento, ref, hito resuelto, agenda pendiente, detalle) e `Intencion/HitosRutina.cs` (`EntradaHorario`, `HorarioDia`, `HitoResuelto`).
- Decisiones de implementación, llevadas a la GSS §4.2: el refractario solo frena los estímulos (pico, deriva, transición en cola); rutina e interlocución pasan siempre. Toda pulsión vacía el integrador y abre ventana. El hito de rutina sale 10 min de juego antes de su entrada. La última entrada del horario es el propio acostarse. El pico exige cruzar el umbral (no se repite mientras sigue arriba). Luz y clima se toman de nuevo como referencia al cambiar de día.
- Perfil: `umbrales.deriva_semivida_min` (opcional, 10 por defecto).
- Tests: `Simulacion/BucleAtencion.cs` (arnés que une FakeWorld, saliencia, bandas, novedad y disparadores), `Escenario.ConMultitud` y `ConHorarios`.
- **Hallazgos de calibración** anotados en CP-208: con el perfil actual la deriva salta al final de cada ventana refractaria en una escena tranquila, de modo que casi todo hito omitible se omite, y el pico casi nunca llega.

## Fuera de alcance

- Pensamiento anticipado (CP-507). Contenido de las cadenas (CP-209). Cambios de rutina (CP-306).
