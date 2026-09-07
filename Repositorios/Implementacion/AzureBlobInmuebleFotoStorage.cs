using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using maps4.Models;
using maps4.Repositorios.Contrato;

namespace maps4.Repositorios.Implementacion
{
    public sealed class AzureBlobInmuebleFotoStorage : IInmuebleFotoStorage
    {
        private readonly BlobContainerClient _container;

        public AzureBlobInmuebleFotoStorage(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("RSMapsImages") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Falta ConnectionStrings:RSMapsImages para Azure Blob Storage.");

            string containerName = configuration["RSMaps:ImageStorageContainer"]?.Trim() ?? "rsmap-images";
            if (string.IsNullOrWhiteSpace(containerName))
                throw new InvalidOperationException("RSMaps:ImageStorageContainer no puede estar vacio.");

            _container = new BlobContainerClient(connectionString, containerName);
        }

        public async Task<FotoAlmacenada> GuardarAsync(
            int idInmueble,
            IFormFile archivo,
            CancellationToken cancellationToken = default)
        {
            if (idInmueble <= 0)
                throw new ArgumentOutOfRangeException(nameof(idInmueble));
            if (archivo == null || archivo.Length <= 0)
                throw new InvalidOperationException("La imagen esta vacia.");

            string mimeType = archivo.ContentType.ToLowerInvariant();
            string extension = mimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => throw new InvalidOperationException("Formato de imagen no permitido.")
            };

            await _container.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            string key = $"{idInmueble}/{Guid.NewGuid():N}{extension}";
            BlobClient blob = _container.GetBlobClient(key);

            await using Stream stream = archivo.OpenReadStream();
            await blob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = mimeType
                    }
                },
                cancellationToken);

            return new FotoAlmacenada
            {
                ClaveAlmacenamiento = key,
                NombreOriginal = Path.GetFileName(archivo.FileName),
                MimeType = mimeType,
                Bytes = archivo.Length
            };
        }

        public async Task<Stream?> AbrirLecturaAsync(
            string claveAlmacenamiento,
            CancellationToken cancellationToken = default)
        {
            string key = NormalizarClave(claveAlmacenamiento);
            BlobClient blob = _container.GetBlobClient(key);

            try
            {
                Response<BlobDownloadStreamingResult> download = await blob.DownloadStreamingAsync(
                    cancellationToken: cancellationToken);
                return download.Value.Content;
            }
            catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
            {
                return null;
            }
        }

        public async Task EliminarAsync(
            string claveAlmacenamiento,
            CancellationToken cancellationToken = default)
        {
            string key = NormalizarClave(claveAlmacenamiento);
            BlobClient blob = _container.GetBlobClient(key);
            await blob.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);
        }

        private static string NormalizarClave(string claveAlmacenamiento)
        {
            if (string.IsNullOrWhiteSpace(claveAlmacenamiento))
                throw new InvalidOperationException("Clave de almacenamiento invalida.");

            string key = claveAlmacenamiento.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("Clave de almacenamiento invalida.");

            return key;
        }
    }
}
