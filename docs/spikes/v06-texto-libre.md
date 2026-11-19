# Spike V-06 · Menú nativo para «Decir otra cosa…» (`CP-014`)

- **Estado:** análisis estático completado (2026-11-19) sobre el decompilado de Stardew Valley `1.6.15.24356`, los assets de fuentes del juego instalado y el puente de Stardew Voyager (`Dr4coVoyagerBridge`). **Sin ejecución en juego vivo** (regla 4 de `AGENTS.md`): las pruebas interactivas en ventana quedan explícitamente delimitadas en §5 para cuando se implemente [`CP-504`](../../backlog/tareas/CP-504-opciones-jugador.md).
- **Decisión recomendada:** implementar un **`IClickableMenu` propio mínimo (`MenuTextoLibre`)** compuesto con `StardewValley.Menus.TextBox`, activación automática de `TextEntryMenu` en modo mando y tres componentes clicables estables (`textBoxCC`, `okButton`, `cancelButton`).

---

## 1. Comparativa técnica (decompilado `1.6.15.24356`)

| Aspecto | `NamingMenu` | `TitleTextInputMenu` | `TextEntryMenu` | `IClickableMenu` propio + `TextBox` |
|---|---|---|---|---|
| **Herencia** | `IClickableMenu` | `NamingMenu` | `IClickableMenu` | `IClickableMenu` |
| **Propósito vanilla** | Nombrar mascotas, hijos, caballos, animales nacidos | Código de invitación co-op (`CoopMenu`) y carteles de texto (`Object.CheckForActionOnTextSign`) | Teclado virtual en pantalla para mando (overlay `Game1.textEntry`) | Entrada de frase libre en diálogo con NPC |
| **Caja de texto** | `TextBox(null, null, Game1.dialogueFont, Game1.textColor)`, `Width = 256`, `limitWidth = true` (defecto), `textLimit = -1` | Hereda `textBox`, cambia `Width = 512`, `X -= 128`, `limitWidth = false` | No tiene propia: escribe en `protected TextBox _target` | `TextBox` con `Game1.dialogueFont`, `Width = 560`, `limitWidth = false`, `textLimit` explícito (p. ej. `160`) |
| **Valor inicial** | `defaultName ?? Dialogue.randomName()` | `default_text` (`""` en ctor base) | N/A | `""` (vacío) |
| **Botones expuestos (`myID`)** | `doneNamingButton` (`102`), `randomButton` (`103`), `textBoxCC` (`104`); define `region_okButton = 101` sin campo `okButton` | Hereda los de `NamingMenu` (`randomButton.visible = false`) y añade `pasteButton` (`105`) | `keys` (`0..39`), `backspaceButton` (`99990`), `spaceButton` (`99991`), `okButton` (`99992`), `upperCaseButton` (`99993`), `symbolsButton` (`99994`) | Solo los necesarios: `okButton` (`101`), `cancelButton` (`102`), `textBoxCC` (`104`) |
| **Cancelación (`Esc` / botón)** | **No cancela**: `receiveKeyPress` bloquea `menuButton` y no tiene botón cancelar ni `upperRightCloseButton` | `update(GameTime)` comprueba `Buttons.B` y `Keys.Escape` y llama `Game1.exitActiveMenu()` (o `TitleMenu.backButtonPressed()`) | `Buttons.B` o `Keys.Delete` llaman a `Close()` (`Game1.closeTextEntry()`) | `cancelButton`, `Keys.Escape` y `Buttons.B` invocan `onCancel` y cierran limpio |
| **Confirmación (`Enter` vía `receiveKeyPress`)** | **No confirma** (`receiveKeyPress` ignora `Enter`; solo `KeyboardDispatcher` → `RecieveCommandInput('\r')` → `OnEnterPressed` → `textBoxEnter`) | Igual que `NamingMenu` (`minLength = 0`, permite enviar vacío) | `Buttons.Start` / `okButton` envían `'\r'` a `_target.RecieveCommandInput('\r')` | `textBox.OnEnterPressed` **y** `receiveKeyPress(Keys.Enter)` confirman si `!string.IsNullOrWhiteSpace` |
| **Filtrado de texto** | `FilterInput = true` → `Utility.FilterDirtyWords(sender.Text)` | Configurable en constructor (`filterInput`) | No filtra | Configurable; por defecto sin censura SDK salvo que el mod la quiera |

