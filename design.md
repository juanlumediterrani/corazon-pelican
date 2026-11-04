# `DESIGN.md` — GSS · Especificación de Diseño del Sistema (*Corazón Pelican*)

> **Estado:** propuesta, 2026-11-03; revisada el 2026-11-04 (decisiones P-2, P-4, P-8 y P-11 en
> [`docs/decisiones.md`](docs/decisiones.md)). Nada de lo descrito existe todavía.
> **Documento hermano:** [`architecture.md`](architecture.md) (ADS: cómo se construye).
> **Regla de la casa:** como en Stardew Voyager (moratoria de prosa), ninguna regla de
> este documento se considera vigente hasta que exista la guarda o el test que la hace
> cumplir. La §13 enumera cada regla con su guarda prevista.

---

## 1. Visión General

* **Nombre:** *Corazón Pelican* — motor de habitantes para Stardew Valley 1.6.
* **Concepto nuclear:** dar a un personaje del valle una **mente que habita el juego**, no
  que lo juega. Un modelo de lenguaje recibe, en prosa, lo que el personaje percibe desde
  dentro del mundo; decide qué quiere; su cuerpo lo hace; y lo que crea se queda en el valle.
* **Primera instancia:** **Leah**, escultora y pintora. Su oficio (mirar y plasmar lo que
  mira) hace visible el sistema entero: percepción, atención, intención y obra.
* **Escena fundacional:** Leah está en la pasarela de madera del lago, con el cuaderno en las
  rodillas. Alguien se acerca y le pide que dibuje. Lo que dibuje nacerá de lo que *ve* ahora,
  de lo que recuerda y de quién se lo pide — no de un menú.

### 1.1. Pilares

1. **Habitar, no jugar.** Leah no tiene puntuación. Su éxito es la coherencia: que lo que
   percibe, piensa, dice y hace encaje con quién es.
2. **Cada atención es propia.** El mismo mundo, percibido por personajes distintos, produce
   prosas distintas. La percepción es la primera expresión del carácter.
3. **El juego es el único medio.** Ningún agente se comunica con otro por fuera del juego.
   Lo que no existe en el juego no existe para Leah (herencia directa del ADR 0002 de Voyager).
4. **Lo prometido se cumple.** Una frase que compromete a algo se convierte en intención
   validada o no se dice.
5. **El peor caso es la Leah de siempre.** Si la mente falla, Leah vuelve a su rutina canónica.
   Nunca queda congelada, muda o rota.

### 1.2. Motor y personaje

| Concepto | Qué es | Ejemplos |
|---|---|---|
| **Motor** | Sensorio, atención, pulsión, cuerpo, validación, persistencia. Común a todos los habitantes. | Cálculo de saliencia, integrador, ejecución de `ir_a` |
| **Personaje** | Datos que parametrizan el motor. Sustituibles sin tocar código. | Pesos de atención de Leah, su léxico, su voz, sus hitos de rutina, su portafolio |

El motor nunca contiene nada específico de Leah. Un segundo personaje (Maru, Elliott,
Linus…) se añade con un perfil nuevo, no con código nuevo.

### 1.3. Relación con los otros proyectos

* **Mod independiente y opcional.** Funciona en partida de un jugador sin nada más.
* **Con Pelican 4Ever** gana el reloj 1:1 (que relaja la latencia), el registro de sucesos
  como fuente de percepción y la vida persistente del servidor.
* **Con Stardew Voyager** comparte filosofía (ADR 0002 y 0003) y, en el juego, puede
  encontrarse con el granjero agente: lo hacen *dentro* del juego, como dos habitantes más.
* **ValleyTalk** es solo inspiración. Si está instalado, debe estar desactivado para los
  personajes que gobierna Corazón Pelican (§8.6).

---

## 2. El Bucle de Vida

```
mundo → sensorio → saliencia → prosa de acción (búfer)
                       │
                       ├─ reflejos (inmediato)
                       │
                       └─ integrador ─(umbral)→ PULSIÓN → mente → intención / actos
                                                                    │
cuerpo ← validación ←───────────────────────────────────────────────┘
   │
   └─ acciones en el juego → el mundo cambia → …
```

La **intención** cierra el bucle: modula qué es saliente, y la saliencia decide cuándo
vuelve a pensar. Una Leah absorta piensa poco; una Leah paseando, más. El coste sigue al
estado del personaje, no a un reloj.

