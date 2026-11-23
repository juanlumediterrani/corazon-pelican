---
id: CP-613
titulo: Iniciativa esporádica y afectiva del personaje para acercarse al jugador y entregarle el diálogo preparado
estado: hecha
fecha: 2026-11-23
prioridad: P2
hito: F6
depende_de: [CP-302, CP-303, CP-612]
tipo: code
complejidad: media
duracion_min: 150
sensibilidad: public
proyecto_destino: Corazón Pelican
---

## Objetivo

Permitir que un personaje gobernado (p. ej. Leah), al detectar a un jugador y tener listo su diálogo precalculado (`CP-612`), pueda **elegir por iniciativa propia** acercarse físicamente a decírselo, naciendo de forma natural de su **estado de ánimo, circunstancia presente y relación con ese jugador**, y ocurriendo de manera **esporádica y no muy recurrente**.

## Principio de diseño: convergencia afectiva (ni azar ciego ni seguimiento constante)

El acercamiento espontáneo surge en **dos capas** fieles a P-11 (el motor mide y acota; la mente siente y elige):

### 1. Puerta de disposición y frenos en el Motor (`Core` + perfil)

Cuando el personaje detecta a un jugador (`farmer:X`) sin diálogo consumido hoy (`CP-612`), un evaluador puro (`EvaluadorIniciativaSocial`) calcula un índice de disposición `[0..1]` a partir de tres dimensiones reales del estado:

1. **Estado de ánimo (`EstadoHumor` / `GestorHumor`, P-9):**
   - **Ánimo intenso o necesidad de desahogo/compañía:** un `HumorVigente` activo (intensidad por encima de umbral, p. ej. entusiasmo tras avanzar en su obra o peso por varios episodios seguidos en soledad según `Bufer`/`Diario`) eleva la disposición.
   - **Inhibición:** energía `baja` o estado de atención `Estrecha` sostenido sin ruptura reducen la disposición (prefiere concentrarse en lo suyo).
2. **Relación con el jugador (`corazones`, `EstadoPersonaje.Relaciones`, `Compromisos`):**
   - **Confianza (`AperturaPerfil`):** con desconocidos (`corazones` por debajo del umbral de apertura social del perfil, p. ej. `< 3`), la disposición base es `0` salvo motivo objetivo concreto (compromiso próximo/vencido o regalo recibido reciente aún vivo en `Bufer`).
   - **Reencuentro / ausencia (`Relaciones[ref].UltimaVez`):** cuanto más tiempo de juego hace que no hablan (p. ej. `>= 3` días sin conversación), más natural es el impulso de cruzar a saludar o comentar algo.
   - **Motivo vivo:** suma si hay un `CompromisoEstado` pendiente/próximo con ese jugador o un suceso reciente ligado a él (`regalo_recibido`, encuentro previo relevante).
3. **Circunstancia presente (escena y momento respirable):**
   - Requiere mismo mapa, distancia caminable razonable (p. ej. `4..16` casillas: ni tan pegado que ya esté al lado ni al otro extremo), jugador a pie y sin diálogo/evento activo, y que el personaje no esté ejecutando un desplazamiento urgente o hito de acostarse.

**Frenos duros anti-recurrencia (garantizan baja frecuencia):**
- Enfriamiento global configurable en el perfil del personaje (`iniciativa_social`: `enfriamiento_global_dias` ≥ `2–3` días de juego, `max_por_semana` bajo, p. ej. `1–2`).
- Enfriamiento específico por interlocutor (`enfriamiento_por_jugador_dias` ≥ `4–5` días).
- Bloqueado si ya consumió diálogo hoy con ese jugador o ya inició/abortó una iniciativa hoy.
- Solo si se superan todos los frenos **y** el índice de disposición alcanza el umbral de ocasión, el motor incluye `"acercarse_a(<ref>)"` en `acciones_disponibles` del pulso y aporta un hecho legible en `hechos` (el motivo emocional/circunstancial: días sin verse, humor vigente, obra/compromiso). Si no, la acción **ni siquiera existe** para la mente.