### Detalles exactos de las clases descartadas o insuficientes

1. **`StardewValley.Menus.TextEntryMenu`**:
   - Confirmado en `TextEntryMenu.cs` (`windowWidth = 168`, `windowHeight = 88`, caja centrada `672×352`) y `Game1.showTextEntry(TextBox)` (`Game1.cs:4459-4465`): es el **teclado virtual auxiliar** que se asigna a `Game1.textEntry` (overlay sobre el menú activo), no un menú de diálogo independiente.
   - Se abre automáticamente desde `TextBox.Update()` (`TextBox.cs:139-146`) cuando `_showKeyboard` está activo (puesto por `TextBox.Selected = true`) y `Game1.options.gamepadControls && !Game1.lastCursorMotionWasMouse`.
   - En consecuencia: **se reutiliza automáticamente para mando** al alojar un `TextBox` nativo, no sustituye al menú contenedor.

2. **`StardewValley.Menus.NamingMenu`**:
   - **Ancho y truncado por píxeles:** fija `textBox.Width = 256` (`NamingMenu.cs:50`) manteniendo `textBox.limitWidth = true` (`TextBox.cs:22`), lo que hace que `TextBox.Text` recorte la frase cuando `_font.MeasureString(_text).X > 235` (`TextBox.cs:60-63`). Una frase como «¿Me dibujarías el lago?» ya ronda o supera ese ancho antes de completar una oración media.
   - **Botón de dado (`randomButton`, `myID = 103`):** `NamingMenu.cs:56-60, 161-166` muestra el botón de nombre aleatorio (`Dialogue.randomName()`). Aunque se oculte con `randomButton.visible = false`, el campo público permanece en la instancia y `GetActiveMenuComponents` del puente de Voyager lo sigue enumerando (ver §3).
   - **Sin salida/cancelación:** no crea `upperRightCloseButton` ni `cancelButton`, no sobreescribe `readyToClose()` y su `receiveKeyPress(Keys key)` (`NamingMenu.cs:125-131`) bloquea `menuButton` tanto con la caja enfocada como desenfocada.
   - **No extensible limpiamente para confirmación por `Keys.Enter`:** `textBoxEnter(TextBox sender)` (`NamingMenu.cs:85`) no es `virtual` y el constructor enlaza `textBox.OnEnterPressed += textBoxEnter`.

3. **`StardewValley.Menus.TitleTextInputMenu`**:
   - Soluciona el ancho (`Width = 512`, `limitWidth = false`, `randomButton.visible = false`) y añade `Escape`/`Buttons.B` en `update(GameTime)` (`TitleTextInputMenu.cs:68-84`).
   - Aun así hereda el campo `randomButton` de `NamingMenu`, añade `pasteButton` (`myID = 105`, posicionado con `Game1.viewport.Height` en lugar de `Game1.uiViewport.Height`), fija `minLength = 0` (admite envíos vacíos) y su cancelación en `update` llama a `Game1.exitActiveMenu()` sin distinguir cancelación de confirmación si no se envuelve el ciclo de salida.

---

## 2. Teclado físico, `KeyboardDispatcher`, tildes y `¿`

### 2.1. Ruta del teclado físico y composición (`SDL TextInput`)

- En `StardewValley.KeyboardDispatcher` (`KeyboardDispatcher.cs:65-82`), en Linux y Windows (`PlatformID.Unix` o `Win32NT`) el juego se suscribe al evento de ventana `GameWindow.TextInput += Event_TextInput`:
  - `Event_TextInput(object sender, TextInputEventArgs e)` (`KeyboardDispatcher.cs:104-126`) encola `Keys.Back` (`'\b'`), `Keys.Enter` (`'\r'`) y `Keys.Tab` (`'\t'`) en `_commandInputs`, y cualquier `e.Character` con `!char.IsControl(e.Character)` en `_charsEntered`.
  - En cada tick con foco (`Game1.HasKeyboardFocus()`), `Game1.cs:3883` llama a `keyboardDispatcher.Poll()` (`KeyboardDispatcher.cs:173-241`), que entrega:
    - cada `char` de `_charsEntered` a `_subscriber.RecieveTextInput(char)`;
    - cada comando de `_commandInputs` a `_subscriber.RecieveCommandInput(char)`;
    - Ctrl+V (o Cmd+V en macOS) mediante `DesktopClipboard.GetText` a `_subscriber.RecieveTextInput(string)`.
