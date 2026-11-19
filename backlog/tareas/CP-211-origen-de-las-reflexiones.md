---
id: CP-211
titulo: Transportar y comprobar el origen de las reflexiones del diario
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-002]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

Que la mente pueda relacionar una impresión con dos momentos concretos de su tarde y que el motor pueda comprobar de dónde procede.

## Alcance

Contrato y validador puro. CP-106 asignará los identificadores al escribir el diario. CP-206 usará la forma con origen al recuperar y guardar recuerdos. CP-210 la aprovechará para el humor.

## Criterios de aceptación

- [x] Un pulso ofrece episodios identificados y una respuesta cita uno o más por reflexión. Los ejemplos compartidos `pulso--diario-con-origen` y `respuesta--reflexion-con-origen` validan en Python y C# y conservan sus datos en la ida y vuelta por DTO. El ejemplo sin episodio se rechaza en ambos lenguajes.
- [x] `OrigenDiarioTests` comprueba la pareja época/pulso, la pertenencia de cada id a los episodios ofrecidos y el rechazo de una reflexión sin origen cuando el pulso contiene episodios identificados.
- [x] El formato inicial de prosa sigue siendo legible. Los ejemplos anteriores siguen en las dos suites. En el esquema, cada pulso y cada propuesta de memoria admiten una sola representación del diario o del recuerdo.

Verificación del 2026-11-19 con los comandos de AGENTS.md y despliegue desactivado. Compilación con 0 advertencias y 0 errores. xUnit, 144 superados y 0 omitidos. pytest, 43 superados.
