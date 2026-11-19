# Escenas doradas (R-01)

Instantáneas de salida completa (escena en 2ª persona y presente + apéndice seco) generadas por `GeneradorEscena` para observaciones fijas de Leah (`EscenaDeterministaTests`).

## Regeneración

Solo cuando cambien las plantillas o el formato de salida de forma intencionada:

```bash
CORAZON_ACTUALIZAR_ESCENAS=1 dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --filter Escena_dorada_coincide_con_instantanea
```

Después revisa `git diff` sobre los ficheros `.escena.txt` y vuelve a ejecutar `dotnet test` sin la variable para confirmar que las instantáneas coinciden byte a byte.
