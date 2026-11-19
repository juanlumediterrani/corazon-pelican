---
id: CP-215
titulo: Conectar un proveedor de modelo al ciclo de la mente
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-204]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

Conectar un proveedor de modelo al ciclo de la mente.

## Evidencia

Los proveedores configurados llegan a `ModeloProveedorConfigurado.generar`, que lanza una excepción incluso con credenciales presentes. Para evaluar la voz y el humor en CP-208 hace falta al menos un transporte operativo. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [ ] Con la configuración normal del proveedor elegido y un transporte HTTP simulado, comprobar que se envían voz, escena y esquema estrechado y que el ciclo consume una respuesta válida.
- [ ] Cubrir respuesta mal formada y fallo del proveedor. La suite debe poder ejecutarse sin credenciales reales. Registrar en CP-208 la evaluación posterior con un modelo real.
