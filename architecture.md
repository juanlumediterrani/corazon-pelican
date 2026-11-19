# `ARCHITECTURE.md` — ADS · Especificación de Arquitectura (*Corazón Pelican*)

> **Estado:** propuesta, 2026-11-03; revisada el 2026-11-04 (decisiones P-1, P-2, P-3 y P-11 en
> [`docs/decisiones.md`](docs/decisiones.md)). Nada de lo descrito existe todavía.
> **Hechos del juego comprobados:** [`docs/notas-juego.md`](docs/notas-juego.md).
> **Documento hermano:** [`design.md`](design.md) (GSS: qué hace y por qué).
> **Convención:** lo marcado **[verificar]** depende de un detalle de Stardew Valley 1.6.15 o de
> SMAPI que debe confirmarse en el decompilado o en vivo antes de implementarse. Ninguna
> afirmación marcada así se da por cierta.

---

## 1. Stack y Topología

* **Plataforma:** Stardew Valley **1.6.15**, **SMAPI 4.1+**, **Harmony 2.x**.
* **Motor:** mod SMAPI en **.NET 6** + biblioteca de dominio pura `CorazonPelican.Core`
  (.NET 6, sin referencias a Stardew, SMAPI ni Harmony). Tests en **xUnit / .NET 8**, igual que
  Pelican 4Ever.
* **Mente y taller:** **Python ≥ 3.10**, procesos separados del juego.
* **Transporte motor ↔ mente:** HTTP por loopback (`127.0.0.1`, puerto por defecto `25560`),
  token opcional. Distinto del puente de Voyager (`25555`) y de la API de anfitrión de Pelican
  (`8085`).

```
┌──────────────────── Proceso host de Stardew Valley ────────────────────┐
│                                                                         │
│  [Pelican 4Ever]*        [Puente Voyager]*        [Corazón Pelican]     │
│   reloj, sucesos ──────────────────────────────▶   sensorio · atención  │
│                                                    cuerpo · validación  │
│                                                    persistencia · API   │
└──────────────────────────────────────────────────────────┬──────────────┘
          ▲                         ▲                       │ HTTP :25560
          │ multijugador            │ HTTP :25555           │ (la mente es cliente)
    [Clientes humanos]*       [Agente Voyager]*      ┌──────▼──────┐
     (con Corazón instalado)                          │    Mente    │──▶ LLM
                                                      └──────┬──────┘
                                                             │ trabajos de render
                                                      ┌──────▼──────┐
                                                      │   Taller    │
                                                      └─────────────┘
   * opcional
```

**Principios de topología:**

1. **El juego nunca hace llamadas salientes bloqueantes.** El motor es servidor; la mente es
   cliente. Si la mente cae, el juego no lo nota más allá de la degradación (GSS §11).
2. **El juego es el único medio entre agentes.** No hay canal entre la mente de Leah y el
   agente Voyager. Se encuentran en el juego.
3. **Toda mutación del mundo ocurre en el hilo principal.** Las peticiones HTTP se encolan y se
   drenan en `UpdateTicked`, con el mismo patrón que `MainThreadActionQueue` de Pelican.

---

## 2. Estructura de la Solución

