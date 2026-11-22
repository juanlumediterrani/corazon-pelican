using System;
using System.Text;
using CorazonPelican.Core.Dialogo;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace CorazonPelican.Adaptador
{
    /// <summary>
    /// Subclase de <see cref="TextBox"/> que mitiga las trampas de <c>limitWidth</c>,
    /// pegado sin límite y caracteres ausentes de la fuente (V-06, CP-504).
    /// </summary>
    public sealed class TextBoxTextoLibre : TextBox
    {
        public TextBoxTextoLibre(
            Texture2D? textBoxTexture,
            Texture2D? caretTexture,
            SpriteFont font,
            Color textColor,
            int textLimit = GeneradorOpcionesJugador.LongitudMaximaTextoLibre)
            : base(textBoxTexture, caretTexture, font, textColor)
        {
            this.limitWidth = false;
            this.textLimit = Math.Max(1, textLimit);
        }

        public override void RecieveTextInput(char inputChar)
        {
            if (char.IsControl(inputChar) || !this.CaracterSoportado(inputChar))
            {
                return;
            }

            if (this.Text.Length >= this.textLimit)
            {
                return;
            }

            if (inputChar == '"')
            {
                this.Text += inputChar;
                return;
            }

            base.RecieveTextInput(inputChar);
        }

        public override void RecieveTextInput(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var actual = this.Text ?? string.Empty;
            var restante = this.textLimit - actual.Length;
            if (restante <= 0)
            {
                return;
            }

            var sb = new StringBuilder(Math.Min(text.Length, restante));
            foreach (var c in text)
            {
                if (sb.Length >= restante)
                {
                    break;
                }

                if (!char.IsControl(c) && this.CaracterSoportado(c))
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                this.Text = actual + sb;
            }
        }

        private bool CaracterSoportado(char c)
        {
            if (this.Font is null)
            {
                return true;
            }

            return this.Font.DefaultCharacter.HasValue || this.Font.Characters.Contains(c);
        }
    }

    /// <summary>
    /// Menú nativo mínimo para «Decir otra cosa…» con identificadores estables para humanos y Voyager (V-06, CP-504).
    /// </summary>
    public sealed class MenuTextoLibre : IClickableMenu
    {
        public const int IdTextBox = 104;
        public const int IdOkButton = 101;
        public const int IdCancelButton = 102;

        private const int AnchoCaja = 640;
        private const int AltoMenu = 240;

        private readonly string titulo;
        private readonly Action<string> alConfirmar;
        private readonly Action? alCancelar;
        private readonly TextBoxTextoLibre textBox;
        private bool finalizado;

        public readonly ClickableComponent textBoxCC;
        public readonly ClickableTextureComponent okButton;
        public readonly ClickableTextureComponent cancelButton;

        public MenuTextoLibre(
            string titulo,
            Action<string> alConfirmar,
            Action? alCancelar = null,
            int longitudMaxima = GeneradorOpcionesJugador.LongitudMaximaTextoLibre)
            : base(
                (Game1.uiViewport.Width - AnchoCaja) / 2,
                (Game1.uiViewport.Height - AltoMenu) / 2,
                AnchoCaja,
                AltoMenu,
                showUpperRightCloseButton: false)
        {
            this.titulo = string.IsNullOrWhiteSpace(titulo) ? "¿Qué quieres decirle?" : titulo.Trim();
            this.alConfirmar = alConfirmar ?? throw new ArgumentNullException(nameof(alConfirmar));
            this.alCancelar = alCancelar;

            var cajaX = this.xPositionOnScreen + 40;
            var cajaY = this.yPositionOnScreen + 104;
            var cajaAncho = AnchoCaja - 80;

            this.textBox = new TextBoxTextoLibre(null, null, Game1.dialogueFont, Game1.textColor, longitudMaxima)
            {
                X = cajaX,
                Y = cajaY,
                Width = cajaAncho,
                Height = 48,
                TitleText = this.titulo,
                Text = string.Empty,
            };
            this.textBox.OnEnterPressed += this.AlPulsarEnterEnCaja;

            this.textBoxCC = new ClickableComponent(new Rectangle(cajaX, cajaY, cajaAncho, 48), "textBox")
            {
                myID = IdTextBox,
                rightNeighborID = IdOkButton,
                downNeighborID = IdOkButton,
            };

            this.okButton = new ClickableTextureComponent(
                "ok",
                new Rectangle(this.xPositionOnScreen + AnchoCaja - 168, this.yPositionOnScreen + AltoMenu - 72, 64, 64),
                null,
                null,
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
                1f)
            {
                myID = IdOkButton,
                leftNeighborID = IdTextBox,
                upNeighborID = IdTextBox,
                rightNeighborID = IdCancelButton,
            };

            this.cancelButton = new ClickableTextureComponent(
                "cancel",
                new Rectangle(this.xPositionOnScreen + AnchoCaja - 92, this.yPositionOnScreen + AltoMenu - 72, 64, 64),
                null,
                null,
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 47),
                1f)
            {
                myID = IdCancelButton,
                leftNeighborID = IdOkButton,
                upNeighborID = IdTextBox,
            };

            if (Game1.keyboardDispatcher is not null)
            {
                Game1.keyboardDispatcher.Subscriber = this.textBox;
            }

            this.textBox.Selected = true;
            this.textBox.Update();

            this.populateClickableComponentList();
            if (Game1.options?.SnappyMenus == true)
            {
                this.snapToDefaultClickableComponent();
            }
        }

        public override void snapToDefaultClickableComponent()
        {
            this.currentlySnappedComponent = this.textBoxCC;
            this.snapCursorToCurrentSnappedComponent();
        }

        public override bool readyToClose() => !this.textBox.Selected;

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (this.okButton.containsPoint(x, y))
            {
                this.Confirmar();
                return;
            }

            if (this.cancelButton.containsPoint(x, y))
            {
                this.Cancelar();
                return;
            }

            if (this.textBoxCC.containsPoint(x, y))
            {
                this.textBox.SelectMe();
                return;
            }

            this.textBox.Selected = false;
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.okButton.tryHover(x, y);
            this.cancelButton.tryHover(x, y);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                this.Cancelar();
                return;
            }

            if (key == Keys.Enter)
            {
                this.Confirmar();
                return;
            }

            // Mientras la caja de texto tiene el foco, no delegar en base.receiveKeyPress
            // para evitar que Keys.E (menuButton) o WASD cierren el menú o muevan el cursor.
            if (this.textBox.Selected)
            {
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Buttons button)
        {
            if (Game1.textEntry is not null)
            {
                return;
            }

            if (button == Buttons.B)
            {
                this.Cancelar();
                return;
            }

            if (button is Buttons.DPadUp or Buttons.DPadDown or Buttons.DPadLeft or Buttons.DPadRight
                or Buttons.LeftThumbstickUp or Buttons.LeftThumbstickDown or Buttons.LeftThumbstickLeft or Buttons.LeftThumbstickRight)
            {
                this.textBox.Selected = false;
            }

            base.receiveGamePadButton(button);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            this.textBox.Update();
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);

            SpriteText.drawStringWithScrollCenteredAt(
                b,
                this.titulo,
                this.xPositionOnScreen + (this.width / 2),
                this.yPositionOnScreen + 24);

            Game1.drawDialogueBox(
                this.textBox.X - 16,
                this.textBox.Y - 24,
                this.textBox.Width + 32,
                this.textBox.Height + 48,
                speaker: false,
                drawOnlyBox: true);

            this.textBox.Draw(b, drawShadow: false);
            this.okButton.draw(b);
            this.cancelButton.draw(b);
            this.drawMouse(b);
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            if (Game1.keyboardDispatcher is not null && ReferenceEquals(Game1.keyboardDispatcher.Subscriber, this.textBox))
            {
                Game1.keyboardDispatcher.Subscriber = null;
            }

            if (Game1.textEntry is not null)
            {
                Game1.closeTextEntry();
            }

            if (!this.finalizado)
            {
                this.finalizado = true;
                this.alCancelar?.Invoke();
            }
        }

        private void AlPulsarEnterEnCaja(TextBox sender) => this.Confirmar();

        private void Confirmar()
        {
            var texto = GeneradorOpcionesJugador.NormalizarTextoLibre(this.textBox.Text);
            if (texto is null)
            {
                Game1.playSound("cancel");
                return;
            }

            this.finalizado = true;
            Game1.playSound("smallSelect");
            this.exitThisMenuNoSound();
            this.alConfirmar(texto);
        }

        private void Cancelar()
        {
            this.finalizado = true;
            Game1.playSound("bigDeSelect");
            this.exitThisMenuNoSound();
            this.alCancelar?.Invoke();
        }
    }
}