### 2.1. Tres velocidades

| Capa | Ritmo | Quién decide | Qué produce |
|---|---|---|---|
| **Cuerpo** | Cada tick | Motor (determinista) | Movimiento, postura, prosa de acción |
| **Reflejo** | < 100 ms | Reglas del perfil (v1); modelo local con etiquetas cerradas (opcional) | Gestos: levantar la vista, saludar, guardar el cuaderno |
| **Pensamiento** | Segundos | Mente (LLM), solo tras una pulsión | Intención, actos de diálogo, decisión de obra |

**El cuerpo nunca espera al pensamiento.** Mientras la mente piensa, Leah sigue haciendo lo
que hacía. El retraso se lee como «tarda en reaccionar», natural en alguien concentrado.

---

## 3. Percepción (Sensorio)

### 3.1. Entidades perceptibles

El sensorio reúne, para la ubicación del personaje y su entorno, las entidades del juego:
granjeros, NPCs, animales y mascotas, objetos y muebles, terreno y agua, cultivos, clima,
luz y hora, sonidos derivables (lluvia, música del Saloon), y sucesos (Pelican, festivales,
regalos recibidos). Cada entidad tiene una **identidad estable** y una **posición** reales.

### 3.2. Saliencia

Cada entidad recibe una saliencia que combina cinco capas:

| Capa | Variación | Ejemplo para Leah |
|---|---|---|
| **Rasgos** | Estable (perfil) | Luz, color, agua, madera, formas naturales pesan más |
| **Estado** | Minutos | Dibujando: atención estrecha. Paseando: abierta. Cansada: más baja |
| **Intención** | Hasta la próxima pulsión | Buscando motivo: lo «pintable» gana peso |
| **Novedad** | Se desvanece con la exposición | Ha visto el lago mil veces; esta niebla, nunca |
| **Relación** | Días (corazones) | Con pocos corazones el jugador es «alguien»; con muchos, lo nota llegar de lejos |

Además, la **distancia y la línea de visión** atenúan toda saliencia física.

### 3.3. Gradiente de detalle (fóvea y periferia)

La saliencia no solo decide si algo entra en la prosa, sino **cuánto detalle** recibe:

| Banda | Tratamiento en prosa |
|---|---|
| **Foco** | Descripción rica: qué es, cómo es, qué hace |
| **Mención** | Una referencia breve |
| **Periferia** | Vaguedad deliberada: «alguien al fondo», «un ruido en el camino» |
| **Ausente** | No aparece. **Para el personaje no existe.** |

Consecuencia emergente buscada: si el jugador llega mientras Leah está absorta y su saliencia
no supera la banda de mención, Leah no se entera. No es un fallo; es un personaje concentrado.

### 3.4. Prosa de acción y escena

* La prosa la generan **plantillas deterministas** del perfil, nunca un LLM narrador. Así no
  aparecen garzas que no existen.
* Dos productos:
  * **Prosa de acción** (cada tick relevante): frases breves de lo que hace y nota
    («trazas la línea del muelle»). Alimenta el **búfer**.
  * **Escena** (al emitir una pulsión): segunda persona, presente, con el gradiente de detalle,
    más un **apéndice seco** de hechos duros y acciones disponibles.

Ejemplo de escena (ilustrativo, no normativo):

```
Es última hora de la tarde de otoño. El viento arrastra hojas sobre el agua, que
está gris y quieta junto a la pasarela. Tienes el cuaderno en las rodillas: el boceto
del sauce va por la mitad. Por el camino del norte se acerca alguien a paso tranquilo.

— Hechos —
hora 17:40 · otoño 12 · viento · energía media
cerca: granjero «Ana» (6 corazones, a 9 casillas, acercándose)
acciones: dibujar · levantar_la_vista · gesto(saludo) · guardar_cuaderno · ir_a(...)
lugares: pasarela_lago · orilla_norte · tu_casa
```

### 3.5. Léxico compartido

Los lugares, objetos y acciones tienen un **nombre único** que se usa igual en la percepción,
en la acción y en el diálogo. Si la escena dice «pasarela_lago», `ir_a(pasarela_lago)` es una
acción válida y Leah puede proponer verse allí. Leah actúa con las mismas palabras con las
que percibe; no hay capa de traducción donde se pierda la intención.

