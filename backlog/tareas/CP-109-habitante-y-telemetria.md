---
id: CP-109
titulo: HabitanteModule, telemetría JSONL y comandos cp_escena / cp_pulso
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-101, CP-104, CP-106]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

Conectar el bucle de sensorio y atención por tick (`HabitanteModule`) en el host, grabar la telemetría `.jsonl` (ADS §12.2) y habilitar los comandos `cp_escena` y `cp_pulso` (ADS §12.3).

## Contexto

ADS §2, §4 y §12.2–§12.3. `CP-101` aporta `ObservadorJuego`; `CP-104`, `GeneradorEscena` y `GeneradorAccion`; `CP-106`, `Bufer`, `Huella` y `EstadoPersonaje`. Faltaba la pieza que une ese flujo en `UpdateTicked` para la aceptación de fase 1 (`CP-108`) con `MenteHabilitada=false`, y la salida de telemetría local.

## Alcance

- `HabitanteModule` (`IModule`, `LadoModulo.Anfitrion`): por cada personaje gobernado y cada `SensorioCadenciaTicks`, ejecuta observar → saliencia/novedad/bandas → prosa de acción/búfer/huella → disparadores → generar escena/pulso → registrar episodio en diario; mide tiempos con `MedidorCosteBucle`.
- Escritor de telemetría JSONL (`Mods/CorazonPelican/telemetria/{saveId}/`: `escenas.jsonl`, `pulsos.jsonl`, `respuestas.jsonl`, `rechazos.jsonl`, `actos.jsonl`) gobernado por `CorazonConfig.Telemetria`; nunca leído por componentes de memoria del personaje.
- Comandos `cp_escena <personaje>` (genera e imprime la escena actual sin gastar presupuesto) y `cp_pulso <personaje>` (fuerza un pulso respetando la contabilidad del presupuesto y lo registra en telemetría).

## Criterios de aceptación

- [ ] FakeWorld / bucle integrado: un día simulado con `MenteHabilitada=false` produce `escenas.jsonl`, entradas en el diario y métricas de coste sin bloquear el tick.
- [ ] `cp_escena` no consume plazas del presupuesto ni reinicia la huella o el integrador; `cp_pulso` sí sigue el protocolo de pulso y queda trazado en `pulsos.jsonl` / `escenas.jsonl`.
- [ ] Con `Telemetria = false` no se escriben ficheros en disco.

## Fuera de alcance

- Ejecución de acciones del cuerpo (F3) o envío por red de respuestas (CP-203 / CP-204).