- En Linux (DesktopGL / SDL2), `GameWindow.TextInput` recibe `SDL_TEXTINPUT` con caracteres Unicode ya compuestos por el gestor de teclado / tecla muerta (`´` + `i` → `í`) o IME.

### 2.2. Soporte de «¿Me dibujarías el lago?» y trampas de `TextBox` / `SpriteFont`

1. **Glifos de `Game1.dialogueFont` (`Fonts\SpriteFont1`):**
   - `Game1.cs:2360, 3103` carga `dialogueFont = content.Load<SpriteFont>("Fonts\\SpriteFont1")`.
   - Comprobación en los `.xnb` instalados: `Content/Fonts/SpriteFont1.xnb` y `Content/Fonts/SpriteFont1.es-ES.xnb` son idénticos byte a byte (`SHA-256: 51416532910d707af18a8759b9ae7c9b5a45d2d4964febf43c2dbb938298691b`); lo mismo ocurre con `SmallFont.xnb` y `SmallFont.es-ES.xnb` (`98bd749209104b82a9d67776df09249cee11aa355afd621b8e1db96231876c66`).
   - En `TextEntryMenu.cs:22, 328-329`, la capa 2 del teclado virtual contiene literalmente `"áéíóúü¡!¿?"` y `"ñ"`, y `TextEntryMenu.draw` llama incondicionalmente a `Game1.dialogueFont.MeasureString(key.name)` y `b.DrawString(Game1.dialogueFont, key.name, ...)`. Esto confirma en código del juego que `Game1.dialogueFont` incluye `á, é, í, ó, ú, ü, ñ, ¡, ¿`.
   - Para el título del menú, `SpriteText` usa la hoja latina cuando `LocalizedContentManager.CurrentLanguageLatin` es verdadero (`en`, `es`, `de`, `pt`, `fr`, `it`, `tr`, `hu`; `LocalizedContentManager.cs:117-131`) mapeando `c - 32` (`SpriteText.cs:875`), lo que cubre el bloque Latin-1 (`¿` = 191, `í` = 237).

2. **Trampas reales detectadas en `TextBox.cs` (deben mitigarse en CP-504):**
   - **Trampa 1 (`limitWidth`):** por defecto `TextBox.limitWidth = true`. El setter de `Text` (`TextBox.cs:60-63`) trunca por anchura en píxeles (`Width - 21`). Con `limitWidth = false`, `TextBox.Draw` (`TextBox.cs:171-176`) hace scroll visual del prefijo (`while (vector.X > (float)Width) text = text.Substring(1);`) conservando el texto completo hasta `textLimit`.
   - **Trampa 2 (`RecieveTextInput(string)` y `textLimit`):** `TextBox.RecieveTextInput(char)` (`TextBox.cs:193`) corta si `Text.Length >= textLimit`. En cambio, `RecieveTextInput(string)` (`TextBox.cs:229`, usado al pegar con Ctrl+V o por `SteamHelper.OnKeyboardDismissed`) solo comprueba `Text.Length < textLimit` **antes** de concatenar toda la cadena, permitiendo superar `textLimit` en un pegado largo; y el setter `TextBox.Text = ...` no comprueba `textLimit`. Hay que aplicar el tope de longitud también en el pegado / setter y al confirmar.
   - **Trampa 3 (comillas `"`):** `TextBox.RecieveTextInput(char)` (`TextBox.cs:216-217`) descarta silenciosamente el carácter `'"'` (`case '"': return;`). Si en CP-504 se quiere permitir comillas tipadas manualmente, una subclase de `TextBox` puede admitirlo; si no hace falta, queda documentado como comportamiento vanilla de `TextBox`.
   - **Trampa 4 (caracteres fuera de `_font.Characters`):** `TextBox.Text` y `TextBox.Draw` invocan `_font.MeasureString` y `DrawString` sin comprobar `_font.Characters.Contains(c)`. Si un jugador pega o escribe un carácter ausente de la fuente (p. ej. emoji o guion largo si faltara) y `DefaultCharacter == null`, MonoGame lanza `ArgumentException`. Una subclase `TextBoxTextoLibre` debe filtrar cualquier carácter no contenido en `_font.Characters` (cuando `_font.DefaultCharacter == null`).
   - **Trampa 5 (`Options.menuButton` incluye `Keys.E`):** en `Options.cs:337-341`, `menuButton` contiene `{ Keys.E, Keys.Escape }`. Además `Game1.updateActiveMenu` (`Game1.cs:4985-4992`) envía todas las teclas pulsadas a `childMenu.receiveKeyPress(keys)` al mismo tiempo que `KeyboardDispatcher` alimenta el `TextBox`. Si se delegara a `IClickableMenu.receiveKeyPress(key)` con `readyToClose() == true`, pulsar `e` o `w/a/s/d` mientras se escribe cerraría el menú o movería el cursor (`applyMovementKey`). Por tanto, mientras `textBox.Selected == true`, `receiveKeyPress` **no debe llamar** a `base.receiveKeyPress(key)` y solo debe interceptar `Keys.Escape` (cancelar) y `Keys.Enter` (confirmar).