```text
CorazonPelican.sln
├── CorazonPelican.Core/                 # Dominio puro (.NET 6). Todo lo que se puede testear sin juego
│   ├── Perfil/
│   │   ├── PerfilPersonaje.cs           # Rasgos, umbrales, léxico, plantillas, apertura, voz
│   │   └── PerfilValidator.cs           # Validación determinista del perfil al cargar
│   ├── Sensorio/
│   │   ├── Observacion.cs               # DTO del estado observado (lo rellena el adaptador)
│   │   ├── Saliencia.cs                 # rasgos × estado × intención × novedad × relación × distancia → x/(1+x); sin visión ≤ periferia
│   │   ├── Novedad.cs                   # Habituación por entidad en minutos de juego, con recuperación y piso
│   │   ├── Bandas.cs                    # Foco / mención / periferia / ausente
│   │   └── Prosa/
│   │       ├── PlantillasProsa.cs       # Plantillas por categoría y banda (deterministas)
│   │       ├── GeneradorEscena.cs       # Escena en segunda persona + apéndice de hechos
│   │       └── GeneradorAccion.cs       # Prosa de acción para el búfer
│   ├── Atencion/
│   │   ├── Integrador.cs                # Acumulador con fuga en minutos de juego (integración exacta)
│   │   ├── Disparadores.cs              # Pico, interlocución, deriva, hito, fin, compromiso, transición → Disparo
│   │   ├── Refractario.cs               # Ventana y elevación de umbrales en tiempo real
│   │   ├── PresupuestoPulsos.cs         # Por personaje, modo de reloj y día de juego; reserva para las cadenas
│   │   ├── Bufer.cs                     # Búfer de prosa y consolidación fóvea/periferia temporal
│   │   └── Huella.cs                    # Atención acumulada por entidad desde el último pensamiento
│   ├── Intencion/
│   │   ├── Intencion.cs                 # Ciclo de vida y pesos de atención
│   │   ├── ValidadorIntencion.cs        # Precondiciones contra la observación vigente
│   │   ├── Acciones.cs                  # Vocabulario v1 (ir_a, sentarse, mirar, dibujar…)
│   │   └── HitosRutina.cs               # Horario del día → hitos, con la anotación del perfil que coincide (cambios: R-16, CP-306)
│   ├── Dialogo/
│   │   ├── Actos.cs                     # promete, regala, cita, enseña, rechaza, acepta_regalo
│   │   ├── ValidadorActos.cs            # Todos válidos o ninguno
│   │   └── OpcionesJugador.cs           # Opciones sugeridas a partir de affordances
│   ├── Memoria/
│   │   ├── EstadoPersonaje.cs           # Diario, recuerdos, relaciones, portafolio, compromisos
│   │   ├── Consolidacion.cs             # Envejecimiento y poda con tope de tamaño
│   │   └── Compromisos.cs               # Plazos y curva de saliencia
│   ├── Contrato/                        # DTOs del API (espejo de schemas/)
│   ├── Tiempo/
│   │   └── Reloj.cs                     # IReloj (tiempo de juego y real), ModoReloj, aritmética de la hora
│   └── Config/
│       └── CorazonConfig.cs             # Config + Validate()
├── CorazonPelican/                      # Mod SMAPI (.NET 6)
│   ├── manifest.json
│   ├── ModEntry.cs                      # Registro de módulos (patrón IModule de Pelican)
│   ├── Adaptador/
│   │   ├── ObservadorJuego.cs           # Game1 → Observacion (solo lectura)
│   │   ├── CuerpoNpc.cs                 # Ejecuta acciones sobre el NPC (hilo principal)
│   │   └── ReclamacionNpc.cs            # Toma y devolución del control del NPC
│   ├── Modules/
│   │   ├── HabitanteModule.cs           # Bucle por tick: sensorio, atención, cuerpo
│   │   ├── DialogoModule.cs             # Intercepción de la conversación y UI nativa
│   │   ├── ObrasModule.cs               # Registro de muebles y texturas de obras
│   │   ├── PersistenciaModule.cs        # WriteSaveData / ReadSaveData
│   │   ├── MultijugadorModule.cs        # Sincronización de obras a clientes
│   │   ├── ApiModule.cs                 # Servidor HTTP loopback + cola al hilo principal
│   │   ├── IntegracionPelicanModule.cs  # Opcional: sucesos y reclamación compartida
│   │   └── ConsolaModule.cs             # Comandos cp_*
│   └── assets/personajes/leah.json      # Perfil por defecto (editable con Content Patcher)
├── CorazonPelican.Tests/                # xUnit (.NET 8) sobre Core + FakeWorld
├── schemas/                             # JSON Schema: fuente única del contrato (C# y Python)
│   ├── comun.schema.json                # Definiciones compartidas ($defs): época, tiempo, ref, paso de plan…
│   ├── pulso.schema.json
│   ├── respuesta.schema.json
│   ├── obra.schema.json
│   ├── perfil.schema.json
│   ├── resultado.schema.json            # Resultado de POST respuestas (motivos de rechazo)
│   ├── salud.schema.json                # GET salud
│   └── ejemplos/                        # validos/ e invalidos/ (<esquema>--<caso>.json) + obligatorios.json
├── mente/                               # Python: proceso de la mente
│   ├── cliente_motor.py                 # Long-poll de pulsos, envío de respuestas
│   ├── ensamblador.py                   # Prompt: voz + escena + búfer + memoria + compromisos
│   ├── recuperacion.py                  # Selección de memoria: recencia × importancia × relevancia
│   ├── reflexion.py                     # Diario → recuerdos
│   ├── modelos.py                       # Enrutado de modelos y presupuesto de tokens
│   └── tests/
└── taller/                              # Python: render de obras
    ├── renderizador.py                  # Interfaz Renderizador (SVG por defecto, enchufable)
    ├── paleta.py                        # Paleta fija extraída de texturas vanilla
    └── tests/
```

`schemas/` es la **única fuente del contrato** (JSON Schema 2020-12, `$id` bajo
`https://dr4co.local/corazon-pelican/schemas/`, referencias cruzadas a `comun.schema.json`).
En Python valida `mente/contrato.py` (`jsonschema` + `referencing`); en C#, los tests con
JsonSchema.Net (solo en `CorazonPelican.Tests`: Core no depende de él). Los DTOs de
`Core/Contrato` (System.Text.Json) se prueban con ida y vuelta sobre los ejemplos válidos, y
`schemas/ejemplos/obligatorios.json` fija los campos obligatorios: cambiarlos rompe un test en
cada lenguaje. Los ejemplos de esta sección están en `schemas/ejemplos/validos/`.

---

## 3. Modelo de Datos

### 3.1. Perfil de personaje

Se carga como asset personalizado `Mods/CorazonPelican/Personajes` (diccionario `id → perfil`),
de modo que un pack de Content Patcher pueda editar o añadir personajes sin código.

```json
{
  "id": "leah",
  "npc": "Leah",
  "rasgos": { "luz": 2.0, "agua": 1.8, "madera": 1.5, "color": 1.6, "persona": 0.8, "maquinaria": 0.3 },
  "umbrales": { "foco": 0.70, "mencion": 0.40, "periferia": 0.15, "pico": 0.85, "deriva": 3.0, "deriva_semivida_min": 10, "histeresis": 0.02 },
  "refractario": { "subida": 0.5, "semivida_s": 30 },
  "presupuesto": {
    "vanilla": { "pulsos_por_dia_juego": 24, "reservados_cadenas": 2 },
    "pelican": { "pulsos_por_dia_juego": 24, "reservados_cadenas": 2 }
  },
  "lexico": {
    "lugares": [
      { "id": "pasarela_lago", "ubicacion": "Mountain", "area": [0, 0, 0, 0], "prosa": "la pasarela de madera del lago", "sentable": true }
    ]
  },
  "plantillas": "assets/personajes/leah.plantillas.json",
  "apertura": [ { "desde_corazones": 0, "ensenar": false, "encargos": false, "regalar": false } ],
  "voz": "assets/personajes/leah.voz.md",
  "acciones": ["ir_a", "sentarse", "mirar", "levantar_la_vista", "dibujar", "guardar_cuaderno", "gesto", "murmurar", "esperar", "volver_a_rutina"],
  "formatos_obra": ["boceto", "cuadro_pequeno", "cuadro_mediano"],
  "rutina": {
    "por_defecto": { "contexto": [], "omitir_si_ya_penso": true, "cambios": [] },
    "anotaciones": [
      { "id": "despertar", "forma": "cadena" },
      { "id": "acostarse", "forma": "cadena" },
      { "id": "dibujar_fuera", "si": { "animacion": "…dibujo…" },
        "contexto": ["luz", "obra_en_curso"], "cambios": ["saltar", "sustituir"] },
      { "id": "saloon", "si": { "ubicacion": "Saloon" },
        "contexto": ["personas_cerca"], "cambios": ["saltar"], "limites": { "max_por_semana": 1, "nunca": ["festival"] } }
    ]
  }
}
```

