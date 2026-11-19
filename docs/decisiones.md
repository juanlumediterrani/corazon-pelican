# Decisiones y pendientes

Registro de las decisiones tomadas sobre la GSS ([`design.md`](../design.md)) y la ADS
([`architecture.md`](../architecture.md)) y de lo que falta por decidir. Los identificadores
P-x se citan desde los documentos y las tareas. La [ADS §15](../architecture.md#15-decisiones-de-arquitectura) enlaza las seis decisiones de arquitectura documentadas en CP-901.

## 1. Decisiones tomadas

P-1 a P-4, P-8 y P-11 se acordaron el 2026-11-04.

### P-1 · Época de mundo en el contrato

La memoria se guarda solo con la partida, así que al recargar, los `id` de pulso y de obra vuelven
atrás, mientras que la mente conserva su cursor y su caché. Cada carga de mundo genera una
`epoca`: identificador opaco y único (p. ej. ULID), no persistido en la partida y que no se repite
entre procesos. Califica cursores, idempotencia y obras.
→ ADS §3.3, §3.4, §4.1, §5 · CP-002, CP-203, CP-403 · ADR-0005.

### P-2 · De dónde nace el pensamiento y con qué cadencia

- **La cadencia no se programa: emerge de los disparadores y se mide** (CP-208). Expectativa de
  partida: del orden de un pensamiento cada 5–15 min reales en vanilla y cada 4–6 h en Pelican,
  que en días de juego equivale a unos 3–4 al día en ambos modos. Si la medida se aleja, se
  ajustan umbrales, fuga, anotaciones y presupuesto; **no se añade un temporizador**. (Se probó y
  se descartó un «latido» temporizado: sustituía a la deriva y era un reloj de consultas.)
- **La deriva se mantiene** como estaba en el diseño: acumulación con fuga de estímulos menores.
- **Hitos: los dan las rutinas de los personajes, literalmente.** Un hito es el final de un plan
  finito. Los de rutina salen del horario que el juego elige cada día (estación, día, clima,
  festival, corazones, matrimonio, avance de la partida); cada entrada dura de su hora a la de la
  siguiente y su final es el momento de **disponerse con la siguiente**. Los de intención salen
  de los planes de la mente. El perfil no lista hitos: los **anota por patrón** (destino, animación,
  franja) para decir qué hechos acompañan al pulso, si abren cadena, si se omiten cuando ya pensó
  y qué se puede cambiar. El motor solo sabe de planes que terminan.
- **Despertar y acostarse** son hitos del motor, comunes a todos, que abren **cadenas de
  pensamientos** (varios pasos encadenados, una sola pulsión), con dos plazas reservadas en el
  presupuesto. La de acostarse incluye la reflexión diario → recuerdos.
- **Pensamiento en cascada:** el despertar elige la **agenda del día**; cada hito la reequilibra.
- **El personaje es dueño de su rutina:** puede saltar, mover o sustituir entradas de su horario
  de hoy, validado contra el perfil (R-16). En v1, solo cambios sin efecto mecánico.
- El presupuesto sigue siendo por día de juego, con valor por modo de reloj (`ModoReloj`).
→ GSS §1.2, §4.1, §4.2, §5.3, §7, §9.2, §12, §13, §14 · ADS §2, §3.1–3.4, §4.1, §6, §12, §14, §15 ·
CP-002, CP-003, CP-011, CP-201, CP-202, CP-206–CP-209, CP-302, CP-305, CP-306 · ADR-0006.

### P-3 · Taller: de la prosa a un asset de verdad

Dos piezas: un **generador** enchufable (Space de Hugging Face adecuado o modelo de imagen capaz;
SVG del LLM como respaldo sin red) y un **script de asset** determinista
(`python -m taller.asset`: recorte, reducción, cuantización a la paleta, limpieza, PNG + vista a
×4). El spike se adelanta a justo después de CP-001. Revisión del modelo fijada, licencia
registrada, nombres de jugadores fuera del prompt; si el servicio falla, la obra queda en boceto.
→ ADS §7 · CP-013, CP-401. Candidatos en [notas-juego](notas-juego.md) §11.

### P-4 · R-15: la mente no nombra lo que no existe para el personaje

R-02 protege la escena del motor; R-15 protege lo que dice la mente (frase, intención, obra).
→ GSS §12, §13 · CP-506.

### P-8 · Huella de atención entre pensamientos

Los estímulos que no disparan un pensamiento no se pierden: además del búfer (qué ha pasado), el
motor acumula **cuánta atención** recibió cada entidad desde el último pensamiento y el pulso
lleva una frase determinista con lo más atendido y el tiempo transcurrido. Tiñe la perspectiva del
siguiente pensamiento; la interpretación es de la mente. Búfer y huella son propios de cada
personaje (su saliencia, sus bandas, sus plantillas); el búfer admite desde la banda de mención.
→ GSS §4.4 · ADS §2, §3.3, §4 · CP-002, CP-104, CP-106.

### P-11 · Pensar en prosa, decidir en léxico

La mente recibe prosa anclada (escena + apéndice con pares id — prosa), piensa en prosa en un
campo `pensamiento` que va primero y que el arnés nunca ejecuta, y decide en campos efectivos
cerrados. Cada pulso lleva su propio esquema de respuesta, estrechado a las acciones e ids que
ofrece, como subconjunto del contrato de `schemas/`; el motor sigue validando contra el mundo
vigente. Los rechazos vuelven con código (arnés) y prosa (personaje). Nunca se inventa un lugar.
→ GSS §3.5, §8.3 · ADS §3.3, §3.4, §4.1, §5, §6 · CP-002, CP-104, CP-204, CP-301, CP-501.

### P-6 · El diario se guarda desde la fase de percepción (2026-11-19)

El diario de cada personaje viaja con la partida desde F1. Al cargar, reaparecen los episodios
del último guardado y se pierden los posteriores. F4 amplía ese mismo estado con las obras.
CP-107 cubrirá el diario con `MemoriaConPartidaTests`, CP-108 lo comprobará en DEV y CP-403
ampliará la prueba al portafolio.

### P-12 · Cupo extra para compromisos y prórroga acordada (2026-11-19)

Cuando un compromiso requiere pensar, el motor usa primero una plaza ordinaria disponible para
ese fin. Al agotarse esas plazas puede gastar una de la bolsa diaria exclusiva de compromisos,
configurable por personaje y modo de reloj. Las dos reservas de despertar y acostarse siguen
protegidas. La interlocución pasa siempre y se contabiliza aparte.

Si tampoco queda cupo extra, Leah pide más tiempo. Solo la aceptación del jugador cambia el
plazo. Si el destinatario está ausente, Leah le envía una carta con la propuesta y una vía
para aceptar o rechazar. Al vencer la fecha vigente sin acuerdo, el compromiso pendiente
queda incumplido. La carta sigue disponible para lectura y desde ese momento ya no admite
respuesta. CP-202 probará la
contabilidad, CP-502 el vencimiento y CP-509 la petición y respuesta del destinatario.

### P-9 · El humor recoge cómo el personaje interpreta lo vivido (2026-11-19)

El personaje puede cambiar pequeñas pautas de conducta durante varios días a raíz de sus
experiencias en el valle. La mente interpreta un episodio según su carácter y sus recuerdos.
El motor guarda esa impresión con la partida y limita sus efectos. En los días tranquilos que
siguen, ese efecto se atenúa poco a poco con el tiempo vivido; una experiencia significativa
puede cambiarlo antes. La huella de atención (P-8) sigue describiendo qué le llamó la atención
desde la última pulsión.

Por ejemplo, tras varias tardes trabajando a gusto en casa, Leah puede querer seguir allí un
rato. Si vivió esas tardes como aislamiento, puede guardar antes el cuaderno para salir. El
perfil permite ambas elecciones. La duración y la intensidad se calibrarán con muestras. CP-210
prepara la prueba entre días, CP-208 evalúa la interpretación y CP-305 observa la conducta.

## 2. Pendiente de decidir

| # | Propuesta o pregunta | Recomendación | Tarea |
|---|---|---|---|
| P-5 | La espera en el diálogo se mide en paciencia, no en tiempo de juego: en un jugador, la `DialogueBox` del reflejo para el reloj; en multijugador, no | Diseñar el reflejo como «está pensando» visible y medir la latencia real en ambos modos | CP-503 |
| P-7 | Interceptar sin sustituir: dejar al juego amistad diaria, misiones, regalos, cónyuge y eventos; el motor solo cambia qué se dice | Aceptar (ver [notas-juego](notas-juego.md) §2) | CP-010, CP-503 |
| GSS §14.1 | ¿Obras con efecto? | Fuera de v1; reabrir tras CP-508 | CP-902 |
| GSS §14.2 | Venta de obras | Precio fijo por formato en v1 | CP-902 |
| GSS §14.3 | Segundo personaje | Elliott: su obra es texto, obliga a que «obra» no sea «sprite» | CP-902 |
| GSS §14.4 | ¿Leah inicia conversación en v1? | No como diálogo; sí acercarse + `murmurar`/`gesto` cuando la apertura lo permita | CP-902 |
| GSS §14.5 | Cambios de rutina con efecto mecánico (Pierre cierra la tienda) | Fuera de v1; en v2 con aviso visible, límites en el perfil y coherencia con el canon (Pierre ya cierra los miércoles) | CP-902 |
| P-10 | Velocidad de consolidación del búfer y de fuga de la huella por personaje (alguien distraído olvida antes) | Parámetros del perfil con valores por defecto del motor | CP-106 |
| — | Inscribir el proyecto en Dr4co Core | Decisión del usuario; el backlog ya usa su formato | — |
| — | UniqueID del mod | `Dr4co.CorazonPelican` (propuesto) | CP-001 |

## 3. Riesgos técnicos abiertos

| Riesgo | Mitigación | Tarea |
|---|---|---|
| Interceptar la conversación borra la amistad diaria o bloquea *Introductions* | P-7 y prueba en vivo | CP-010 |
| Una entrada de horario perdida deja a Leah fuera de sitio el resto del día | Recalcular la ruta al tramo vigente | CP-011 |
| Las obras quedan ilegibles a la resolución de un cuadro | Spike temprano con varias vías y el mismo script | CP-013 |
| Recargar desincroniza mente y motor | Época (P-1) | CP-002, CP-203 |
| Coste alto en vanilla con sesiones largas | Medir la cadencia, ajustar anotaciones y presupuesto | CP-207, CP-208 |
| La reflexión de acostarse llega después del guardado y se pierde si se cierra el juego antes del siguiente | Aplicarla al llegar y guardarla en el siguiente guardado; aceptar la pérdida | CP-209 |
| La voz copia demasiado texto canónico | Paráfrasis y rasgos; citar solo lo imprescindible | CP-205 |
| Nombres de jugadores enviados al proveedor del LLM | Documentarlo y permitir seudónimos; nunca en prompts de imagen | CP-204, CP-401 |
| Un generador externo cambia, cae o tiene licencia no comercial | Revisión fijada, licencia registrada, respaldo local | CP-013, CP-401 |
