namespace CorazonPelican.Tests;

/// <summary>Rutas del repositorio desde los tests (que corren en <c>bin/</c>).</summary>
internal static class Repo
{
    public static readonly string Raiz = Buscar();

    public static string Ruta(params string[] partes) => Path.Combine(new[] { Raiz }.Concat(partes).ToArray());

    private static string Buscar()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CorazonPelican.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("No se encuentra la raíz del repositorio por encima de " + AppContext.BaseDirectory);
    }
}