Los hitos de rutina **no se listan**: cada día, al cargarse el horario que el juego eligió para
el NPC (`NPC.Schedule` y `dayScheduleName`; la clave sale de estación, día, clima, festival,
corazones, matrimonio y avance de la partida), el motor convierte cada entrada en un hito con
`id = <clave del horario>:<hora>` (GSS §4.1, §5.3). `rutina.anotaciones` los enriquece por
patrón: `si` compara con la ubicación de destino, la animación final o la franja horaria de la
entrada **[verificar campos de `SchedulePathDescription` en CP-011]**; la primera que coincide
aplica, y si ninguna coincide se usa `por_defecto`. `contexto` lista las fuentes de hechos que
acompañan al pulso (solo las que el motor sabe reunir); `forma` es `pensamiento` (por defecto) o
`cadena`; `cambios` y `limites` dicen qué puede cambiar la mente de esa entrada. Sin `cambios`,
la entrada no se puede alterar. `despertar` y `acostarse` son hitos del motor que no salen del
horario. Para un
personaje con tienda, `contexto: ["ventas_dia"]` exigiría una fuente nueva en el sensorio.

La cadencia de pensamiento no se programa: emerge de los disparadores (GSS §4.1) y se mide en
la fase 2 en ambos modos. Los valores de `presupuesto` son provisionales hasta esa medición.

`area` es `[x, y, ancho, alto]` en casillas de su `ubicacion`. `[0,0,0,0]` es el marcador de
«pendiente de medir en vivo» **[verificar: CP-006]**: `PerfilValidator` lo reporta como aviso en
modo de desarrollo y como error en modo normal (el personaje no se gobierna). Los lugares que
salen del horario de Leah usan su casilla del horario como área `1×1` de partida.

`PerfilPersonaje` (Core) usa `[DataContract]`/`[DataMember]` con los nombres del esquema: así lo
leen SMAPI y Content Patcher (Newtonsoft.Json) sin que Core dependa de ninguna biblioteca. Cada
fichero `assets/personajes/<id>.json` es un perfil (los que llevan otro punto, como
`leah.plantillas.json`, no). Las fuentes de `contexto` que el motor sabe reunir están en
`Core/Sensorio/FuentesContexto.cs` (`luz`, `clima`, `energia`, `obra_en_curso`, `personas_cerca`,
`compromisos`); una nueva exige ampliar el sensorio.

### 3.2. Estado persistido por personaje (`EstadoPersonaje`)

| Campo | Contenido |
|---|---|
| `version_esquema` | Entero; migraciones como en `ServerWorldState` de Pelican |
| `diario[]` | `{ id, dia_juego, hora, resumen_prosa, saliencia, entidades[] }` |
| `recuerdos[]` | `{ id, texto, importancia, origen_diario[] }` |
| `relaciones{}` | `farmerId|npc → { impresion, ultima_vez }` |
| `portafolio[]` | `{ obra_id, estado, decision, nota, escena_origen, png_base64?, destinatario? }` |
| `compromisos[]` | `{ id, tipo, con, objeto, plazo_juego, estado }` |
| `contadores` | Pulsos del día, día de juego del contador |
| `intencion_activa?` | Para reanudar tras cargar |

### 3.3. Pulso (motor → mente)

```json
{
  "id": 1042,
  "epoca": "01JC8Q2M5V7K3T9W4X6Y8Z0A1B",
  "personaje": "leah",
  "motivo": "interlocucion",
  "anticipado": false,
  "tiempo_juego": { "dia": 68, "hora": 1740 },
  "tiempo_real_utc": "2026-11-03T22:40:12Z",
  "escena": "Es última hora de la tarde de otoño…",
  "hechos": { "energia": "media", "cerca": [ { "ref": "farmer:123", "nombre": "Ana", "corazones": 6, "distancia": 9 } ] },
  "acciones_disponibles": ["dibujar", "levantar_la_vista", "gesto(saludo)", "guardar_cuaderno"],
  "lugares": [
    { "id": "pasarela_lago", "prosa": "la pasarela de madera del lago" },
    { "id": "orilla_norte", "prosa": "la orilla norte" },
    { "id": "tu_casa", "prosa": "tu casa" }
  ],
  "bufer": [ { "hora": 1700, "prosa": "trazas la línea del muelle", "saliencia": 0.52 } ],
  "huella": {
    "desde_ultimo_pensamiento_min_juego": 95,
    "prosa": "Durante el camino te has fijado sobre todo en el agua gris y en la luz que se va.",
    "entidades": [ { "ref": "agua:Mountain:lago", "atencion": 0.81 }, { "ref": "luz", "atencion": 0.64 } ]
  },
  "intencion_activa": { "id": "int-77", "objetivo": "acabar el boceto del sauce" },
  "agenda": [ { "hito": "fall:1900", "prosa": "volver a casa", "estado": "pendiente" }, { "hito": "fall:2200", "prosa": "recoger el taller", "estado": "saltado" } ],
  "compromisos_proximos": [],
  "interlocutor": { "ref": "farmer:123", "texto": "¿Me dibujarías el lago?" },
  "caduca_en_ms": 8000
}
```

