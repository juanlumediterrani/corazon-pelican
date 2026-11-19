using System;
using System.Collections.Generic;
using System.Globalization;
using CorazonPelican.Core.Sensorio;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace CorazonPelican.Adaptador
{
    /// <summary>
    /// Adaptador de solo lectura que traduce el estado actual del juego (<see cref="NPC"/> y
    /// <see cref="GameLocation"/>) en una <see cref="Observacion"/> del dominio (GSS §3.1, ADS §4, CP-101).
    /// No muta ningún estado del juego.
    /// </summary>
    public sealed class ObservadorJuego
    {
        public const int RadioObjetosCasillas = 32;
        public const int RadioTerrenoCasillas = 16;

        public Observacion Observar(NPC npc, GameLocation? ubicacion = null)
        {
            if (npc is null)
            {
                throw new ArgumentNullException(nameof(npc));
            }

            ubicacion ??= npc.currentLocation;
            var dia = Game1.Date?.TotalDays ?? 0;
            var hora = Game1.timeOfDay;
            var casillaNpc = npc.TilePoint;

            if (ubicacion is null)
            {
                var climaBase = "soleado";
                return new Observacion(
                    dia,
                    hora,
                    string.Empty,
                    casillaNpc.X,
                    casillaNpc.Y,
                    LuzAmbiental.Calcular(hora, climaBase, esExterior: true),
                    climaBase,
                    Array.Empty<EntidadObservada>(),
                    Array.Empty<SucesoObservado>());
            }

            var nombreUbicacion = ubicacion.NameOrUniqueName ?? ubicacion.Name ?? string.Empty;
            var clima = ResolverClima(ubicacion);
            var luz = LuzAmbiental.Calcular(
                hora,
                clima,
                ubicacion.IsOutdoors,
                Game1.getStartingToGetDarkTime(ubicacion),
                Game1.getTrulyDarkTime(ubicacion));

            var (anchoMapa, altoMapa) = ObtenerDimensionesMapa(ubicacion);
            Func<int, int, bool> esOpaco = (tx, ty) => EsCasillaOpaca(ubicacion, anchoMapa, altoMapa, tx, ty);

            var entidades = new List<EntidadObservada>();
            var sucesos = new List<SucesoObservado>();

            this.ObservarGranjeros(npc, ubicacion, nombreUbicacion, casillaNpc, dia, esOpaco, entidades, sucesos);
            this.ObservarPersonajesYMascotas(npc, ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);
            this.ObservarAnimales(ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);
            this.ObservarObjetos(ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);
            this.ObservarMuebles(ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);
            this.ObservarAgua(ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);
            this.ObservarTerreno(ubicacion, nombreUbicacion, casillaNpc, esOpaco, entidades);

            return new Observacion(dia, hora, nombreUbicacion, casillaNpc.X, casillaNpc.Y, luz, clima, entidades, sucesos);
        }

        private void ObservarGranjeros(
            NPC npc,
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            int dia,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades,
            List<SucesoObservado> sucesos)
        {
            foreach (var granjero in ubicacion.farmers)
            {
                if (granjero is null)
                {
                    continue;
                }

                var pos = granjero.TilePoint;
                var referencia = IdentidadesObservacion.Granjero(granjero.UniqueMultiplayerID);
                var nombre = string.IsNullOrWhiteSpace(granjero.Name) ? "granjero" : granjero.Name;
                int? corazones = string.IsNullOrEmpty(npc.Name) ? null : granjero.getFriendshipHeartLevelForNPC(npc.Name);
                var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y, esOpaco);
                var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y);

                entidades.Add(new EntidadObservada(
                    referencia,
                    CategoriaEntidad.Granjero,
                    nombre,
                    nombreUbicacion,
                    pos.X,
                    pos.Y,
                    new[] { "persona", "granjero" },
                    corazones,
                    visible,
                    distancia));

                if (!string.IsNullOrEmpty(npc.Name)
                    && granjero.friendshipData.TryGetValue(npc.Name, out var amistad)
                    && amistad.GiftsToday > 0
                    && amistad.LastGiftDate is not null
                    && amistad.LastGiftDate.TotalDays == dia)
                {
                    var refSuceso = IdentidadesObservacion.Suceso(
                        "regalo",
                        dia.ToString(CultureInfo.InvariantCulture),
                        granjero.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                        amistad.GiftsToday.ToString(CultureInfo.InvariantCulture));
                    var descripcion = $"regalo recibido de {nombre}";

                    sucesos.Add(new SucesoObservado(refSuceso, "regalo", descripcion, ActorRef: referencia));
                    entidades.Add(new EntidadObservada(
                        refSuceso,
                        CategoriaEntidad.Suceso,
                        descripcion,
                        nombreUbicacion,
                        pos.X,
                        pos.Y,
                        new[] { "regalo", "persona" },
                        corazones,
                        visible,
                        distancia));
                }
            }
        }

        private void ObservarPersonajesYMascotas(
            NPC observador,
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            foreach (var personaje in ubicacion.characters)
            {
                if (personaje is null || ReferenceEquals(personaje, observador) || personaje.IsInvisible)
                {
                    continue;
                }

                var pos = personaje.TilePoint;
                var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y, esOpaco);
                var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y);

                if (personaje is Pet mascota)
                {
                    var tipoMascota = NormalizarEtiqueta(mascota.petType.Value, "mascota");
                    var nombreMascota = string.IsNullOrWhiteSpace(mascota.displayName) ? mascota.Name : mascota.displayName;
                    entidades.Add(new EntidadObservada(
                        IdentidadesObservacion.Mascota(mascota.petId.Value),
                        CategoriaEntidad.Animal,
                        string.IsNullOrWhiteSpace(nombreMascota) ? tipoMascota : nombreMascota,
                        nombreUbicacion,
                        pos.X,
                        pos.Y,
                        new[] { "animal", "mascota", tipoMascota },
                        Visible: visible,
                        Distancia: distancia));
                }
                else
                {
                    var nombreOtro = string.IsNullOrWhiteSpace(personaje.displayName) ? personaje.Name : personaje.displayName;
                    entidades.Add(new EntidadObservada(
                        IdentidadesObservacion.Npc(personaje.Name),
                        CategoriaEntidad.Npc,
                        string.IsNullOrWhiteSpace(nombreOtro) ? "habitante" : nombreOtro,
                        nombreUbicacion,
                        pos.X,
                        pos.Y,
                        new[] { "persona", "npc" },
                        Visible: visible,
                        Distancia: distancia));
                }
            }
        }

        private void ObservarAnimales(
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            foreach (var animal in ubicacion.Animals.Values)
            {
                if (animal is null)
                {
                    continue;
                }

                var pos = animal.TilePoint;
                var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y, esOpaco);
                var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, pos.X, pos.Y);
                var tipo = NormalizarEtiqueta(animal.type.Value, "granja");
                var nombre = string.IsNullOrWhiteSpace(animal.displayName) ? animal.Name : animal.displayName;

                entidades.Add(new EntidadObservada(
                    IdentidadesObservacion.Animal(animal.myID.Value),
                    CategoriaEntidad.Animal,
                    string.IsNullOrWhiteSpace(nombre) ? tipo : nombre,
                    nombreUbicacion,
                    pos.X,
                    pos.Y,
                    new[] { "animal", tipo },
                    Visible: visible,
                    Distancia: distancia));
            }
        }

        private void ObservarObjetos(
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            foreach (var par in ubicacion.Objects.Pairs)
            {
                var obj = par.Value;
                if (obj is null)
                {
                    continue;
                }

                var x = (int)par.Key.X;
                var y = (int)par.Key.Y;
                if (Math.Abs(x - casillaNpc.X) > RadioObjetosCasillas || Math.Abs(y - casillaNpc.Y) > RadioObjetosCasillas)
                {
                    continue;
                }

                var idObjeto = !string.IsNullOrWhiteSpace(obj.QualifiedItemId)
                    ? obj.QualifiedItemId
                    : !string.IsNullOrWhiteSpace(obj.ItemId)
                        ? obj.ItemId
                        : obj.Name;
                var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, x, y, esOpaco);
                var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, x, y);
                var nombre = string.IsNullOrWhiteSpace(obj.DisplayName) ? obj.Name : obj.DisplayName;

                entidades.Add(new EntidadObservada(
                    IdentidadesObservacion.Objeto(nombreUbicacion, x, y, idObjeto),
                    CategoriaEntidad.Objeto,
                    string.IsNullOrWhiteSpace(nombre) ? idObjeto : nombre,
                    nombreUbicacion,
                    x,
                    y,
                    EtiquetasDeObjeto(obj),
                    Visible: visible,
                    Distancia: distancia));
            }
        }

        private void ObservarMuebles(
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            foreach (var mueble in ubicacion.furniture)
            {
                if (mueble is null)
                {
                    continue;
                }

                var x = (int)mueble.TileLocation.X;
                var y = (int)mueble.TileLocation.Y;
                var idMueble = !string.IsNullOrWhiteSpace(mueble.QualifiedItemId)
                    ? mueble.QualifiedItemId
                    : !string.IsNullOrWhiteSpace(mueble.ItemId)
                        ? mueble.ItemId
                        : mueble.Name;
                var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, x, y, esOpaco);
                var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, x, y);
                var nombre = string.IsNullOrWhiteSpace(mueble.DisplayName) ? mueble.Name : mueble.DisplayName;

                entidades.Add(new EntidadObservada(
                    IdentidadesObservacion.Mueble(nombreUbicacion, x, y, idMueble),
                    CategoriaEntidad.Objeto,
                    string.IsNullOrWhiteSpace(nombre) ? idMueble : nombre,
                    nombreUbicacion,
                    x,
                    y,
                    EtiquetasDeMueble(mueble),
                    Visible: visible,
                    Distancia: distancia));
            }
        }

        private void ObservarAgua(
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            var matriz = ubicacion.waterTiles?.waterTiles;
            if (matriz is null)
            {
                return;
            }

            var ancho = matriz.GetLength(0);
            var alto = matriz.GetLength(1);
            var minX = Math.Max(0, casillaNpc.X - RadioTerrenoCasillas);
            var maxX = Math.Min(ancho - 1, casillaNpc.X + RadioTerrenoCasillas);
            var minY = Math.Max(0, casillaNpc.Y - RadioTerrenoCasillas);
            var maxY = Math.Min(alto - 1, casillaNpc.Y + RadioTerrenoCasillas);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var celda = matriz[x, y];
                    if (!celda.isWater || !celda.isVisible)
                    {
                        continue;
                    }

                    // Solo casillas de orilla para mantener acotado el coste y evitar cientos de casillas interiores.
                    if (!EsOrillaDeAgua(matriz, ancho, alto, x, y))
                    {
                        continue;
                    }

                    var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, x, y, esOpaco);
                    var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, x, y);

                    entidades.Add(new EntidadObservada(
                        IdentidadesObservacion.Agua(nombreUbicacion, x, y),
                        CategoriaEntidad.Terreno,
                        "agua",
                        nombreUbicacion,
                        x,
                        y,
                        new[] { "agua" },
                        Visible: visible,
                        Distancia: distancia));
                }
            }
        }

        private void ObservarTerreno(
            GameLocation ubicacion,
            string nombreUbicacion,
            Point casillaNpc,
            Func<int, int, bool> esOpaco,
            List<EntidadObservada> entidades)
        {
            foreach (var par in ubicacion.terrainFeatures.Pairs)
            {
                var x = (int)par.Key.X;
                var y = (int)par.Key.Y;
                if (Math.Abs(x - casillaNpc.X) > RadioTerrenoCasillas || Math.Abs(y - casillaNpc.Y) > RadioTerrenoCasillas)
                {
                    continue;
                }

                if (par.Value is Tree arbol && arbol.growthStage.Value >= 5)
                {
                    var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, x, y, esOpaco);
                    var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, x, y);
                    entidades.Add(new EntidadObservada(
                        IdentidadesObservacion.Terreno(nombreUbicacion, x, y, "arbol"),
                        CategoriaEntidad.Terreno,
                        "árbol",
                        nombreUbicacion,
                        x,
                        y,
                        new[] { "madera", "naturaleza" },
                        Visible: visible,
                        Distancia: distancia));
                }
                else if (par.Value is HoeDirt tierra && tierra.crop is not null)
                {
                    var visible = LineaVision.HayLineaVision(casillaNpc.X, casillaNpc.Y, x, y, esOpaco);
                    var distancia = EntidadObservada.CalcularDistancia(casillaNpc.X, casillaNpc.Y, x, y);
                    entidades.Add(new EntidadObservada(
                        IdentidadesObservacion.Terreno(nombreUbicacion, x, y, "cultivo"),
                        CategoriaEntidad.Terreno,
                        "cultivo",
                        nombreUbicacion,
                        x,
                        y,
                        new[] { "cultivo", "naturaleza" },
                        Visible: visible,
                        Distancia: distancia));
                }
            }
        }

        private static bool EsOrillaDeAgua(WaterTiles.WaterTileData[,] matriz, int ancho, int alto, int x, int y)
            => x == 0 || y == 0 || x == ancho - 1 || y == alto - 1
               || !matriz[x - 1, y].isWater || !matriz[x + 1, y].isWater
               || !matriz[x, y - 1].isWater || !matriz[x, y + 1].isWater;

        private static (int Ancho, int Alto) ObtenerDimensionesMapa(GameLocation ubicacion)
        {
            if (ubicacion.Map is { Layers.Count: > 0 } mapa)
            {
                return (mapa.Layers[0].LayerWidth, mapa.Layers[0].LayerHeight);
            }

            return (0, 0);
        }

        private static bool EsCasillaOpaca(GameLocation ubicacion, int anchoMapa, int altoMapa, int x, int y)
        {
            if (x < 0 || y < 0 || x >= anchoMapa || y >= altoMapa)
            {
                return true;
            }

            if (ubicacion.hasTileAt(x, y, "Buildings")
                && ubicacion.doesTileHaveProperty(x, y, "Passable", "Buildings") is null)
            {
                return true;
            }

            foreach (Building edificio in ubicacion.buildings)
            {
                if (!edificio.isMoving && edificio.occupiesTile(x, y))
                {
                    return true;
                }
            }

            var clave = new Vector2(x, y);
            if (ubicacion.Objects.TryGetValue(clave, out var obj) && obj is not null && obj.bigCraftable.Value)
            {
                return true;
            }

            if (ubicacion.terrainFeatures.TryGetValue(clave, out var rasgo)
                && rasgo is Tree arbol
                && arbol.growthStage.Value >= 5)
            {
                return true;
            }

            return false;
        }

        private static IReadOnlyList<string> EtiquetasDeObjeto(StardewValley.Object obj)
        {
            var etiquetas = new List<string>(3) { "objeto" };
            if (obj.bigCraftable.Value && !obj.isLamp.Value)
            {
                etiquetas.Add("maquinaria");
            }

            if (obj.lightSource is not null || obj.isLamp.Value || obj is Torch)
            {
                etiquetas.Add("luz");
            }

            if (obj.Category == StardewValley.Object.flowersCategory)
            {
                etiquetas.Add("color");
            }

            if (obj is Fence || obj is Chest || obj.QualifiedItemId == StardewValley.Object.woodQID)
            {
                etiquetas.Add("madera");
            }

            return etiquetas;
        }

        private static IReadOnlyList<string> EtiquetasDeMueble(Furniture mueble)
        {
            var etiquetas = new List<string>(3) { "mueble", "madera" };
            if (mueble.IsOn && (mueble.furniture_type.Value == 14 || mueble.furniture_type.Value == 16))
            {
                etiquetas.Add("luz");
            }

            if (mueble.GetSeatCapacity() > 0)
            {
                etiquetas.Add("asiento");
            }

            return etiquetas;
        }

        private static string ResolverClima(GameLocation ubicacion)
        {
            if (ubicacion.IsGreenRainingHere())
            {
                return "lluvia_verde";
            }

            if (ubicacion.IsLightningHere())
            {
                return "tormenta";
            }

            if (ubicacion.IsRainingHere())
            {
                return "lluvia";
            }

            if (ubicacion.IsSnowingHere())
            {
                return "nieve";
            }

            if (ubicacion.IsDebrisWeatherHere())
            {
                return "viento";
            }

            return "soleado";
        }

        private static string NormalizarEtiqueta(string? valor, string respaldo)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return respaldo;
            }

            return valor.Trim().ToLowerInvariant().Replace(' ', '_');
        }
    }
}
