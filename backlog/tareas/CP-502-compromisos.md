---
id: CP-502
titulo: Compromisos con plazo cuya saliencia sube hasta provocar una pulsión
estado: pendiente
prioridad: P1
hito: F5
depende_de: [CP-501, CP-201, CP-202]
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
- Al acercarse el plazo, consultar el presupuesto de CP-202. Si quedan plazas ordinarias libres o extra, emitir el pulso. Si ambas se agotan, registrar una petición de prórroga para ese compromiso y destinatario, conservando el plazo vigente (P-12, CP-509). La cadena de despertar también repasa los compromisos del día (CP-209).

## Criterios de aceptación

- [ ] `CompromisoSalienciaTests`: al acercarse el plazo se emite un pulso ordinario, uno extra o se registra una petición según los saldos (R-09). Repetir la observación del mismo compromiso pendiente de prórroga conserva una única petición y el plazo original.
- [ ] Un compromiso vencido sin cumplir pasa a la memoria como incumplido y aparece en el siguiente pulso con esa persona.
- [ ] Guardar y cargar conserva compromisos y su estado.

## Fuera de alcance

- Generar el plan de cumplimiento (lo hace la mente).