`motivo` ∈ `pico`, `interlocucion`, `deriva`, `hito`, `fin_intencion`, `compromiso`, `transicion`
(GSS §4.1). Un pulso de `hito` lleva `hito: { id, origen: rutina|intencion, forma }` y los hechos de
su `contexto`; los hitos `despertar` y `acostarse` llevan además el diario del día (acostarse) o
los recuerdos y compromisos pendientes (despertar). `epoca` identifica la carga de mundo vigente (§5).

`bufer` guarda lo que llegó al menos a la banda de mención del personaje; `huella` resume la
atención acumulada desde el último pensamiento (GSS §4.4) y se reinicia al emitir cada pulso. Ambos
salen de la saliencia y las plantillas del perfil y solo nombran entidades observadas.

`hechos`, `acciones_disponibles` y `lugares` forman el apéndice seco de la escena (GSS §3.4).
Toda referencia (`ref`) es una identidad estable del juego. El apéndice da **pares id — prosa**
(lugares, entidades, hitos de la agenda, obras del portafolio): la escena usa la prosa y la
respuesta, el id, sin que la mente tenga que adivinar la correspondencia.

### 3.4. Respuesta (mente → motor)

```json
{
  "pensamiento": "Ana otra vez. Quiere el lago… y yo llevo toda la tarde mirándolo. Pero hoy la luz ya se ha ido.",
  "epoca": "01JC8Q2M5V7K3T9W4X6Y8Z0A1B",
  "basada_en": 1042,
  "intencion": {
    "objetivo": "dibujar el lago para Ana mañana al atardecer",
    "porque": "me lo ha pedido y la luz de otoño en el agua es lo que llevo semanas mirando",
    "plazo_juego": { "dia": 69, "hora": 1900 },
    "pesos_atencion": { "luz": 1.5, "agua": 1.5, "persona": 0.6 },
    "plan": [ { "accion": "guardar_cuaderno" }, { "accion": "volver_a_rutina" } ]
  },
  "frase": "¿El lago? …Mañana, cuando baje el sol. Hoy la luz ya no da para nada.",
  "actos": [ { "acto": "promete", "obra": "nueva", "tema": "lago al atardecer", "para": "farmer:123", "cuando": { "dia": 69, "hora": 1900 } } ],
  "memoria": { "recordar": [ "Ana me pidió un dibujo del lago. Le dije que sí, casi sin pensarlo." ] }
}
```

**Pensar en prosa, decidir en léxico (P-11).** La respuesta tiene dos tipos de campos:

* **Prosa del personaje**, que el arnés nunca ejecuta: `pensamiento`, `objetivo`, `porque`,
  `frase`, `memoria.recordar`, `obra.decision`. `pensamiento` va **el primero** del esquema para
  que el modelo piense como el personaje antes de comprometerse; el motor lo guarda en la
  telemetría y como entrada del diario.
* **Campos efectivos**, cerrados: `plan`, `actos`, `cambios_rutina`, `agenda`, `plazo_juego`,
  `pesos_atencion`. Solo admiten acciones del vocabulario e ids que el pulso ofrece.

`epoca` y `basada_en` los copia la mente del pulso; el modelo no los genera. Los campos
opcionales se omiten en vez de enviarse a `null`. Cada paso de `plan` es
`{ "accion", "lugar"?, "objetivo"?, "obra"?, "tipo"?, "texto"?, "duracion_min"? }`, con el argumento
que exige cada acción (`ir_a`/`sentarse` → `lugar`, `mirar` → `objetivo`, `dibujar` → `obra`,
`gesto` → `tipo`, `murmurar` → `texto`, `esperar` → `duracion_min`). En JSON los identificadores
son ASCII: el acto `enseña` de la GSS se escribe `ensena`.

El esquema **no admite coordenadas** en `plan` (regla R-13): solo acciones del vocabulario con
argumentos del léxico. Si lo que el personaje quiere no tiene id («la orilla con esta niebla»),
elige entre lo disponible o la intención se rechaza: nunca se inventa un lugar.

La intención puede llevar `cambios_rutina: [ { "hito": "fall_Fri:1900", "cambio": "saltar" } ]`
(`saltar`, `mover` con nueva hora, `sustituir` con un plan). Se validan contra `rutina` del
perfil (R-16) y su efecto dura, como mucho, hasta el final del día. La respuesta a la cadena de
`despertar` fija la **agenda** del día (entradas de hoy con su estado y los planes de intención
intercalados); cada pulso de hito la lleva y su respuesta puede reequilibrar lo que queda con
nuevos `cambios_rutina` (GSS §4.1, pensamiento en cascada). El pulso lista los
hitos que quedan hoy, con sus `id`, para que la mente solo pueda referirse a entradas reales.

### 3.5. Obra

```json
{
  "obra_id": "leah-0031",
  "formato": "cuadro_mediano",
  "decision": { "titulo": "Lago, otoño", "tema": "…", "composicion": "…", "paleta": "grises fríos, un ocre", "nota": "…" },
  "estado": "en_curso",
  "intentos": 0
}
```

---

## 4. Bucle del Motor (hilo principal)

Por cada personaje gobernado, en `UpdateTicked` (solo host):

