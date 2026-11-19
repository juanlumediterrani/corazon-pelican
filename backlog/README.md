# Backlog · *Corazón Pelican*

Tareas del proyecto, una por fichero en `tareas/`, con el formato de tarea v1 de
Dr4co Core (el mismo de Stardew Voyager). Este índice se mantiene sincronizado
con los ficheros en el mismo commit.

Fuentes: [`design.md`](../design.md) (GSS), [`architecture.md`](../architecture.md) (ADS),
[`docs/decisiones.md`](../docs/decisiones.md) (decisiones P-x, pendientes y riesgos) y
[`docs/notas-juego.md`](../docs/notas-juego.md) (hechos del juego y del entorno comprobados).

**Estados:** `pendiente` · `en-curso` · `hecha` · `bloqueada` · `dormida`.
**🎮** = requiere juego vivo en la partida DEV y autorización explícita en esa sesión.
**Hitos:** F0 cimientos · S spikes · F1 sensorio · F2 pulsión y mente · F3 cuerpo ·
F4 taller y obras · F5 diálogo con actos · F6 encuentro · D decisiones · Futuro (tareas
`dormida` con direcciones fuera de v1).

## Mapa

```
F0 ──► F1 Sensorio ──► F2 Pulsión+mente ──► F3 Cuerpo ──► F4 Obras ──► F5 Diálogo ──► F6 Encuentro
 │                                            ▲             ▲            ▲
 └──► S Spikes ── CP-011/012 (horario, anim.) ┘             │            │
              ── CP-013/015 (taller, mueble) ───────────────┘            │
              ── CP-010/014 (conversación, texto libre) ─────────────────┘
```

Los spikes sin juego (CP-012, CP-013, CP-014) pueden hacerse en paralelo a F0–F1. Para leer
datos del juego (horarios, animaciones) está [`tools/xnbdump`](../tools/xnbdump/).
Para evaluar la vida propia de un habitante, la primera tarea creativa lista es **CP-205**.
Sus escenas permiten juzgar qué elegiría Leah cuando nadie le pide nada. CP-210 conectará lo
vivido con el humor de días posteriores. CP-208 observará la elección de la mente y CP-305
seguirá su efecto en el cuerpo. CP-013 puede resolver en paralelo
la legibilidad de los cuadros.

## Tareas listas

Estas cinco fichas están pendientes y tienen todas sus dependencias hechas.

| Prioridad | Tareas por hito e identificador |
|---|---|
| P1 | CP-013 taller · CP-203 API motor · CP-212 agenda y rutina |
| P2 | CP-006 medición de lugares 🎮 · CP-106 búfer y diario |

CP-014, CP-101 y CP-902 están en curso. El [registro de decisiones](../docs/decisiones.md) recoge las preguntas que siguen abiertas.

## Tareas

