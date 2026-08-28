using maps4.Models;
using maps4.Repositorios.Contrato;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace maps4.Controllers
{
    [Authorize]
    public class ZonaAdminController : Controller
    {
        private const double MargenCoberturaKm = 12d;

        private readonly IZonaRepository _zonaRepository;
        private readonly IInventarioRepository _inventarioRepository;
        private readonly ILogger<ZonaAdminController> _logger;

        public ZonaAdminController(
            IZonaRepository zonaRepository,
            IInventarioRepository inventarioRepository,
            ILogger<ZonaAdminController> logger)
        {
            _zonaRepository = zonaRepository;
            _inventarioRepository = inventarioRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Challenge();

            try
            {
                List<ZonaResumenViewModel> zonas = await _zonaRepository.ListarAsync(correo);
                ZonaCoberturaActualViewModel? coberturaActual = await _zonaRepository.ObtenerCoberturaActualAsync(correo);
                List<InventarioInmuebleViewModel> inventario = await _inventarioRepository.ListarAutorizadosAsync(correo);

                ZonaAdminIndexViewModel modelo = new()
                {
                    Zonas = zonas,
                    Inmuebles = inventario
                        .Where(x => x.TieneUbicacion)
                        .Select(x =>
                        {
                            bool sinZona = !x.TieneZona;
                            bool dentroAreaActual = coberturaActual == null || EstaDentroAreaActual(
                                (double)x.Lat!.Value,
                                (double)x.Lng!.Value,
                                coberturaActual);

                            return new ZonaInmueblePinViewModel
                            {
                                IdInmueble = x.IdInmueble,
                                Lat = x.Lat.Value,
                                Lng = x.Lng.Value,
                                Tipo = x.TipoNombre,
                                Direccion = x.Direccion,
                                Precio = x.Precio,
                                CoberturaPendiente = sinZona && dentroAreaActual,
                                FueraAreaActual = sinZona && !dentroAreaActual
                            };
                        })
                        .ToList()
                };

                return View(modelo);
            }
            catch (SqlException ex) when (ex.Number is 53840 or 53841)
            {
                return Forbid();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Obtener(int id)
        {
            string? correo = User.Identity?.Name;
            if (id <= 0 || string.IsNullOrWhiteSpace(correo))
                return BadRequest(new { success = false, message = "Solicitud invalida." });

            try
            {
                ZonaEdicionViewModel? zona = await _zonaRepository.ObtenerAsync(correo, id);
                if (zona == null)
                    return NotFound(new { success = false, message = "Zona no encontrada." });

                return Ok(new { success = true, zona });
            }
            catch (SqlException ex) when (ex.Number is 53853 or 53854)
            {
                return Forbid();
            }
            catch (SqlException ex) when (ex.Number == 53855)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Guardar([FromBody] ZonaGuardarRequest request)
        {
            string? correo = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(correo))
                return Unauthorized(new { success = false, message = "Se requiere iniciar sesion." });

            if (request == null || string.IsNullOrWhiteSpace(request.Nombre) || request.Vertices == null || request.Vertices.Count < 3)
                return BadRequest(new { success = false, message = "Indica un nombre y dibuja al menos tres vertices." });

            request.Codigo = NormalizarCodigo(string.IsNullOrWhiteSpace(request.Codigo) ? request.Nombre : request.Codigo);
            if (request.Prioridad < 0 || request.Prioridad > 10000)
                return BadRequest(new { success = false, message = "La prioridad no es valida." });

            if (!string.IsNullOrWhiteSpace(request.ColorHex) &&
                !System.Text.RegularExpressions.Regex.IsMatch(request.ColorHex, "^#[0-9A-Fa-f]{6}$"))
                return BadRequest(new { success = false, message = "El color no es valido." });

            if (request.Vertices.Any(v => v.Lat < -90 || v.Lat > 90 || v.Lng < -180 || v.Lng > 180))
                return BadRequest(new { success = false, message = "Hay vertices fuera del rango geografico valido." });

            request.Aliases = (request.Aliases ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (request.Aliases.Count > 50)
                return BadRequest(new { success = false, message = "Una zona puede tener hasta 50 alias en esta etapa." });

            if (request.Aliases.Any(x => x.Length < 2 || x.Length > 150))
                return BadRequest(new { success = false, message = "Cada alias debe tener entre 2 y 150 caracteres." });

            try
            {
                int idZona = await _zonaRepository.GuardarAsync(correo, request);
                return Ok(new
                {
                    success = true,
                    idZona,
                    message = "Zona y nombres comunes guardados. Los inmuebles de la cuenta fueron reclasificados automaticamente."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (SqlException ex) when (ex.Number is 53863 or 53864 or 53866)
            {
                return Forbid();
            }
            catch (SqlException ex) when ((ex.Number is >= 53856 and <= 53865) || ex.Number is 53883 or 53884)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No fue posible guardar la zona administrativa.");
                return StatusCode(500, new { success = false, message = "No fue posible guardar la zona." });
            }
        }

        private static bool EstaDentroAreaActual(double lat, double lng, ZonaCoberturaActualViewModel cobertura)
        {
            const double kmPorGradoLat = 111.32d;
            double latCentro = (cobertura.MinLat + cobertura.MaxLat) / 2d;
            double kmPorGradoLng = kmPorGradoLat * Math.Max(0.2d, Math.Cos(latCentro * Math.PI / 180d));

            double margenLat = MargenCoberturaKm / kmPorGradoLat;
            double margenLng = MargenCoberturaKm / kmPorGradoLng;

            return lat >= cobertura.MinLat - margenLat
                && lat <= cobertura.MaxLat + margenLat
                && lng >= cobertura.MinLng - margenLng
                && lng <= cobertura.MaxLng + margenLng;
        }

        private static string NormalizarCodigo(string valor)
        {
            string texto = valor.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
            string sinAcentos = new(texto.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            string codigo = System.Text.RegularExpressions.Regex.Replace(sinAcentos, "[^A-Z0-9]+", "_").Trim('_');
            return codigo.Length <= 60 ? codigo : codigo[..60].TrimEnd('_');
        }
    }
}