| Paso | Cadencia | Coste objetivo | Componente |
|---|---|---|---|
| 1. Drenar cola de la API (respuestas, obras) | Cada tick | < 0,2 ms | `ApiModule` |
| 2. Avanzar el cuerpo (acción en curso) | Cada tick | < 0,1 ms | `CuerpoNpc` |
| 3. Observar | Cada `SensorioCadenciaTicks` (15 ≈ 250 ms) | < 1 ms | `ObservadorJuego` |
| 4. Saliencia, bandas, novedad | Idem | < 0,5 ms | `Core/Sensorio` |
| 5. Prosa de acción → búfer | Idem, solo si hay cambio relevante | < 0,2 ms | `GeneradorAccion` |
| 6. Reflejos | Idem | < 0,1 ms | Reglas del perfil |
| 7. Integrador, huella de atención y disparadores | Idem | < 0,1 ms | `Core/Atencion` |
| 8. Emitir pulso (escena + búfer consolidado) | Al dispararse | < 2 ms | `GeneradorEscena`, `Bufer` |
| 9. Compromisos: actualizar saliencia por plazo | Cada cambio de 10 min de juego | despreciable | `Compromisos` |

La observación se limita a la **ubicación actual** del personaje (más sucesos globales). Los costes
son objetivos de diseño; se miden en la fase 1 con un contador por paso.

### 4.1. Validación de respuestas

Al drenar una respuesta:

1. Comprobar `epoca`: una respuesta de otra época se rechaza con motivo `epoca_obsoleta`.
   Comprobar `basada_en`: si existe un pulso posterior de prioridad igual o mayor ya emitido,
   la respuesta se descarta como obsoleta (salvo interlocución, que nunca se descarta en silencio).
2. Validar la intención contra la **observación vigente** (lugares alcanzables, acciones del
   vocabulario, plazos futuros) y sus `cambios_rutina` contra el perfil (R-16).
3. Validar **todos** los actos (R-07). Si alguno falla, se devuelve a la mente como rechazo con
   motivo; la mente puede reformular una vez.
   Cada motivo de rechazo va en dos formas: `{ "codigo": "lugar_ocupado", "campo": "plan[0]",
   "prosa": "no puedes sentarte en la pasarela: hay alguien" }`. El código es para el arnés y la
   telemetría; la prosa, para que la mente reformule en sus propios términos.
4. Aplicar en orden: intención → actos → memoria → frase visible.

---

## 5. Contrato Motor ↔ Mente (API v1)

Base: `http://127.0.0.1:25560/api/v1/`. Cabecera `Authorization: Bearer <token>` si hay token.

| Método y ruta | Uso |
|---|---|
| `GET salud` | Versión, personajes activos, si el mundo está cargado, `epoca` vigente |
| `GET pulsos?epoca=E&since=N&espera_ms=25000` | **Long-poll** de pulsos con `id > N` de la época `E`, orden creciente, solo añadir. Si `E` no es la vigente, responde `409` con la época actual |
| `POST respuestas` | Envía una respuesta (§3.4). Devuelve `aceptada` / `rechazada{motivos}` (cada motivo con `codigo`, `campo` y `prosa`, §4.1) |
| `GET memoria/{personaje}` | Instantánea de memoria para la caché de trabajo de la mente |
| `GET eventos?since=N` | Registro de solo añadir del motor: intenciones activadas, rechazadas, cumplidas; actos aplicados; obras creadas |
| `POST obras/{obra_id}/sprite` | PNG en base64 + metadatos de render. Solo válido para obras `en_curso` |
| `GET escena/{personaje}` | Depuración: escena que se emitiría ahora (sin consumir presupuesto) |

**Propiedades del contrato:**

* **Época (P-1, 2026-11-04):** la memoria se guarda solo con la partida (§9), así que al
  recargar, los `id` de pulso y de obra vuelven atrás y pueden repetirse. Cada carga de mundo
  (`SaveLoaded`) genera una `epoca` nueva: un identificador opaco y único (p. ej. ULID), que no
  se guarda en la partida y no se repite entre procesos. Toda clave de idempotencia y todo
  cursor van calificados por la época.
* **Idempotencia:** `POST respuestas` con un (`epoca`, `basada_en`) ya resuelto devuelve el
  resultado anterior sin reaplicar. `POST obras/{id}/sprite` es idempotente por
  (`epoca`, `obra_id`, hash).
* **Reanudación:** la mente persiste solo su cursor (`epoca`, `since`) y su caché; tras
  reiniciar, reconstruye desde `GET memoria` y continúa. Si la época vigente es otra, descarta
  cursor y caché y empieza desde `since=0`.
* **Prioridad:** los pulsos de interlocución salen con `caduca_en_ms`. Pasado ese tiempo, el
  motor aplica el fallback vanilla y marca el pulso como caducado.
* **Versionado:** ruta `/api/v1/`; cambios incompatibles → `/v2/`.

---

## 6. La Mente (`mente/`)

Bucle principal:

```
loop:
  pulsos = motor.pulsos(since=cursor, espera_ms=25000)
  para cada pulso (interlocución primero):
      contexto = ensamblar(perfil.voz, pulso.escena, pulso.bufer,
                           recuperar(memoria, pulso), pulso.compromisos_proximos)
      esquema = estrechar(respuesta.schema.json, pulso)
      salida  = llm(contexto, esquema)
      validar localmente contra el esquema (1 reintento si falla)
      r = motor.responder(salida)
      si r.rechazada y quedan reintentos: reformular con r.motivos
      si salida.obra: encolar trabajo en el taller
  cursor = último id
```

* **Esquema por pulso (P-11):** `estrechar` convierte el esquema general en uno propio del pulso,
  con listas cerradas sacadas del propio pulso: acciones disponibles, ids de lugares, de
  entidades en escena, de hitos de la agenda y de obras del portafolio. Si el proveedor hace
  cumplir el esquema, el modelo no puede escribir un id que no exista. `schemas/` sigue siendo el
  contrato: el esquema del pulso es siempre un subconjunto suyo. La validación del motor (§4.1) se
  mantiene, porque el mundo puede haber cambiado mientras pensaba.
