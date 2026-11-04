---
id: CP-502
titulo: Compromisos con plazo cuya saliencia sube hasta provocar una pulsión
estado: pendiente
prioridad: P1
hito: F5
depende_de: [CP-501, CP-201]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-09]
---

## Objetivo

Las promesas vuelven a la conciencia de Leah por el mismo mecanismo de atención que todo lo demás.

## Contexto

GSS §9.3. Un compromiso incumplido no se oculta: se recuerda y puede disculparse.

## Alcance

- `Compromisos.cs`: curva de saliencia por plazo, estados cumplido / incumplido.
- Disparador de compromiso que ignora el presupuesto agotado solo en la ventana final del plazo (decisión a documentar). La cadena de despertar también repasa los compromisos del día (CP-209).

## Criterios de aceptación

- [ ] `CompromisoSalienciaTests`: todo compromiso válido genera una pulsión antes de su plazo (R-09), también con presupuesto agotado si se decide así.
- [ ] Un compromiso vencido sin cumplir pasa a la memoria como incumplido y aparece en el siguiente pulso con esa persona.
- [ ] Guardar y cargar conserva compromisos y su estado.

## Fuera de alcance

- Generar el plan de cumplimiento (lo hace la mente).