**Pensar en prosa, decidir en léxico.** Todo lo que la mente recibe es prosa anclada: la escena
cuenta «la pasarela de madera del lago» y el apéndice da el par `pasarela_lago` — «la pasarela de
madera del lago». La mente piensa en prosa (un `pensamiento` que va primero y que el arnés nunca
ejecuta) y luego decide **solo con ids y acciones que el pulso le ofrece**: cada pulso lleva su
propio esquema de respuesta, cerrado a lo que existe en ese momento. Si lo que quiere no tiene
nombre en el léxico, elige entre lo que hay o renuncia; nunca se inventa un lugar. Cuando el motor
rechaza algo, explica el motivo en prosa para que el personaje reformule con sus propias
palabras.

### 3.6. Atender es una acción

`mirar(objetivo)` y `levantar_la_vista` cambian el foco del sensorio; la siguiente escena se
reconstruye con esa atención. La percepción deja de ser algo que le llega y pasa a ser algo
que hace.

---

## 4. Pulsión de Atención

Una **pulsión** es el momento en que el motor decide que algo merece pensamiento y despierta
a la mente.

### 4.1. Disparadores

| Disparador | Condición | Ejemplo |
|---|---|---|
| **Pico** | Una entidad supera el umbral de pico | El jugador con muchos corazones se acerca |
| **Interlocución** | Alguien le habla | Clic del jugador sobre Leah |
| **Deriva** | El integrador de saliencias menores supera su umbral | Muchas cosas pequeñas sin pensar: divaga, se aburre |
| **Hito** | Termina un plan finito, de su rutina o de su intención, y se dispone con el siguiente (§5.3) | Leah: «voy a sentarme en la pasarela del lago» → llega y piensa «llegué». Pierre: abre o cierra la tienda y piensa en los clientes de ayer o de hoy y en lo que se llevaron |
| **Fin de intención** | Cumplida, caducada o **bloqueada** | Acaba el boceto; la pasarela está ocupada |
| **Compromiso** | Un plazo prometido se acerca | «Mañana al atardecer te dibujo el lago» |
| **Transición** | Cambio de luz o de clima | Se pone el sol; empieza a llover |

Los **hitos los dan las rutinas de los personajes**, literalmente: el horario que el propio
juego elige cada día para ese personaje. Ese horario cambia con la estación, el día de la
semana, la lluvia, los festivales, los corazones, el matrimonio y el avance de la partida
(cartas recibidas, Centro Cívico o Joja…). Cada entrada del horario de **hoy** es un plan finito
(«abrir la tienda», «ir al Saloon»); también lo es un plan que pidió la mente («ir a la pasarela
y sentarse»). Al terminar, el personaje vuelve a mirar desde donde está.

Una entrada del horario es un plan que dura desde su hora hasta la de la siguiente: va al sitio
y hace allí lo que toque. Su final es, por tanto, el momento de **disponerse con la siguiente**:
ahí es donde el personaje vuelve a pensar, y donde todavía puede cambiarla.

**Pensamiento en cascada.** La cadena de despertar ya **elige la agenda del día**: qué entradas
de su horario de hoy mantiene, cuáles cambia o sustituye y dónde encajan sus intenciones. Al
disponerse con el primer hito vuelve a pensar y **reequilibra** lo que queda de agenda con lo
que ha pasado desde la mañana; y así sucesivamente, hito a hito, hasta acostarse. Cada pulso de
hito lleva la agenda pendiente; la mente no rehace el día desde cero, lo ajusta.

El motor saca los hitos del horario del día; no hay una lista fija. El **perfil** solo los
**anota por patrón** (destino, animación, franja horaria) para decir qué le hacen pensar: qué
hechos acompañan al pulso (la luz y la obra en curso para Leah; las ventas para Pierre), si
abren cadena o si se pueden cambiar. Una entrada sin anotación es un hito genérico. El motor no
sabe nada de tiendas ni de pasarelas: solo de planes que terminan.

Si durante el plan ya pensó (p. ej. por deriva con lo que vio por el camino), el hito puede
omitirse: la mente ya decidió con información más reciente. El perfil decide, por hito, si se
omite o si se piensa igual (cerrar la tienda merece pensarse aunque haya pensado antes).