* **Recuperación de memoria:** puntuación por recencia × importancia × relevancia respecto a la
  escena (inspirada en *Generative Agents*, Park et al., 2023).
* **Cadenas (GSS §4.1):** un pulso de hito con `forma: cadena` (siempre `despertar` y
  `acostarse`; otros si el perfil lo dice) se resuelve en varias llamadas encadenadas, cada
  una con la salida de la anterior, y devuelve **una** respuesta. La de acostarse incluye la
  **reflexión**: condensa el diario del día en recuerdos y los envía como `memoria.recordar` (el
  motor poda el diario según su tope), junto con impresiones de relaciones y compromisos para
  mañana.
* **Orden y guardado:** el motor emite `acostarse` cuando Leah se va a dormir o en
  `DayEnding`, lo que llegue antes. En vanilla el guardado suele llegar antes que la cadena; su
  respuesta se aplica igual al llegar (misma época) y se persiste en el siguiente guardado. La
  mente procesa las cadenas en orden: el `despertar` espera a que termine el `acostarse` anterior.
* **Modelos:** un modelo grande para pensamiento y diálogo; opcionalmente uno pequeño local para
  reflejos complejos con etiquetas cerradas. El prompt de sistema (voz del personaje + reglas)
  es estable para aprovechar caché de prompt del proveedor.
* **Presupuesto de tokens por día real:** segunda barrera de coste, subordinada al presupuesto de
  pulsos del motor (que es el primario).
* **Pensamiento anticipado:** la mente procesa los pulsos `anticipado: true` con la misma
  prioridad que el resto, salvo interlocución. Si el motor los cancela, aparecen en `eventos` y
  la mente descarta su resultado.

---

## 7. El Taller (`taller/`)

```
decisión de obra ─▶ prompt de imagen ─▶ Generador ─────────▶ imagen bruta ─▶ script de asset ─▶ PNG del formato
   (prosa, mente)      (mente)           · Space de Hugging Face              recorte, reducción,   + metadatos
                                         · modelo de imagen capaz             cuantización a paleta,
                                         · SVG del LLM (respaldo local)       limpieza, marco
                                                                                    │
                              autocrítica (mente, imagen) ◀─────────────────────────┘ ─▶ acepta ─▶ POST obras/{id}/sprite
                                                                                         repite (máx. 2)
```

* **De la prosa al asset (P-3, 2026-11-04):** la decisión de obra es prosa; el taller la lleva a
  un asset de verdad en dos piezas separadas:
  1. **Generador enchufable** que produce una imagen bruta a partir del prompt: un Space de
     Hugging Face adecuado (p. ej. un modelo con LoRA de pixel art), un modelo de imagen capaz
     por API, o, como respaldo sin red, el SVG restringido escrito por el LLM. El ganador y el
     orden de respaldo se deciden en el spike del taller (CP-013).
  2. **Script de asset** (`python -m taller.asset`), determinista y ejecutable a mano: recorta al
     encuadre del formato, reduce al lienzo, cuantiza a la paleta fija, limpia píxeles sueltos y
     emite la PNG con sus metadatos y una vista previa a ×4. Es lo que hace que cualquier
     generador produzca una obra que no desentona.
* **Generadores externos:** se fija la revisión del Space o del modelo para que sea
  reproducible, se registra la licencia del modelo (algunas LoRA son de uso no comercial) y una
  caída, cola o límite del servicio deja la obra como boceto, nunca bloquea a Leah. El prompt de
  imagen no lleva nombres de jugadores.
* **Paleta:** fichero fijo extraído una vez de texturas vanilla; versionado con el taller.
* **Formatos:** `boceto` (no colocable, solo portafolio), `cuadro_pequeno` y `cuadro_mediano`
  con los tamaños de los cuadros vanilla equivalentes **[verificar en `Data/Furniture`, campo 2]**.
  La textura de cada obra va en el campo 9 de su entrada (V-03, resuelta).
* **Tiempo:** el trabajo corre mientras el cuerpo ejecuta `dibujar`. Si no llega antes de que la
  intención caduque, la obra queda como boceto y Leah puede retomarla.

---

## 8. Diálogo (`DialogoModule`)

### 8.1. Intercepción

* Enganche en la acción del jugador sobre el NPC gobernado: prefijo Harmony sobre
  `NPC.checkAction`, que es `virtual`. Dentro, el juego gestiona regalos, misiones de socializar,
  cónyuge y la amistad diaria (`grantConversationFriendship`): la intercepción solo cambia qué se
  dice y deja el resto al juego (propuesta P-7, pendiente; [notas](docs/notas-juego.md) §2)
  **[verificar en vivo: CP-010]**.
* **Regalos:** si el jugador sostiene un objeto regalable, se deja actuar al juego (amistad,
  reacción nativa) y el regalo entra al sensorio como suceso. Corazón no reimplementa regalos.
* **Condiciones para interceptar:** mente disponible, presupuesto de interlocución, ValleyTalk
  no activo para ese personaje (R-12), ningún evento con guion en curso.

### 8.2. Secuencia

```
jugador habla ──▶ reflejo: emote + DialogueBox nativo con línea breve del perfil (< 300 ms)
              └─▶ pulso de interlocución (o se reutiliza el anticipado)
                     │
mente responde ──▶ validación de actos ──▶ DialogueBox con la frase
                                        └─▶ pregunta nativa con opciones (§8.3)
si caduca ──────▶ emote «…» y diálogo vanilla del día
si el jugador se aleja antes de la respuesta ──▶ la frase se guarda como «lo que iba a decir»
```

### 8.3. Opciones del jugador