| ID | Tarea | Hito | Prio | Depende de | Estado |
| --- | --- | --- | --- | --- | --- |
| [CP-001](tareas/CP-001-repositorio-y-solucion.md) | Repositorio y solución multiproyecto que compila sin juego | F0 | P1 | — | hecha |
| [CP-002](tareas/CP-002-contrato-json-schema.md) | Contrato v1 en JSON Schema, con época de mundo, validado desde C# y Python | F0 | P1 | CP-001 | hecha |
| [CP-003](tareas/CP-003-perfil-leah.md) | Perfil de personaje cargado como asset y validado; perfil inicial de Leah | F0 | P1 | CP-002 | hecha |
| [CP-004](tareas/CP-004-fakeworld.md) | FakeWorld para probar el bucle completo sin juego | F0 | P1 | CP-001 | hecha |
| [CP-005](tareas/CP-005-config-modulos-consola.md) | Configuración validada, registro de módulos y comando cp_estado | F0 | P2 | CP-001 | hecha |
| [CP-006](tareas/CP-006-partida-dev-y-lugares.md) | Partida de desarrollo separada y medición de los lugares de Leah (V-09) 🎮 | F0 | P2 | CP-003 | pendiente |
| [CP-010](tareas/CP-010-spike-interceptar-conversacion.md) | Spike V-01 · interceptar la conversación sin perder amistad, regalos ni misiones 🎮 | S | P1 | CP-006 | pendiente |
| [CP-011](tareas/CP-011-spike-reclamar-npc.md) | Spike V-02 · reclamar a Leah y devolverla a su horario sin romperlo 🎮 | S | P1 | CP-006 | pendiente |
| [CP-012](tareas/CP-012-spike-animaciones-leah.md) | Spike V-07 · mapear las animaciones de Leah a las acciones del cuerpo | S | P2 | CP-001 | hecha |
| [CP-013](tareas/CP-013-spike-taller-32px.md) | Spike del taller · ¿se reconoce una obra a la resolución de un cuadro vanilla? (V-04) | S | P1 | CP-001 | pendiente |
| [CP-014](tareas/CP-014-spike-texto-libre.md) | Spike V-06 · menú nativo para «Decir otra cosa…» operable por humanos y por Voyager | S | P3 | CP-001 | en-curso |
| [CP-015](tareas/CP-015-spike-obra-como-mueble.md) | Spike V-03/V-05 · una textura generada como mueble colocable que sobrevive a guardar, cargar y multijugador 🎮 | S | P1 | CP-006 | pendiente |
| [CP-101](tareas/CP-101-observador-juego.md) | ObservadorJuego · del estado del juego a una Observacion de solo lectura | F1 | P1 | CP-003, CP-005 | en-curso |
| [CP-102](tareas/CP-102-saliencia.md) | Saliencia de cinco capas con distancia, línea de visión y novedad | F1 | P1 | CP-003, CP-004 | hecha |
| [CP-103](tareas/CP-103-bandas-gradiente.md) | Bandas de detalle foco/mención/periferia/ausente | F1 | P1 | CP-102 | hecha |
| [CP-104](tareas/CP-104-prosa-determinista.md) | Plantillas de prosa deterministas, escena y prosa de acción, con escenas doradas | F1 | P1 | CP-103, CP-105 | hecha |
| [CP-105](tareas/CP-105-lexico-unico.md) | Léxico único compartido por percepción, acción y diálogo | F1 | P2 | CP-003 | hecha |
| [CP-106](tareas/CP-106-bufer-diario.md) | Búfer de prosa de acción, huella de atención y diario con consolidación temporal | F1 | P2 | CP-104 | pendiente |
| [CP-107](tareas/CP-107-persistencia-estado.md) | Estado del personaje persistido con la partida, con versión y migraciones | F1 | P1 | CP-106 | pendiente |
| [CP-108](tareas/CP-108-aceptacion-sensorio.md) | Aceptación de fase 1 · un día completo de escenas legibles 🎮 | F1 | P2 | CP-101, CP-104, CP-107, CP-109, CP-006 | pendiente |
| [CP-109](tareas/CP-109-habitante-y-telemetria.md) | HabitanteModule, telemetría JSONL y comandos cp_escena / cp_pulso | F1 | P1 | CP-101, CP-104, CP-106 | pendiente |
| [CP-201](tareas/CP-201-integrador-disparadores.md) | Integrador con fuga, refractario y disparadores de pulsión, incluidos los hitos de rutina | F2 | P1 | CP-102, CP-004, CP-003 | hecha |
| [CP-202](tareas/CP-202-presupuesto-pulsos.md) | Presupuesto de pulsos aplicado por el motor, por modo de reloj, con reserva para las cadenas | F2 | P1 | CP-201 | hecha |
| [CP-203](tareas/CP-203-api-motor.md) | API loopback del motor con long-poll, cola al hilo principal, idempotencia y época | F2 | P1 | CP-002, CP-005 | pendiente |
| [CP-204](tareas/CP-204-mente-sin-cuerpo.md) | Mente en Python que recibe pulsos y escribe pensamientos (sin cuerpo) | F2 | P1 | CP-203, CP-205 | pendiente |
| [CP-205](tareas/CP-205-voz-y-rubrica.md) | Voz de Leah y rúbrica de coherencia de carácter | F2 | P1 | — | hecha |
| [CP-206](tareas/CP-206-memoria-recuperacion-reflexion.md) | Recuperación de memoria y reflexión del día | F2 | P2 | CP-204, CP-107, CP-211 | pendiente |
| [CP-207](tareas/CP-207-costes-tokens.md) | Presupuesto de tokens, caché de prompt y telemetría de costes | F2 | P2 | CP-204 | pendiente |
| [CP-208](tareas/CP-208-aceptacion-pulsion.md) | Aceptación de fase 2 · medir la cadencia de pensamiento en vanilla y en Pelican 🎮 | F2 | P2 | CP-202, CP-204, CP-206, CP-207, CP-209, CP-210, CP-108 | pendiente |
| [CP-209](tareas/CP-209-cadenas-despertar-acostarse.md) | Cadenas de pensamiento en los hitos, empezando por despertar y acostarse | F2 | P1 | CP-201, CP-204, CP-206, CP-212 | pendiente |
| [CP-210](tareas/CP-210-humor-y-conducta.md) | Conservar el humor nacido de lo vivido y observar su efecto en la conducta | F2 | P1 | CP-107, CP-206, CP-209 | pendiente |
| [CP-211](tareas/CP-211-origen-de-las-reflexiones.md) | Transportar y comprobar el origen de las reflexiones del diario | F2 | P1 | CP-002 | hecha |
| [CP-212](tareas/CP-212-agenda-y-cambios-rutina.md) | Agenda del día y validador de cambios de rutina en Core (R-16) | F2 | P1 | CP-003, CP-201 | pendiente |
| [CP-301](tareas/CP-301-intencion-validador.md) | Intención con ciclo de vida, pesos de atención y validador de precondiciones | F3 | P1 | CP-105, CP-203 | pendiente |
| [CP-302](tareas/CP-302-cuerpo-vocabulario.md) | CuerpoNpc · vocabulario de acciones v1 sobre Leah 🎮 | F3 | P1 | CP-011, CP-012, CP-301 | pendiente |
| [CP-303](tareas/CP-303-reclamacion-y-degradacion.md) | Reclamación, volver_a_rutina y degradación a la Leah vanilla | F3 | P1 | CP-011, CP-302 | pendiente |
| [CP-304](tareas/CP-304-reflejos.md) | Reflejos del perfil en menos de 100 ms | F3 | P2 | CP-302 | pendiente |
| [CP-305](tareas/CP-305-aceptacion-cuerpo.md) | Aceptación de fase 3 · intenciones cumplidas y rechazadas en vivo 🎮 | F3 | P2 | CP-303, CP-304, CP-306, CP-208 | pendiente |
| [CP-306](tareas/CP-306-control-rutina.md) | El personaje controla su rutina · agenda del día, reequilibrio en cascada y cambios de hitos | F3 | P2 | CP-011, CP-212, CP-301, CP-303 | pendiente |
| [CP-401](tareas/CP-401-taller.md) | Taller · generador enchufable y script de asset de la prosa a la PNG | F4 | P2 | CP-013, CP-002 | pendiente |
| [CP-402](tareas/CP-402-decision-autocritica.md) | Decisión de obra y autocrítica de la mente | F4 | P2 | CP-401, CP-204 | pendiente |
| [CP-403](tareas/CP-403-obras-y-portafolio.md) | ObrasModule · portafolio persistido y obras colocables como muebles | F4 | P2 | CP-015, CP-107, CP-401 | pendiente |
| [CP-404](tareas/CP-404-obras-multijugador.md) | Sincronización de obras a clientes multijugador 🎮 | F4 | P3 | CP-403 | pendiente |
| [CP-405](tareas/CP-405-desinstalacion.md) | cp_preparar_desinstalacion · exportar obras y sustituirlas por cuadros vanilla | F4 | P3 | CP-403 | pendiente |
| [CP-406](tareas/CP-406-aceptacion-obras.md) | Aceptación de fase 4 · una obra nacida de la escena, colgada y vista en un cliente 🎮 | F4 | P2 | CP-402, CP-404, CP-305 | pendiente |
| [CP-501](tareas/CP-501-actos-validador.md) | Actos de habla y validador «todos válidos o ninguno» | F5 | P1 | CP-301, CP-403 | pendiente |
| [CP-502](tareas/CP-502-compromisos.md) | Compromisos con plazo cuya saliencia sube hasta provocar una pulsión | F5 | P1 | CP-501, CP-201, CP-202 | pendiente |
| [CP-503](tareas/CP-503-dialogo-intercepcion.md) | DialogoModule · reflejo inmediato, respuesta pensada y caída a vanilla 🎮 | F5 | P1 | CP-010, CP-501, CP-304 | pendiente |
| [CP-504](tareas/CP-504-opciones-jugador.md) | Opciones del jugador desde affordances reales y texto libre | F5 | P2 | CP-503, CP-014 | pendiente |
| [CP-505](tareas/CP-505-valleytalk.md) | Convivencia con ValleyTalk · nunca dos voces para la misma persona | F5 | P3 | CP-503 | pendiente |
| [CP-506](tareas/CP-506-validador-menciones.md) | Validador de menciones · la mente no nombra lo que no existe para Leah | F5 | P2 | CP-501, CP-206 | pendiente |
| [CP-507](tareas/CP-507-pensamiento-anticipado.md) | Pensamiento anticipado y su cancelación | F5 | P2 | CP-503, CP-201 | pendiente |
| [CP-508](tareas/CP-508-aceptacion-dialogo.md) | Aceptación de fase 5 · una promesa hecha hoy y cumplida mañana 🎮 | F5 | P2 | CP-502, CP-504, CP-505, CP-506, CP-507, CP-509, CP-406 | pendiente |
| [CP-509](tareas/CP-509-prorroga-por-carta.md) | Pedir una prórroga por carta y aplicar la respuesta del destinatario | F5 | P2 | CP-502 | pendiente |
| [CP-601](tareas/CP-601-pelican-micro-wandering.md) | Pelican 4Ever · WorldLifeModule ignora NPCs reclamados por Corazón Pelican | F6 | P3 | CP-303 | pendiente |
| [CP-602](tareas/CP-602-sucesos-pelican.md) | IntegracionPelicanModule · sucesos del servidor como percepción | F6 | P3 | CP-101 | pendiente |
| [CP-603](tareas/CP-603-encuentro.md) | Sesión de encuentro · Pelican, un humano y el granjero de Voyager con Leah 🎮 | F6 | P3 | CP-508, CP-601, CP-602 | pendiente |
| [CP-901](tareas/CP-901-adrs.md) | Redactar ADR-0001 a ADR-0006 | D | P2 | CP-001 | hecha |
| [CP-902](tareas/CP-902-preguntas-abiertas.md) | Decidir los pendientes de docs/decisiones.md (P-5…P-7, GSS §14 y otros) | D | P2 | — | en-curso |
| [CP-903](tareas/CP-903-estado-y-ruta-del-proyecto.md) | Actualizar el estado del proyecto y la ruta de tareas listas | D | P2 | CP-901 | hecha |
| [CP-904](tareas/CP-904-vida-propia-del-habitante.md) | Revisar el diseño y las aceptaciones desde la vida propia del habitante | D | P1 | — | hecha |
| [CP-910](tareas/CP-910-catalogo-hitos.md) | Catálogo de hitos elegibles y variaciones de los existentes | Futuro | profunda | CP-306, CP-208 | dormida |
| [CP-911](tareas/CP-911-intenciones-arnes-voyager.md) | Intenciones propias potenciadas con un ejecutor al estilo del arnés de Stardew Voyager | Futuro | profunda | CP-302, CP-306 | dormida |