**Despertar** y **acostarse** son el primer y el último hito de cualquier rutina, comunes a
todos los personajes. Como cualquier hito que el perfil marque así, no lanzan un pensamiento
suelto sino una **cadena de pensamientos**: varios pasos encadenados de la mente sobre la misma
pulsión, sin actuar en el mundo entre ellos.

| Cadena | Cuándo | Pasos |
|---|---|---|
| **Despertar** | Al empezar su día | Qué recuerdo de ayer y qué tengo pendiente (recuerdos, compromisos) → cómo amanece y cómo me siento (estación, clima, ánimo) → qué quiero hoy: **la agenda del día** (qué entradas de mi horario mantengo, cambio o sustituyo, y mis intenciones) |
| **Acostarse** | Al irse a dormir | Qué pasó hoy (diario del día) → qué significó (diario → recuerdos) → qué pienso de quienes vi (relaciones) → qué queda para mañana (compromisos) |

Cada cadena cuenta como **una** pulsión. El presupuesto reserva dos al día para ellas, de modo
que Leah siempre empieza y cierra el día pensando, aunque se haya agotado el resto.

### 4.2. Integrador, refractario y presupuesto

* El **integrador** acumula saliencias menores (periferia y mención) con fuga (lo antiguo pesa
  menos), en minutos de juego, como la novedad. Se vacía en cada pulsión: lo acumulado ya se pensó.
* Tras cada pulsión se abre un **período refractario** en tiempo real: durante una ventana no pasan
  los disparadores de estímulo (pico, deriva, transición; las transiciones esperan su turno) y
  después sus umbrales siguen elevados y decaen hacia su valor base. La rutina (hitos, despertar,
  acostarse, fin de intención, compromiso) y la interlocución pasan siempre, para no llegar tarde a
  lo que toca.
* Un hito de rutina se dispara **diez minutos de juego antes** de la hora de su entrada: al
  disponerse, cuando todavía se puede cambiar. La luz y el clima con que amanece un día no cuentan
  como transición.
* Hay un **presupuesto de pulsiones** por personaje y día de juego, con dos reservadas para
  las cadenas de despertar y acostarse. Agotado, solo pasan esas dos y la interlocución; el resto
  del tiempo Leah vive de intención, hábito y reflejo.
* **El presupuesto lo controla el motor, nunca la mente.**
* La **cadencia de pensamiento no se programa: emerge** de los disparadores y se mide (§7).

### 4.3. Pensamiento anticipado

Si una pulsión es probable (la saliencia del jugador sube pero aún no cruza el umbral), el
motor puede lanzarla por adelantado. Si al final no se produce el encuentro, se cancela.
Objetivo: que cuando el jugador hable con Leah, la respuesta ya esté pensada.

### 4.4. El búfer que recibe la mente

La mente no recibe todos los ticks. El motor **consolida** el búfer: conserva solo lo que
llegó al menos a la banda de **mención** del personaje, con más detalle para lo reciente y menos
para lo antiguo (la misma fóvea/periferia, aplicada al tiempo). No se resume con otro LLM.

El búfer es **tan propio como la percepción** (pilar 2): lo que entra lo decide la saliencia de
ese personaje (sus rasgos, su estado, su intención, la novedad y sus relaciones, §3.2) y sus
umbrales, y cómo se cuenta, sus plantillas. Por el mismo camino, Leah y Pierre recuerdan cosas
distintas de la misma tarde.

**Huella de atención.** Entre un pensamiento y el siguiente, los estímulos que no han disparado
nada no se pierden. Además del búfer (qué ha pasado), el motor acumula por cada entidad **cuánta
atención** ha recibido: el tiempo en foco o mención por su saliencia, con fuga para que lo antiguo
pese menos. El siguiente pulso lleva:

* una o dos frases deterministas con lo más atendido («durante el camino te has fijado sobre
  todo en la niebla sobre el lago; Ana pasó dos veces»), con las plantillas del perfil;
* el tiempo transcurrido desde el último pensamiento.

Así lo vivido entre hitos **tiñe la perspectiva** del siguiente pensamiento: no es lo mismo
llegar a la pasarela tras una tarde mirando el agua que tras cruzar el pueblo en día de mercado.
La interpretación es de la mente; el motor solo mide y cuenta. Como la escena, la huella solo
contiene entidades que estuvieron en la observación.