### 2.3. Soporte de mando

- Al construir el menú, si `textBox.Selected = true; textBox.Update();` se ejecuta cuando `Game1.options.gamepadControls && !Game1.lastCursorMotionWasMouse`, `TextBox.Update()` (`TextBox.cs:141-144`) llama automáticamente a `Game1.showTextEntry(this)`, abriendo `TextEntryMenu` con su capa de tildes y `¿`.
- En `receiveGamePadButton(Buttons button)` (análogo a `NamingMenu.cs:103-122`), pulsar direcciones de D-Pad o stick izquierdo libera `textBox.Selected = false` si `TextEntryMenu` no está interceptando el mando, permitiendo saltar con `SnappyMenus` entre `textBoxCC`, `okButton` y `cancelButton`; `Buttons.B` cancela el menú si `Game1.textEntry == null`.

---

## 3. Compatibilidad con Stardew Voyager (`Dr4coVoyagerBridge` y `SV-084`)

Inspeccionado en solo lectura en `/home/mtx/Documentos/Projects/stardew-voyager/`:

1. **Observación (`GET /menu_components`):**
   - `NativeMenuInput.GetActiveMenuComponents()` (`bridge/NativeMenuInput.cs:123-215`) recorre por reflexión todos los campos `ClickableComponent` (y colecciones) de `menu.GetType()` y sus clases base, devolviendo `{ field, name, myID, x, y, width, height }`.
   - **Hallazgo clave:** `GetActiveMenuComponents()` **no filtra por `component.visible`**. Si heredásemos de `NamingMenu`, el campo `randomButton` (`myID = 103`) seguiría apareciendo como componente clicable observado aunque tuviera `visible = false`. Con un `IClickableMenu` propio solo existen los componentes reales.
2. **Clic (`POST /menu_click`):**
   - `NativeMenuInput.ClickActiveMenu(x, y)` (`bridge/NativeMenuInput.cs:27-82`) ejecuta `performHoverAction(x, y)` → `receiveLeftClick(x, y, true)` → `releaseLeftClick(x, y)`. Funciona sobre cualquier `IClickableMenu` cuyos `ClickableComponent` tengan `bounds` reales y coherentes con `receiveLeftClick`.
3. **Confirmación por teclado (`POST /menu_confirm`):**
   - `NativeMenuInput.ConfirmActiveMenu()` (`bridge/NativeMenuInput.cs:217-234`) invoca `menu.receiveKeyPress(Keys.Enter)`.
   - En `NamingMenu` vanilla esto **no confirma** (porque `NamingMenu.receiveKeyPress` ignora `Keys.Enter`). En `MenuTextoLibre`, manejar `Keys.Enter` en `receiveKeyPress` hace compatible también `POST /menu_confirm`.