## Trazabilidad de reglas (GSS §13)

Ninguna regla está vigente hasta que su guarda exista (moratoria de prosa).

| Regla | Guarda | Tarea |
| --- | --- | --- |
| R-01 | `EscenaDeterministaTests` | CP-104 |
| R-02 | `EscenaSinAlucinacionTests` | CP-104 |
| R-03 | `GradienteDetalleTests` | CP-103 |
| R-04 | `PresupuestoPulsionesTests` | CP-202 |
| R-05 | `CuerpoNoBloqueaTests` | CP-203 |
| R-06 | `ValidadorIntencionTests` | CP-301 |
| R-07 | `ActosAntesDeFraseTests` | CP-501 |
| R-08 | `ActoRegalaTests` | CP-501 |
| R-09 | `CompromisoSalienciaTests`, `ProrrogaCompromisoTests` | CP-502, CP-509 |
| R-10 | `MemoriaConPartidaTests` | CP-107 (diario), CP-403 (obras) |
| R-11 | `DegradacionVanillaTests` | CP-303 |
| R-12 | `ConvivenciaValleyTalkTests` | CP-505 |
| R-13 | `EsquemaIntencionTests` | CP-002, CP-301 |
| R-14 | `LexicoUnicoTests` | CP-105 |
| R-15 | `MencionesAncladasTests` | CP-506 |
| R-16 | `CambioRutinaTests` | CP-212, CP-306 |

## Verificaciones

Estado de V-01…V-09 en la ADS §16; detalle en `docs/notas-juego.md`.