---

## 5. Intención y Cuerpo

### 5.1. Intención

La mente devuelve una **intención**: qué quiere conseguir, por qué, con qué plazo y cómo
modula su atención (p. ej. «acabar el boceto antes de que se vaya la luz» → peso alto a la luz
y al cuaderno, bajo a los transeúntes).

Ciclo de vida:

```
propuesta → validada → activa → cumplida | bloqueada | caducada | sustituida
                │
                └→ rechazada (precondiciones falsas o mundo cambiado)
```

Cada intención declara la pulsión de la que nace. Si el mundo ha cambiado tanto que sus
precondiciones ya no se cumplen, se rechaza y se pide otra pulsión (§4.1, fin de intención).

### 5.2. Vocabulario de acciones (v1)

| Acción | Efecto |
|---|---|
| `ir_a(lugar)` | Camina por rutas nativas a un lugar del léxico |
| `sentarse(lugar)` | Se sienta donde el lugar lo permita |
| `mirar(objetivo)` / `levantar_la_vista` | Cambia el foco del sensorio |
| `dibujar(obra)` | Postura de dibujo; inicia o continúa una obra (§6) |
| `guardar_cuaderno` | Interrumpe la obra en curso sin perderla |
| `gesto(tipo)` | Emote nativo (sorpresa, alegría, duda…) |
| `murmurar(texto)` | Frase breve sobre la cabeza, sin abrir diálogo |
| `esperar(duración)` | Permanece; la atención sigue activa |
| `volver_a_rutina` | Devuelve el control al horario canónico |

La mente decide **qué** («quiero la orilla con esta niebla»). El cuerpo decide **cómo**
(ruta, casilla, orientación). La mente no elige casillas.

### 5.3. Rutina: planes finitos de los que el personaje es dueño

* La rutina canónica es el horario vanilla **que el juego elige cada día** (estación, día,
  clima, corazones, avance de la partida). Es una sucesión de planes finitos cuyos finales son
  **hitos** (§4.1): ahí nacen buena parte de los pensamientos del personaje. Como el horario
  cambia, los hitos también, sin tocar el perfil.
* Sin intención activa, Leah sigue su horario. Una intención lo suspende temporalmente; al
  terminar, `volver_a_rutina` la reincorpora al tramo horario que le corresponda.
* La **agenda del día** (§4.1) es la rutina de hoy tal como la ha decidido el personaje: la
  elige al despertar y la reequilibra en cada hito.
* **El personaje controla su rutina.** La mente puede proponer **saltar**, **mover** o
  **sustituir** un hito próximo: Leah, «hoy no voy al Saloon, quiero acabar el lago»; Pierre,
  «hoy cierro la tienda para estar con la familia». Es parte de una intención y se valida como
  ella, contra el perfil: qué entradas de su horario de hoy se pueden cambiar, de qué forma, con qué frecuencia y en
  qué condiciones (p. ej., nunca un día de festival).
* **v1: solo cambios sin efecto mecánico** (los de Leah). Un cambio que altera el juego para los
  jugadores, como cerrar una tienda, es pregunta abierta (§14): exigiría que fuese visible
  para ellos y límites de frecuencia, sobre todo en un servidor persistente.

---

## 6. La Obra

### 6.1. Decisión y ejecución separadas

1. **Decisión (mente):** tema, composición, título, intención de paleta, una frase sobre qué
   quería capturar y cómo se siente con el resultado. La obra nace de la escena y de la memoria.
2. **Ejecución (taller):** convierte la decisión en sprite, cuantizado a la paleta del juego y a
   la resolución del formato.
3. **Autocrítica (mente):** mira el resultado y lo acepta o lo repite, con un límite de intentos.
   Descartar bocetos es completamente canónico.

Mientras el taller trabaja, el cuerpo de Leah **está dibujando**. La latencia del render se
esconde dentro del propio acto.

### 6.2. Alcance v1: obras decorativas

* Cuadros y bocetos colocables como **muebles decorativos**, regalables y, opcionalmente,
  vendibles a un precio fijo por formato.
* Las obras **no tienen efectos mecánicos** en v1. Inventar objetos con efecto exigiría un
  sistema de balance; queda como pregunta abierta (§14).
