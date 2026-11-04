---
id: CP-005
titulo: Configuración validada, registro de módulos y comando cp_estado
estado: pendiente
prioridad: P2
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
---

## Objetivo

El mod arranca con `config.json` validado, registra sus módulos aislados y responde a `cp_estado`.

## Contexto

ADS §12. Patrón `IModule`/`ModContext` y `ServerConfig.Validate()` de Pelican 4Ever (`Pelican4Ever/Framework/IModule.cs`).

## Alcance

- `CorazonConfig` con las claves de ADS §12.1 y `Validate()`.
- Registro de módulos con `try/catch` por módulo; un fallo desactiva ese módulo, no el mod.
- Guarda de host: todo el motor solo corre si `Context.IsMainPlayer`.
- `cp_estado` y `cp_mente on/off`.

## Criterios de aceptación

- [ ] Tests de `Validate()`: puerto fuera de rango, `TiempoMaxRespuestaMs <= 0`, personaje sin perfil.
- [ ] Con config inválida el mod registra los errores y queda inactivo sin excepción no capturada.
- [ ] `cp_estado` imprime personajes, `MenteHabilitada`, presupuesto y estado de la API.

## Fuera de alcance

- Resto de comandos `cp_*` (cada uno con su tarea).