* `OpcionesJugador` (Core) genera 3 o 4 opciones a partir de affordances: inventario regalable,
  encargos según apertura, lugares del léxico para citas, más «Decir otra cosa…».
* Se presentan como **pregunta nativa con respuestas** (`DialogueBox` con `responses`), lo que la
  hace operable por humanos y por el puente de Voyager (`responseCC`, `myID`), sin API especial.
* «Decir otra cosa…» abre un **menú nativo de entrada de texto**. `TextEntryMenu` no sirve (es
  el teclado en pantalla para mando); candidatos: `NamingMenu` o un menú propio con `TextBox`
  **[verificar: CP-014]**.

---

## 9. Persistencia (`PersistenciaModule`)

* **Dónde:** `Helper.Data.WriteSaveData("CorazonPelican.Estado", estado)` en el evento `Saving`
  del host; lectura en `SaveLoaded`. Los datos viajan dentro de la partida (GSS §9.4)
  Solo el jugador principal puede usar `ReadSaveData`/`WriteSaveData` (SMAPI lanza
  `InvalidOperationException` en granjeros), coherente con la autoridad del host **[verificar en
  vivo: CP-015]**.
* **Obras:** PNG en base64 dentro del portafolio. Un cuadro de 32×32 px ocupa unos pocos KB; el
  tope total (`TamanoMaxMemoriaKB`) protege el tamaño de la partida.
* **Al cargar:** reconstruir texturas desde el portafolio, registrarlas como assets
  `Mods/CorazonPelican/Obras/{obra_id}` (`AssetRequested` + `LoadFrom`), inyectar las entradas en
  `Data/Furniture` con su textura (campo 9; el índice de sprite es el campo 8) e invalidar la
  caché.
* **Recarga y retroceso:** como la memoria se escribe solo al guardar, recargar una partida
  devuelve memoria y mundo al mismo punto. La telemetría (§12) no es memoria y no se restaura.
* **Migraciones:** `version_esquema` con migradores puros en Core y tests por versión.
* **Desinstalación:** comando `cp_preparar_desinstalacion` que exporta las obras a PNG en una
  carpeta y sustituye las colocadas por un cuadro vanilla, evitando objetos de error.

---

## 10. Multijugador (`MultijugadorModule`)

* **Autoridad:** motor, mente, memoria y validación solo en el host (`Context.IsMainPlayer`).
* **Obras en clientes:** el host envía las texturas por `Helper.Multiplayer.SendMessage` al
  conectar un par (`PeerConnected`) y al crear cada obra; el cliente las registra localmente.
* **Clientes sin el mod:** verían objetos de error en las obras colocadas. El host avisa por
  consola al detectar un par sin Corazón Pelican.
* **Interfaz de diálogo en clientes:** la conversación se inicia en el cliente y se resuelve en el
  host por mensajes del mod **[verificar flujo de `checkAction` en clientes]**.

---

## 11. Integraciones Opcionales

### 11.1. Pelican 4Ever

* **Sucesos:** si se configura, el motor lee `GET /api/v1/host/events?since=N` de Pelican desde un
  hilo de fondo y convierte sucesos relevantes (`player.connected`, festivales) en entidades del
  sensorio. Preferible a futuro: una API de mod (`Helper.ModRegistry.GetApi`) expuesta por
  Pelican, sin HTTP dentro del mismo proceso.
* **Reclamación del NPC:** mientras un personaje tiene intención activa, el motor marca
  `npc.modData["CorazonPelican.Control"]`. El *micro-wandering* de Pelican (`WorldLifeModule`)
  debe ignorar NPCs con esa clave. **Requiere un cambio pequeño en Pelican** (tarea a abrir allí).
* **Reloj:** el motor no toca el reloj. Lee `Game1.timeOfDay` y deriva sus tiempos (GSS §7).

### 11.2. Stardew Voyager

* Sin integración de código. Puertos distintos. El granjero de Voyager interactúa con Leah por el
  juego y su puente ya sabe leer y operar `DialogueBox` (`docs/dialoguebox-notes.md` de Voyager).
* La mente de Leah y Dr4co pueden compartir infraestructura de modelos, **nunca contexto**.

### 11.3. ValleyTalk

* Si `Helper.ModRegistry.IsLoaded(<UniqueID de ValleyTalk>)` **[verificar UniqueID: mod 30319 de
  Nexus, autor dandm1]** y el
  personaje no figura en `ValleyTalkDesactivadoPara`, el motor no intercepta ese personaje y
  avisa. La configuración de ValleyTalk no se lee ni se modifica.

---

## 12. Configuración y Observabilidad

### 12.1. `config.json`

| Clave | Por defecto | Uso |
|---|---|---|
| `Personajes` | `["leah"]` | Personajes gobernados |
| `MenteHabilitada` | `true` | Con `false`, solo sensorio y diario (fase 1) |
| `ModoDesarrollo` | `false` | Con `true`, lo pendiente de medir en vivo (áreas del léxico) es aviso y no error |
| `ApiPuerto` / `ApiToken` | `25560` / vacío | API motor ↔ mente |
| `SensorioCadenciaTicks` | `15` | Frecuencia de observación |
| `ModoReloj` | `auto` | `auto` / `vanilla` / `pelican`: elige el `presupuesto` del perfil y etiqueta la telemetría para medir la cadencia. `auto` detecta Pelican 4Ever |
| `TiempoMaxRespuestaMs` | `8000` | Caducidad de interlocución |
| `AnticipacionHabilitada` | `true` | Pensamiento anticipado |
| `TamanoMaxMemoriaKB` | `512` | Tope por personaje |
| `ValleyTalkDesactivadoPara` | `[]` | Confirmación explícita del usuario |
| `PelicanApiUrl` / `PelicanApiToken` | vacío | Sucesos de Pelican |
| `Telemetria` | `true` | Registros jsonl |