* Escultura: fuera de v1 (requiere formatos y animaciones propias).

### 6.3. Portafolio

Cada obra queda registrada con su decisión, su nota, la escena de la que nació, su estado
(en curso, terminada, descartada, regalada, vendida) y su destinatario. El portafolio da
continuidad («la última vez pinté el lago en verano; ahora lo quiero con el agua gris»).

### 6.4. Apertura según la relación

| Corazones | Comportamiento por defecto (ajustable en el perfil) |
|---|---|
| 0–2 | Cierra el cuaderno si alguien mira. Rechaza encargos |
| 3–5 | Enseña bocetos si se le pide. Acepta encargos sencillos |
| 6–8 | Ofrece enseñar su trabajo. Acepta encargos y propone temas |
| 9+ | Regala obras por iniciativa propia |

Que diga «hoy no me sale» o entregue un dibujo con vergüenza vale más que cualquier sprite.

---

## 7. Tiempo

| Modo | 10 min de juego | Implicación |
|---|---|---|
| **Vanilla** | ~7 s reales | El pensamiento anticipado es imprescindible. Pocas pulsiones por hora real |
| **Pelican 4Ever** | 10 min reales | Un pensamiento de varios segundos es imperceptible para el mundo. Solo la interlocución exige rapidez |

**Cadencia esperada (a medir, no a programar):** del orden de un pensamiento cada 5–15 min
reales en vanilla y cada 4–6 h en Pelican, más las interrupciones puntuales. La fase 2 mide la
cadencia que dan deriva, hitos y el resto de disparadores en ambos modos; si se aleja de lo
esperado, se ajustan umbrales, fuga y presupuesto del perfil, no se añade un temporizador.

Reglas comunes:

* Plazos de intención y compromisos en **tiempo de juego**.
* Refractario y presupuesto de latencia en **tiempo real**.
* Presupuesto de pulsiones por **día de juego**, con valor propio por modo de reloj.

---

## 8. Diálogo y Actos

### 8.1. Principio

El diálogo es donde las palabras se convierten en compromisos. Cada respuesta de Leah es una
**frase más una cola de actos** con vocabulario cerrado. La frase solo se muestra si **todos
sus actos son válidos**.

### 8.2. Actos de habla (v1)

| Acto | Efecto en el motor |
|---|---|
| `promete(obra, tema, cuándo)` | Registra un compromiso con plazo (§9.3) |
| `regala(obra_id)` | Entrega una obra del portafolio. Solo válida si existe y está disponible |
| `cita(lugar, cuándo)` | Compromiso de estar en un lugar del léxico a una hora |
| `enseña(obra_id)` | Muestra una obra (en curso o terminada) |
| `rechaza(motivo)` | Sin efecto material; queda en memoria |
| `acepta_regalo` | Reacción a un regalo recibido (el regalo en sí lo resuelve el juego) |

### 8.3. Flujo de una conversación

1. El jugador habla con Leah → **pulsión de interlocución** (máxima prioridad).
2. **Reflejo inmediato:** gesto y línea breve nativa («Leah levanta la vista del cuaderno»).
3. Si hay pensamiento anticipado, se usa; si no, la mente responde ahora.
4. El motor **valida los actos**. Si alguno falla, devuelve el motivo en prosa y la mente
   reformula una vez; si vuelve a fallar, Leah responde con su diálogo vanilla del día.
5. Se muestra la frase, se aplican los actos, se escribe en memoria.

### 8.4. Opciones del jugador

Se ofrecen 3 o 4 respuestas sugeridas **generadas a partir de las acciones realmente
disponibles** (regalar algo del inventario, pedir un dibujo, invitarla a un lugar), más una
opción de **texto libre**. Las opciones usan componentes nativos de diálogo, de modo que un
humano y un agente (p. ej. el granjero de Voyager) las operan igual.

### 8.5. Coherencia

* Leah nunca menciona entidades que no estén en su escena o su memoria.
* Leah nunca promete lo que el motor no puede cumplir.
* Leah puede negarse, dudar o cambiar de tema: no es una asistente.

### 8.6. Convivencia con ValleyTalk