4. **Entrada de texto (`POST /name` vs contrato `SV-084`):**
   - `Tools.NameActiveMenu(string text)` (`bridge/Tools.cs:1841-1889`, ruta `POST /name`) exige `if (menu is not StardewValley.Menus.NamingMenu naming)`, asigna `box.Text = text ?? string.Empty` e invoca `textBoxEnter(box)` por reflexión en el mismo paso.
   - No existe actualmente en `Dr4coVoyagerBridge` una ruta genérica para escribir en el `TextBox` de un `IClickableMenu` cualquiera sin confirmar de golpe (en `Tools.cs:1896`, `SetAnimalPurchaseName` para `/animal_purchase_name` está acotada a `PurchaseAnimalsMenu`, aunque sigue exactamente el patrón `SV-084` de `voyager/menu_contract.py`: escribir primero y confirmar después con clic nativo en un botón observado).
   - **Conclusión de compatibilidad:**
     - **Opción recomendada (limpia y fiel a `SV-084`):** usar `MenuTextoLibre : IClickableMenu` en Corazón Pelican y, cuando se integre Voyager en Hito 6 (fuera de este repositorio), añadir en `Dr4coVoyagerBridge` una ruta genérica de escritura sobre menús con `TextBox` (p. ej. `POST /menu_text`, análoga a `/animal_purchase_name`: busca el campo `textBox`, verifica longitud y caracteres, asigna `box.Text`, no confirma por su cuenta) seguida de `POST /menu_click` sobre `okButton` (`myID = 101`) o `POST /menu_confirm`.
     - **Alternativa de respaldo sin tocar `Dr4coVoyagerBridge`:** si en una prueba temprana no se quisiera modificar Voyager, hacer que `MenuTextoLibre` herede de `NamingMenu` (con `textBox` ampliado, `limitWidth = false`, `FilterInput = false`, `randomButton.bounds = Rectangle.Empty` y `randomButton.visible = false`) haría funcionar `POST /name` tal como está, asumiendo que `/menu_components` vería un `randomButton` vacío y que `POST /name` escribiría y enviaría en una sola llamada. No es necesario pagar esa deuda técnica en `CP-504` salvo que se priorice evitar cambios en el puente.

---

## 4. Diseño mínimo recomendado para `CP-504`

Clase propuesta en el mod SMAPI: `CorazonPelican.Adaptador.MenuTextoLibre : IClickableMenu` (junto con una subclase mínima `TextBoxTextoLibre : TextBox` para las guardas de pegado y glifos).

### Componentes estables expuestos

| Campo | Tipo | `myID` | `name` | Vecinos (`SnappyMenus`) | Acción |
|---|---|---|---|---|---|
| `textBoxCC` | `ClickableComponent` | `104` | `"textBox"` | `rightNeighborID = 101` | Enfoca `textBox.SelectMe()` y abre `TextEntryMenu` en modo mando |
| `okButton` | `ClickableTextureComponent` | `101` | `"ok"` | `leftNeighborID = 104`, `rightNeighborID = 102` | Envía `textBox.Text.Trim()` si no está vacío (sprite tick `46`) |
| `cancelButton` | `ClickableTextureComponent` | `102` | `"cancel"` | `leftNeighborID = 101` | Cancela y cierra el menú (sprite aspa `47`) |

*(Nota: no usar `upperRightCloseButton` duplicado si ya existe `cancelButton` explícito, para mantener un único componente de cancelación sin ambigüedad en `/menu_components`.)*

### Configuración y reglas mínimas

1. **Caja de texto (`TextBox`):**
   - Constructor `base(null, null, Game1.dialogueFont, Game1.textColor)` (o textura `LooseSprites\textBox` dentro de una caja de diálogo `Game1.drawDialogueBox`).
   - `Width = 560`, `Height = 48` (o `192` si usa `textBoxTexture == null` como `NamingMenu`, ajustando el rectángulo de `textBoxCC` al área real clicable de la caja).
   - `limitWidth = false` y `textLimit = LongitudMaximaTextoLibre` (p. ej. `160` caracteres, compartido/coherente con el validador de Core en `CP-504`).
   - `TitleText` con el título del menú (p. ej. «¿Qué quieres decirle?»), utilizado por teclados virtuales como Steam Deck.
   - Subclase o filtro en `RecieveTextInput(char)` / `RecieveTextInput(string)`:
     - descarta caracteres de control y caracteres ausentes en `_font.Characters` cuando `_font.DefaultCharacter == null`;
     - aplica el tope `textLimit` también al pegar cadenas (`RecieveTextInput(string)`).