### 2. Elección propia en la Mente (`mente` + `ensamblador.py`)

- Que `"acercarse_a(farmer:X)"` esté disponible **habilita la posibilidad, no obliga**: la mente evalúa en `pensamiento` si al personaje de verdad le apetece cruzar hacia el jugador en este momento o prefiere seguir con su actividad y dejar que sea el jugador quien se acerque.
- Si elige tomar la iniciativa: emite `intencion.plan = [{ "accion": "acercarse_a", "objetivo": "farmer:X" }]` junto con un `dialogo` precalculado (`afirmacion` o `pregunta + opciones + vuelta`) coherente con ese impulso interior (comentar por qué cruzó a buscarte: su humor de hoy, lo que lleva días pensando, una obra o cita).

### 3. Ejecución natural y corte limpio (`CuerpoNpc` + `DialogoModule`)

- El cuerpo camina con `PathFindController` hacia una casilla libre adyacente al jugador dentro del mismo mapa (sin teletransporte ni bloqueo largo).
- **Respeto si el jugador se va:** si durante el trayecto el jugador sale del mapa, monta a caballo, abre otro menú/evento o se aleja demasiado (`> distancia_max`), el personaje detiene el acercamiento con naturalidad, suelta `CorazonPelican.Control` y vuelve a su rutina (`volver_a_rutina`), registrando el enfriamiento para no perseguirlo en bucle (pero conservando el `DialogoPreparado` en caché por si el jugador decide hablarle después).
- **Entrega al llegar:** al situarse en rango de conversación (`<= 2` casillas), abre de inmediato el `DialogoPreparado` (`Afirmacion` o `createQuestionDialogue` con opciones y *vuelta*, en host o enviando mensaje dirigido al *farmhand* remoto), guarda `ultimoMensaje` del día para evitar repetición y libera `CorazonPelican.Control`.

## Criterios de aceptación

- [x] `EvaluadorIniciativaSocial` en `CorazonPelican.Core`: pruebas unitarias verifican que la disponibilidad de `acercarse_a` responde a la combinación de `EstadoHumor`, relación (`corazones` + días desde `UltimaVez` + `Compromisos`/sucesos) y circunstancia, respetando siempre los enfriamientos globales y por jugador (nunca recurrente ni en el mismo día). (`IniciativaSocialCP613Tests.EvaluadorIniciativaSocial_RespondeAHumorRelacionYCircunstanciaYRespetaEnfriamientos_CP613`)
- [x] En `FakeWorld` / núcleo, la mente puede rechazar acercarse (siguiendo en su rutina) o aceptarlo con `acercarse_a(ref)` + `dialogo`; si el jugador huye o cambia de mapa durante el trayecto, el cuerpo cancela el acercamiento, entra en enfriamiento y libera `CorazonPelican.Control`. (`IniciativaSocialCP613Tests.AcercarseA_OfreceImpulsoEnPulsoEntregaAlLlegarYYaBloqueaSiJugadorSeAleja_CP613`)
- [x] Al llegar a rango con diálogo preparado válido, entrega al instante la afirmación o pregunta con opciones y registra la *vuelta* igual que `CP-612` (incluyendo soporte *farmhand* en multijugador). (`DialogoModule.EntregarIniciativaDesdeMotor` + `MsgDialogoEntregarIniciativa`)
- [x] `dotnet build` (`0` advertencias), `dotnet test` y `pytest` en verde.

## Fuera de alcance

- Perseguir al jugador entre distintos mapas o teletransportarse para alcanzarlo.
- Usar azar puro desvinculado del humor, circunstancia o relación como disparador principal.
- Activar la iniciativa con la mente caída/deshabilitada o bajo control de `ValleyTalk`.