ValleyTalk sigue disponible para el resto del pueblo. Para los personajes gobernados por
Corazón Pelican debe estar desactivado; si el motor detecta lo contrario, avisa en consola y
no intercepta ese personaje hasta que se resuelva (nunca dos voces para una misma persona).

---

## 9. Memoria

### 9.1. Una sola verdad

Hay **una única memoria por personaje**. La mente decide qué recordar; el motor la custodia y
la persiste **con la partida**. Ninguna otra capa (diálogo, telemetría, mods externos) mantiene
una memoria paralela del personaje.

### 9.2. Contenido

| Tipo | Contenido |
|---|---|
| **Diario** | Episodios consolidados del búfer, con fecha de juego y saliencia |
| **Recuerdos** | Reflexiones que la mente extrae del diario (lo que el episodio *significó*) |
| **Relaciones** | Impresiones propias de cada persona, además de los corazones nativos |
| **Portafolio** | Obras (§6.3) |
| **Compromisos** | Promesas y citas con plazo y estado |

El diario envejece: lo antiguo se condensa en recuerdos y se poda, con un tope de tamaño.
La condensación del día ocurre en la cadena de acostarse (§4.1).

### 9.3. Compromisos

Al validarse un `promete` o una `cita`, nace un compromiso con plazo. A medida que el plazo se
acerca, **su saliencia sube** hasta provocar una pulsión de compromiso. Las promesas vuelven
a la conciencia por el mismo mecanismo de atención que todo lo demás, sin un planificador
aparte. Un compromiso incumplido no se oculta: Leah lo recuerda y puede disculparse.

### 9.4. La memoria viaja con el mundo

La memoria se guarda en el mismo momento y lugar que la partida. Si la partida se recarga o se
pierde un día por un cierre abrupto, **la memoria vuelve al mismo punto**: Leah nunca recuerda
algo que el mundo no vivió.

---

## 10. Multijugador y Agentes

* **Autoridad del host.** Los NPCs los controla el host; el motor de un personaje corre solo ahí.
* **Los clientes necesitan el mod** para ver las obras (sus texturas) y la interfaz de diálogo.
* **Varios interlocutores.** Leah atiende a quien tenga más saliencia; los demás quedan en
  mención o periferia («hay más gente esperando»).
* **Agentes.** Un granjero agente es, para Leah, un granjero más. Leah no sabe que lo es y no
  hay canal privado entre mentes.

---

## 11. Modos y Degradación

| Situación | Comportamiento |
|---|---|
| Sin mente (caída, sin configurar, sin presupuesto) | Leah vanilla; sensorio y diario siguen activos si se desea |
| Mente lenta | El reflejo cubre; tras el tiempo máximo, diálogo vanilla del día |
| Sin taller | Leah dibuja, pero las obras quedan como bocetos sin sprite (no colocables) |
| Sin Pelican | Reloj vanilla; sin sucesos de servidor |
| Con Pelican | Reloj 1:1, sucesos de servidor como percepción, Leah excluida del *micro-wandering* mientras tenga intención activa (requiere un cambio pequeño en Pelican, ver ADS §11.1) |
| Mod desinstalado | Las obras colocadas se convertirían en objetos de error: ver mitigación en la ADS |

---

## 12. Criterios de Aceptación del Diseño

| Criterio | Medida observable |
|---|---|
| **Sin alucinación de mundo** | 100 % de las entidades nombradas por la mente existen en su escena o su memoria (R-15) |
| **Promesas cumplidas** | Todo compromiso válido termina cumplido o recordado como incumplido; ninguno se pierde |
| **Reflejo inmediato** | La primera reacción visible a una interlocución llega en < 300 ms |
| **Respuesta pensada** | p50 < 3 s en vanilla (con anticipación); p95 < 8 s |
| **Coste acotado** | Pulsiones por día de juego ≤ presupuesto configurado (cada cadena cuenta como una), sin excepciones salvo interlocución |
| **Degradación** | Con la mente apagada, ninguna diferencia de comportamiento con la Leah vanilla |
| **Coherencia de carácter** | Evaluación humana sobre muestras de escenas/pensamientos (rúbrica en el backlog) |

---

## 13. Reglas Verificables

Cada regla entra en vigor con su guarda. Los nombres de test son previstos.