`CorazonConfig.Validate()` rechaza configuraciones incoherentes al arrancar, como en Pelican.

### 12.2. Telemetría

`Mods/CorazonPelican/telemetria/{saveId}/`: `pulsos.jsonl`, `respuestas.jsonl`, `rechazos.jsonl`,
`actos.jsonl`, `escenas.jsonl`, `costes.jsonl`. Es registro, no memoria: ningún componente del
personaje la lee.

### 12.3. Consola

| Comando | Efecto |
|---|---|
| `cp_estado` | Personajes, época, intención activa, presupuesto, salud de la mente |
| `cp_escena leah` | Imprime la escena actual sin emitir pulso |
| `cp_pulso leah` | Fuerza un pulso (consume presupuesto) |
| `cp_mente on/off` | Activa o desactiva la intercepción en caliente |
| `cp_preparar_desinstalacion` | Exporta obras y las sustituye por cuadros vanilla |

---

## 13. Estrategia de Pruebas

| Nivel | Qué cubre | Herramienta |
|---|---|---|
| **Core** | Saliencia, bandas, integrador, refractario, presupuesto, validadores, consolidación, migraciones | xUnit sobre `CorazonPelican.Core` |
| **Escenas doradas** | Prosa determinista para observaciones fijas (snapshot) | xUnit + ficheros `.escena.txt` |
| **FakeWorld** | Bucle completo sin juego: observaciones sintéticas, cuerpo simulado, mente falsa lenta/rápida/caída | xUnit |
| **Contrato** | DTOs C# y modelos Python contra `schemas/` | xUnit + pytest |
| **Mente** | Ensamblado, recuperación, reintentos, presupuesto con `FakeLLM` | pytest |
| **Taller** | Cuantización, tamaños, idempotencia | pytest |
| **Vivo** | Fases 3–6 en partida de desarrollo separada (como Voyager) | Manual con recibos |

Cada regla R-xx de la GSS §13 tiene su test en esta matriz antes de considerarse vigente.

---

## 14. Plan de Construcción

| Fase | Entrega | Aceptación |
|---|---|---|
| **1. Sensorio** | Observación, saliencia, bandas, prosa y diario; `MenteHabilitada=false` | Un día completo de escenas legibles; R-01, R-02, R-03, R-14; costes por paso medidos |
| **2. Pulsión y mente sin cuerpo** | Atención, pulsos, cadenas de despertar y acostarse, API, mente que solo escribe pensamientos | Cadencia medida en vanilla y Pelican; pulsos/día dentro de presupuesto; R-04, R-05 en FakeWorld |
| **3. Cuerpo** | Vocabulario v1, reclamación y devolución del NPC | Intenciones cumplidas y rechazadas en vivo; R-06, R-11, R-13 |
| **4. Taller y obras** | Render, registro, persistencia, sincronización | Obra colocada sobrevive a guardar/cargar y se ve en un cliente; R-10 |
| **5. Diálogo con actos** | Intercepción, opciones, texto libre, compromisos | Promesa hecha y cumplida al día siguiente; R-07, R-08, R-09, R-12 |
| **6. Encuentro** | Pelican + humanos + Voyager | Sesión en servidor de desarrollo con al menos un humano y el granjero agente |

---

## 15. Decisiones de arquitectura

* [ADR-0001 · El host gobierna el cuerpo y la mente trabaja fuera del juego](docs/adr/0001-motor-en-el-host.md).
* [ADR-0002 · La escena se escribe con plantillas deterministas](docs/adr/0002-percepcion-por-plantillas.md).
* [ADR-0003 · El motor valida los actos antes de mostrar la frase](docs/adr/0003-actos-antes-de-la-frase.md).
* [ADR-0004 · La memoria del personaje viaja con la partida](docs/adr/0004-memoria-con-la-partida.md).
* [ADR-0005 · Un solo contrato y una época por carga de mundo](docs/adr/0005-contrato-y-epoca.md).
* [ADR-0006 · Los sucesos y la rutina determinan cuándo pensar](docs/adr/0006-cadencia-por-disparadores.md).

---

## 16. Verificaciones

Detalle y fuentes en [`docs/notas-juego.md`](docs/notas-juego.md).

| # | Qué confirmar | Estado (2026-11-04) | Tarea |
|---|---|---|---|
| V-01 | Punto de enganche para interceptar la conversación | Localizado (`NPC.checkAction`, virtual); falta validar efectos laterales en vivo | CP-010 |
| V-02 | Suspender y reanudar el horario de un NPC sin romperlo | Mecanismo entendido (entradas a hora exacta, `lastAttemptedSchedule`); falta prototipo | CP-011 |
| V-03 | Campo de textura en `Data/Furniture` | **Resuelta**: campo 9 (sprite en el 8) | CP-015 |
| V-04 | Tamaños de los cuadros vanilla por formato | Pendiente | CP-013 |
| V-05 | `WriteSaveData` en multijugador | Casi resuelta: solo el jugador principal; confirmar en vivo | CP-015 |
| V-06 | Menú para texto libre | `TextEntryMenu` descartado; candidatos `NamingMenu` / `TextBox` | CP-014 |
| V-07 | Animaciones de dibujo de Leah | Casi resuelta: `leah_draw`, `leah_sculpt`, `leah_sleep`; falta el mapeo con acciones | CP-012 |
| V-08 | UniqueID de ValleyTalk | Pendiente: leer del `manifest.json` instalado | CP-505 |
| V-09 | Coordenadas de los lugares del léxico de Leah | Pendiente: medición en vivo (su horario da casillas de partida) | CP-006 |
