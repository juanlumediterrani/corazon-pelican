# xnbdump

Vuelca a la consola los assets `Dictionary<string,string>` del `Content/` del juego
(horarios de NPC, `Data/animationDescriptions`…). Los `.xnb` están comprimidos y `strings` no
sirve; esto usa el `ContentManager` de MonoGame con un proveedor de servicios nulo.

```bash
dotnet run --project tools/xnbdump -- "$HOME/.steam/debian-installation/steamapps/common/Stardew Valley" "Characters/schedules/Leah" "Data/animationDescriptions"
```

La compilación copia `MonoGame.Framework.dll` del juego a `bin/` (no versionado). Si el juego
está en otra ruta: `-p:GamePath=...`. **No versionar la salida**: es contenido del juego.

Para tipos de `StardewValley.GameData` (p. ej. `Data/Characters`) hay que referenciar también
`StardewValley.GameData.dll` y `Stardew Valley.dll` y cargar con el tipo concreto.