| ID | Regla | Guarda prevista |
|---|---|---|
| R-01 | La prosa de escena se genera solo con plantillas deterministas | `EscenaDeterministaTests`: misma entrada, misma salida |
| R-02 | Toda entidad mencionada en la escena existe en el estado observado | `EscenaSinAlucinacionTests` |
| R-03 | Entidades bajo la banda de periferia no aparecen en la escena | `GradienteDetalleTests` |
| R-04 | El presupuesto de pulsiones lo aplica el motor | `PresupuestoPulsionesTests` |
| R-05 | El cuerpo nunca bloquea esperando a la mente | `CuerpoNoBloqueaTests` (FakeWorld con mente lenta) |
| R-06 | Intención con precondiciones falsas se rechaza | `ValidadorIntencionTests` |
| R-07 | Una frase solo se muestra si todos sus actos son válidos | `ActosAntesDeFraseTests` |
| R-08 | `regala` solo acepta obras existentes y disponibles | `ActoRegalaTests` |
| R-09 | Todo compromiso válido genera una pulsión antes de su plazo | `CompromisoSalienciaTests` |
| R-10 | La memoria se persiste y se restaura con la partida | `MemoriaConPartidaTests` (guardar/cargar/recargar) |
| R-11 | Sin mente, el comportamiento es el vanilla | `DegradacionVanillaTests` |
| R-12 | Con ValleyTalk activo para un personaje, el motor no lo intercepta | `ConvivenciaValleyTalkTests` |
| R-13 | La mente no elige casillas ni rutas | Esquema de salida sin coordenadas + `EsquemaIntencionTests` |
| R-14 | El léxico es único y compartido por percepción, acción y diálogo | `LexicoUnicoTests` |
| R-15 | Toda entidad que nombra la mente (frase, intención, obra) existe en su escena o su memoria | Validador de menciones + `MencionesAncladasTests` |
| R-16 | Un cambio de rutina solo se aplica si el perfil permite ese cambio en ese hito y se respetan sus límites | `CambioRutinaTests` |

---

## 14. Fuera de Alcance (v1), Riesgos y Preguntas Abiertas

**Fuera de alcance:** escultura; obras con efectos mecánicos; cambios de rutina con efecto mecánico; varios habitantes simultáneos
(el motor lo admite, pero v1 valida solo a Leah); conversaciones entre NPCs gobernados;
eventos con guion propios.

**Direcciones futuras (fuera de v1):**

* **Más hitos y más variación:** un catálogo de hitos posibles que el personaje pueda elegir
  además de los de su horario, y formas de variar los existentes (otro sitio, otra hora, otra
  actividad), siempre declarados como datos del perfil.
* **Intenciones propias potenciadas:** las intenciones particulares (las que no salen de la
  rutina) necesitarán un ejecutor más capaz, con funciones parecidas a las del arnés de Stardew
  Voyager: acciones registradas con precondiciones y postcondiciones observables, rutas y
  colisión nativas, «¿puedo actuar aquí y qué me falta?», resolver en vez de rechazar y una lista
  de objetivos que un ejecutor determinista cumple con presupuesto. Se comparte el diseño, nunca
  el contexto entre mentes (pilar 3).

**Riesgos de diseño:**

* **Leah servicial.** El mayor riesgo para la ilusión. Mitigación: tabla de apertura (§6.4),
  derecho explícito a negarse, rúbrica de carácter.
* **Monotonía perceptiva.** Plantillas repetitivas. Mitigación: novedad en la saliencia,
  variantes por plantilla, revisión en la fase 1 (solo sensorio).
* **Pulsiones en ráfaga** en mundos agitados (festivales). Mitigación: refractario y presupuesto.
* **Expectativa de texto libre.** Los jugadores escribirán cualquier cosa. La validación de
  actos acota consecuencias, no temas.

**Preguntas abiertas:**

1. ¿Obras con efecto (v2)? ¿Con qué sistema de balance?
2. ¿Venta de obras: precio fijo por formato o valoración por el propio personaje?
3. ¿Segundo personaje: cuál pone mejor a prueba que el motor no contiene nada de Leah?
4. ¿Debe Leah poder iniciar conversación (acercarse ella al jugador) en v1?
5. ¿Cambios de rutina con efecto mecánico (Pierre cierra la tienda un día)? ¿Cómo se avisa a los
   jugadores (cartel, carta, aviso de Pelican) y con qué límites?