2. **Ciclo de entrada y cierre:**
   - En constructor: `Game1.keyboardDispatcher.Subscriber = textBox; textBox.Selected = true; textBox.Update();` (abre `TextEntryMenu` solo si mando activo), `populateClickableComponentList()`, y `if (Game1.options.SnappyMenus) snapToDefaultClickableComponent();` (enfocando `104`).
   - `receiveKeyPress(Keys key)`:
     - si `key == Keys.Escape` → cancelar (`Cancelar()`);
     - si `key == Keys.Enter` → confirmar (`Confirmar()`);
     - si `textBox.Selected` → **no** llamar a `base.receiveKeyPress(key)` (protege `E` y `WASD`);
     - si `!textBox.Selected` → `base.receiveKeyPress(key)` (permite navegar con `SnappyMenus` entre `104`, `101`, `102`).
   - `receiveGamePadButton(Buttons button)`: si `button == Buttons.B` y `Game1.textEntry == null` → `Cancelar()`; si `textBox.Selected` y se pulsa D-Pad/LeftThumbstick → `textBox.Selected = false` y `snapToDefaultClickableComponent()` / movimiento.
   - `Confirmar()`: recorta con `Trim()` (o valida no vacío/espacios), trunca defensivamente a `textLimit`, cierra con `exitThisMenu()` e invoca `onSubmit(texto)`.
   - `Cancelar()`: cierra con `exitThisMenu()` e invoca `onCancel?.Invoke()`.
   - `cleanupBeforeExit()`: `textBox.Selected = false;` si `Game1.keyboardDispatcher.Subscriber == textBox` lo pone a `null`, y si `Game1.textEntry != null` llama a `Game1.closeTextEntry()`.

---

## 5. Verificación estática realizada vs. confirmación en vivo pendiente (`CP-504`)

| Punto | Verificado estáticamente (este spike) | Pendiente de confirmación interactiva en vivo (`CP-504` 🎮) |
|---|---|---|
| Selección de componente (`IClickableMenu` + `TextBox` + `TextEntryMenu`) | Sí: comparativa completa de `NamingMenu`, `TitleTextInputMenu`, `TextEntryMenu`, `TextBox` e `IClickableMenu` en el decompilado `1.6.15.24356` | Validar ergonomía visual (posición de `textBox`, `okButton`, `cancelButton` a distintas escalas de UI) |
| Escritura de «¿Me dibujarías el lago?» (teclado físico, tildes, `¿`, límite de longitud) | Sí a nivel de tubería y glifos: `KeyboardDispatcher.Event_TextInput`, `SpriteFont1.xnb == SpriteFont1.es-ES.xnb`, `TextEntryMenu` dibuja `áéíóúü¡!¿?ñ` con `Game1.dialogueFont`, `limitWidth = false` evita truncado en 235 px | Probar en la ventana viva de Linux con teclado físico (tecla muerta `´` + `i` e IME) y confirmar que no se corta ni falla al enviar a la mente |
| Soporte de mando (`TextEntryMenu`) | Sí: `TextBox.Update()` dispara `Game1.showTextEntry(this)` y `TextEntryMenu.letterMaps[2]` contiene `"áéíóúü¡!¿?"` | Probar en vivo apertura/cierre de `TextEntryMenu` y transición de foco con D-Pad hacia `okButton` / `cancelButton` |
| Operabilidad desde Stardew Voyager | Sí: contrato leído en `NativeMenuInput.cs`, `Tools.cs`, `HttpServer.cs` y `menu_contract.py` (`GET /menu_components`, `POST /menu_click`, `POST /menu_confirm`, restricción de `POST /name` a `NamingMenu`) | Ejecutar flujo completo en vivo con `Dr4coVoyagerBridge` (`GET /menu_components` → escritura de texto → clic en `okButton` / `menu_confirm`) |
