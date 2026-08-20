using maps4.Models;
using maps4.Repositorios.Contrato;

namespace maps4.Repositorios.Implementacion
{
    public class LocalInmuebleFotoStorage : IInmuebleFotoStorage
    {
        private readonly string _root;

        public LocalInmuebleFotoStorage(IWebHostEnvironment environment, IConfiguration configuration)
        {
            string? configuredRoot = configuration["RSMaps:ImageStorageRoot"];
            _root = string.IsNullOrWhiteSpace(configuredRoot)
                ? Path.Combine(environment.ContentRootPath, "App_Data", "RSMapsImages")
                : Path.GetFullPath(configuredRoot);

            Directory.CreateDirectory(_root);
        }

        public async Task<FotoAlmacenada> GuardarAsync(int idInmueble, IFormFile archivo, CancellationToken cancellationToken = default)
        {
            if (idInmueble <= 0)
                throw new ArgumentOutOfRangeException(nameof(idInmueble));
            if (archivo == null || archivo.Length <= 0)
                throw new InvalidOperationException("La imagen está vacía.");

            string extension = archivo.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => throw new InvalidOperationException("Formato de imagen no permitido.")
            };

            string folder = Path.Combine(_root, idInmueble.ToString());
            Directory.CreateDirectory(folder);

            string fileName = $"{Guid.NewGuid():N}{extension}";
            string fullPath = Path.Combine(folder, fileName);

            await using (FileStream stream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await archivo.CopyToAsync(stream, cancellationToken);
            }

            return new FotoAlmacenada
            {
                ClaveAlmacenamiento = $"{idInmueble}/{fileName}",
                NombreOriginal = Path.GetFileName(archivo.FileName),
                MimeType = archivo.ContentType.ToLowerInvariant(),
                Bytes = archivo.Length
            };
        }

        public Task<Stream?> AbrirLecturaAsync(string claveAlmacenamiento, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolverRutaSegura(claveAlmacenamiento);
            if (!File.Exists(fullPath))
                return Task.FromResult<Stream?>(null);

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            return Task.FromResult<Stream?>(stream);
        }

        public Task EliminarAsync(string claveAlmacenamiento, CancellationToken cancellationToken = default)
        {
            string fullPath = ResolverRutaSegura(claveAlmacenamiento);
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            return Task.CompletedTask;
        }

        private string ResolverRutaSegura(string claveAlmacenamiento)
        {
            if (string.IsNullOrWhiteSpace(claveAlmacenamiento))
                throw new InvalidOperationException("Clave de almacenamiento inválida.");

            string relative = claveAlmacenamiento.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(_root, relative));
            string rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Ruta de almacenamiento inválida.");

            return fullPath;
        }
    }
}
