param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'maps4.csproj'
$programPath = Join-Path $repoRoot 'Program.cs'
$appSettingsPath = Join-Path $repoRoot 'appsettings.json'
$storagePath = Join-Path $repoRoot 'Repositorios\Implementacion\AzureBlobInmuebleFotoStorage.cs'

foreach ($path in @($projectPath, $programPath, $appSettingsPath)) {
    if (-not (Test-Path $path)) {
        throw "Required file was not found: $path"
    }
}

if (Test-Path $storagePath) {
    throw 'AzureBlobInmuebleFotoStorage.cs already exists. No files were changed.'
}

$project = Get-Content -Raw -LiteralPath $projectPath
$program = Get-Content -Raw -LiteralPath $programPath
$appSettings = Get-Content -Raw -LiteralPath $appSettingsPath

$projectOld = '    <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.2" />'
$projectNew = @'
    <PackageReference Include="Azure.Storage.Blobs" Version="12.29.2" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="7.0.2" />
'@

$programOld = 'builder.Services.AddSingleton<IInmuebleFotoStorage, LocalInmuebleFotoStorage>();'
$programNew = @'
string imageStorageProvider = builder.Configuration["RSMaps:ImageStorageProvider"]?.Trim() ?? "Local";
if (imageStorageProvider.Equals("AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInmuebleFotoStorage, AzureBlobInmuebleFotoStorage>();
}
else if (imageStorageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IInmuebleFotoStorage, LocalInmuebleFotoStorage>();
}
else
{
    throw new InvalidOperationException($"Proveedor de imagenes RSMaps no soportado: {imageStorageProvider}");
}
'@

$appSettingsOld = '    "PublicAssetBaseUrl": "https://rsmap.azurewebsites.net"'
$appSettingsNew = @'
    "PublicAssetBaseUrl": "https://rsmap.azurewebsites.net",
    "ImageStorageProvider": "Local",
    "ImageStorageContainer": "rsmap-images"
'@

if (-not $project.Contains($projectOld)) {
    throw 'maps4.csproj package anchor was not found. No files were changed.'
}
if (-not $program.Contains($programOld)) {
    throw 'Program.cs storage registration anchor was not found. No files were changed.'
}
if (-not $appSettings.Contains($appSettingsOld)) {
    throw 'appsettings.json RSMaps anchor was not found. No files were changed.'
}

$projectUpdated = $project.Replace($projectOld, $projectNew.TrimEnd())
$programUpdated = $program.Replace($programOld, $programNew.TrimEnd())
$appSettingsUpdated = $appSettings.Replace($appSettingsOld, $appSettingsNew.TrimEnd())

$storageContent = @'
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
'@

Set-Content -LiteralPath $projectPath -Value $projectUpdated -Encoding UTF8
Set-Content -LiteralPath $programPath -Value $programUpdated -Encoding UTF8
Set-Content -LiteralPath $appSettingsPath -Value $appSettingsUpdated -Encoding UTF8
Set-Content -LiteralPath $storagePath -Value $storageContent -Encoding UTF8

Write-Host 'Azure Blob photo storage patch applied.'
Write-Host 'Provider remains Local by default; production is not switched yet.'
Write-Host 'Next: run dotnet build .\maps4.csproj and inspect git diff.'
