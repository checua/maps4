using maps4.Models;
using maps4.Repositorios.Contrato;
using System.Globalization;
using System.Text;

namespace maps4.Services
{
    public class RadarMatchingService : IRadarMatchingService
    {
        private const int PuntuacionMinimaCandidato = 55;
        private const double ExcesoMaximoPrecio = 0.20;

        private readonly IInventarioRepository _inventarioRepository;

        public RadarMatchingService(IInventarioRepository inventarioRepository)
        {
            _inventarioRepository = inventarioRepository;
        }

        public async Task<RadarMatchingResponse> CompararAsync(string correo, RadarMatchingRequest solicitud)
        {
            var inventario = await _inventarioRepository.ListarAutorizadosAsync(correo);

            var disponibles = inventario
                .Where(x => string.IsNullOrWhiteSpace(x.EstadoCodigo)
                    || string.Equals(x.EstadoCodigo, "PUBLICADO", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var resultados = new List<RadarMatchingResultado>();

            foreach (var inmueble in disponibles)
            {
                if (OperacionIncompatible(solicitud.Operacion, inmueble))
                    continue;

                if (TipoIncompatible(solicitud.TiposPropiedad, inmueble.TipoNombre))
                    continue;

                // Un inmueble puede ser parecido por tipo/operación, pero no debe
                // presentarse como oportunidad si viola de forma importante un
                // requisito duro que sí conocemos (por ejemplo, presupuesto).
                if (!EsCandidatoViable(solicitud, inmueble))
                    continue;

                var resultado = Evaluar(solicitud, inmueble);
                if (resultado.Puntuacion >= PuntuacionMinimaCandidato)
                    resultados.Add(resultado);
            }

            int maxResultados = Math.Clamp(solicitud.MaxResultados, 1, 10);

            return new RadarMatchingResponse
            {
                TotalInventarioEvaluado = inventario.Count,
                TotalCandidatos = resultados.Count,
                Resultados = resultados
                    .OrderByDescending(x => x.Puntuacion)
                    .ThenBy(x => x.Precio ?? double.MaxValue)
                    .Take(maxResultados)
                    .ToList()
            };
        }

        private static bool EsCandidatoViable(
            RadarMatchingRequest solicitud,
            InventarioInmuebleViewModel inmueble)
        {
            if (solicitud.PrecioMaximo.HasValue && inmueble.Precio.HasValue && inmueble.Precio.Value > 0)
            {
                double maximo = (double)solicitud.PrecioMaximo.Value;
                double exceso = (inmueble.Precio.Value - maximo) / Math.Max(maximo, 1);

                // Hasta 20% arriba puede conservarse únicamente como alternativa
                // aproximada. Más que eso ya no es una oportunidad razonable.
                if (exceso > ExcesoMaximoPrecio)
                    return false;
            }

            if (solicitud.RecamarasMin.HasValue && inmueble.Recamaras.HasValue)
            {
                if (inmueble.Recamaras.Value < solicitud.RecamarasMin.Value - 1)
                    return false;
            }

            if (solicitud.BanosMin.HasValue && inmueble.BanosCompletos.HasValue)
            {
                if (inmueble.BanosCompletos.Value < solicitud.BanosMin.Value - 1)
                    return false;
            }

            if (solicitud.TerrenoMinM2.HasValue && inmueble.Terreno.HasValue)
            {
                if (inmueble.Terreno.Value < (double)solicitud.TerrenoMinM2.Value * 0.80)
                    return false;
            }

            if (solicitud.ConstruccionMinM2.HasValue && inmueble.Construccion.HasValue)
            {
                if (inmueble.Construccion.Value < (double)solicitud.ConstruccionMinM2.Value * 0.80)
                    return false;
            }

            if (solicitud.UnaPlanta == true && inmueble.Niveles.HasValue && inmueble.Niveles.Value > 1)
                return false;

            return true;
        }

        private static RadarMatchingResultado Evaluar(
            RadarMatchingRequest solicitud,
            InventarioInmuebleViewModel inmueble)
        {
            double puntos = 0;
            double pesoTotal = 0;
            var coincidencias = new List<string>();
            var diferencias = new List<string>();

            void Agregar(double peso, double valor, string? coincide = null, string? diferencia = null)
            {
                pesoTotal += peso;
                puntos += peso * Math.Clamp(valor, 0, 1);

                if (valor >= 0.8 && !string.IsNullOrWhiteSpace(coincide))
                    coincidencias.Add(coincide);
                else if (valor < 0.8 && !string.IsNullOrWhiteSpace(diferencia))
                    diferencias.Add(diferencia);
            }

            if (!string.IsNullOrWhiteSpace(solicitud.Operacion))
            {
                var valor = CoincidenciaOperacion(solicitud.Operacion, inmueble);
                Agregar(20, valor,
                    $"Operación compatible: {solicitud.Operacion}",
                    "Operación no confirmada en los datos del inmueble");
            }

            if (solicitud.TiposPropiedad.Count > 0)
            {
                var valor = CoincidenciaTipo(solicitud.TiposPropiedad, inmueble.TipoNombre);
                Agregar(20, valor,
                    $"Tipo compatible: {inmueble.TipoNombre}",
                    $"Tipo distinto o no confirmado: {inmueble.TipoNombre ?? "sin dato"}");
            }

            if (solicitud.Zonas.Count > 0)
            {
                var valor = CoincidenciaZona(solicitud.Zonas, inmueble);
                string zonaCandidato = DescribirZonaCandidato(inmueble);

                Agregar(25, valor,
                    $"Zona compatible: {zonaCandidato}",
                    $"Zona no confirmada: {zonaCandidato}");
            }

            if (solicitud.PrecioMinimo.HasValue || solicitud.PrecioMaximo.HasValue)
            {
                var valor = CoincidenciaPrecio(solicitud, inmueble.Precio);
                string precioTexto = inmueble.Precio.HasValue && inmueble.Precio.Value > 0
                    ? inmueble.Precio.Value.ToString("C0", CultureInfo.GetCultureInfo("es-MX"))
                    : "sin precio";

                Agregar(20, valor,
                    $"Precio compatible: {precioTexto}",
                    $"Precio fuera de rango o no disponible: {precioTexto}");
            }

            if (solicitud.RecamarasMin.HasValue)
            {
                var valor = CoincidenciaMinimo(solicitud.RecamarasMin.Value, inmueble.Recamaras);
                Agregar(6, valor,
                    $"Recámaras: {inmueble.Recamaras}",
                    $"Recámaras: {(inmueble.Recamaras?.ToString() ?? "sin dato")}");
            }

            if (solicitud.BanosMin.HasValue)
            {
                var valor = CoincidenciaMinimo(solicitud.BanosMin.Value, inmueble.BanosCompletos);
                Agregar(4, valor,
                    $"Baños: {inmueble.BanosCompletos}",
                    $"Baños: {(inmueble.BanosCompletos?.ToString() ?? "sin dato")}");
            }

            if (solicitud.CocheraMinAutos.HasValue)
            {
                var valor = CoincidenciaMinimo(solicitud.CocheraMinAutos.Value, inmueble.Estacionamientos);
                Agregar(2, valor,
                    $"Estacionamientos: {inmueble.Estacionamientos}",
                    $"Estacionamientos: {(inmueble.Estacionamientos?.ToString() ?? "sin dato")}");
            }

            if (solicitud.TerrenoMinM2.HasValue)
            {
                var valor = CoincidenciaMinimo((double)solicitud.TerrenoMinM2.Value, inmueble.Terreno);
                Agregar(1, valor,
                    $"Terreno: {inmueble.Terreno:0.#} m²",
                    $"Terreno: {(inmueble.Terreno.HasValue ? $"{inmueble.Terreno:0.#} m²" : "sin dato")}");
            }

            if (solicitud.ConstruccionMinM2.HasValue)
            {
                var valor = CoincidenciaMinimo((double)solicitud.ConstruccionMinM2.Value, inmueble.Construccion);
                Agregar(1, valor,
                    $"Construcción: {inmueble.Construccion:0.#} m²",
                    $"Construcción: {(inmueble.Construccion.HasValue ? $"{inmueble.Construccion:0.#} m²" : "sin dato")}");
            }

            if (solicitud.AceptaMascotas.HasValue)
            {
                var tiene = TieneAmenidad(inmueble, "MASCOTAS");
                var valor = solicitud.AceptaMascotas.Value == tiene ? 1 : 0;
                Agregar(1, valor,
                    solicitud.AceptaMascotas.Value ? "Acepta mascotas" : "No requiere mascotas",
                    solicitud.AceptaMascotas.Value ? "No está confirmado que acepte mascotas" : null);
            }

            if (solicitud.Amueblado.HasValue)
            {
                var tiene = TieneAmenidad(inmueble, "AMUEBLADO");
                var valor = solicitud.Amueblado.Value == tiene ? 1 : 0;
                Agregar(1, valor,
                    solicitud.Amueblado.Value ? "Amueblado" : "Sin requisito de amueblado",
                    solicitud.Amueblado.Value ? "No está registrado como amueblado" : "Está registrado como amueblado");
            }

            if (solicitud.UnaPlanta.HasValue)
            {
                double valor;
                if (!inmueble.Niveles.HasValue)
                    valor = 0.10;
                else
                    valor = solicitud.UnaPlanta.Value == (inmueble.Niveles.Value <= 1) ? 1 : 0;

                Agregar(1, valor,
                    solicitud.UnaPlanta.Value ? "Una planta" : null,
                    solicitud.UnaPlanta.Value ? "No está confirmado como una planta" : null);
            }

            if (solicitud.CasetaVigilancia.HasValue && solicitud.CasetaVigilancia.Value)
            {
                var tiene = TieneAmenidad(inmueble, "VIGILANCIA_24H")
                    || TieneAmenidad(inmueble, "ACCESO_CONTROLADO");
                Agregar(1, tiene ? 1 : 0,
                    "Seguridad/acceso controlado",
                    "No está registrada vigilancia o acceso controlado");
            }

            int puntuacion = pesoTotal <= 0
                ? 0
                : (int)Math.Round((puntos / pesoTotal) * 100, MidpointRounding.AwayFromZero);

            return new RadarMatchingResultado
            {
                IdInmueble = inmueble.IdInmueble,
                Puntuacion = puntuacion,
                Nivel = puntuacion >= 85 ? "ALTA"
                    : puntuacion >= 70 ? "MEDIA"
                    : "APROXIMADA",
                Direccion = DireccionUtil(inmueble.Direccion) ? inmueble.Direccion : null,
                TipoNombre = inmueble.TipoNombre,
                Precio = inmueble.Precio,
                Recamaras = inmueble.Recamaras,
                BanosCompletos = inmueble.BanosCompletos,
                Estacionamientos = inmueble.Estacionamientos,
                Terreno = inmueble.Terreno,
                Construccion = inmueble.Construccion,
                Link = inmueble.Link,
                Coincidencias = coincidencias,
                Diferencias = diferencias
            };
        }

        private static bool OperacionIncompatible(string? operacion, InventarioInmuebleViewModel inmueble)
        {
            if (string.IsNullOrWhiteSpace(operacion))
                return false;

            string esperada = Normalizar(operacion);
            string texto = Normalizar($"{inmueble.TipoNombre} {inmueble.Observaciones}");

            bool pideRenta = ContieneAlguno(esperada, "RENTA", "ALQUILER", "ARRENDAMIENTO");
            bool pideVenta = ContieneAlguno(esperada, "VENTA", "COMPRA", "COMPRAR");
            bool esRenta = ContieneAlguno(texto, "RENTA", "ALQUILER", "ARRENDAMIENTO");
            bool esVenta = ContieneAlguno(texto, "VENTA", "COMPRA");

            return (pideRenta && esVenta && !esRenta)
                || (pideVenta && esRenta && !esVenta);
        }

        private static double CoincidenciaOperacion(string? operacion, InventarioInmuebleViewModel inmueble)
        {
            if (string.IsNullOrWhiteSpace(operacion))
                return 1;

            string esperada = Normalizar(operacion);
            string texto = Normalizar($"{inmueble.TipoNombre} {inmueble.Observaciones}");

            if (ContieneAlguno(esperada, "RENTA", "ALQUILER", "ARRENDAMIENTO"))
                return ContieneAlguno(texto, "RENTA", "ALQUILER", "ARRENDAMIENTO") ? 1 : 0.45;

            if (ContieneAlguno(esperada, "VENTA", "COMPRA", "COMPRAR"))
                return ContieneAlguno(texto, "VENTA", "COMPRA") ? 1 : 0.45;

            return 0.5;
        }

        private static bool TipoIncompatible(List<string> tipos, string? tipoNombre)
        {
            if (tipos.Count == 0 || string.IsNullOrWhiteSpace(tipoNombre))
                return false;

            string candidato = TipoCanonico(tipoNombre);
            if (string.IsNullOrWhiteSpace(candidato))
                return false;

            var solicitados = tipos
                .Select(TipoCanonico)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            return solicitados.Count > 0 && !solicitados.Contains(candidato);
        }

        private static double CoincidenciaTipo(List<string> tipos, string? tipoNombre)
        {
            if (tipos.Count == 0)
                return 1;

            if (string.IsNullOrWhiteSpace(tipoNombre))
                return 0.20;

            string candidato = TipoCanonico(tipoNombre);
            var solicitados = tipos.Select(TipoCanonico).Where(x => !string.IsNullOrWhiteSpace(x));

            return solicitados.Contains(candidato) ? 1 : 0;
        }

        private static string TipoCanonico(string texto)
        {
            string n = Normalizar(texto);
            if (ContieneAlguno(n, "CASA", "RESIDENCIA", "VIVIENDA")) return "CASA";
            if (ContieneAlguno(n, "DEPARTAMENTO", "DEPTO", "APARTAMENTO")) return "DEPARTAMENTO";
            if (ContieneAlguno(n, "TERRENO", "LOTE")) return "TERRENO";
            if (ContieneAlguno(n, "LOCAL", "COMERCIAL")) return "LOCAL";
            if (ContieneAlguno(n, "BODEGA", "NAVE")) return "BODEGA";
            if (ContieneAlguno(n, "OFICINA", "DESPACHO")) return "OFICINA";
            if (ContieneAlguno(n, "RANCHO", "QUINTA")) return "RANCHO";
            return string.Empty;
        }

        private static double CoincidenciaZona(List<string> zonas, InventarioInmuebleViewModel inmueble)
        {
            string estructuradas = Normalizar($"{inmueble.ZonaPrincipalNombre} {inmueble.ZonasCsv}");
            string direccion = DireccionUtil(inmueble.Direccion) ? inmueble.Direccion! : string.Empty;
            string respaldo = Normalizar($"{direccion} {inmueble.Observaciones}");

            if (string.IsNullOrWhiteSpace(estructuradas) && string.IsNullOrWhiteSpace(respaldo))
                return 0;

            double mejor = 0;

            foreach (var zona in zonas)
            {
                string z = Normalizar(zona);
                if (string.IsNullOrWhiteSpace(z))
                    continue;

                if (!string.IsNullOrWhiteSpace(estructuradas))
                {
                    if (estructuradas.Contains(z, StringComparison.OrdinalIgnoreCase))
                        return 1;

                    var tokensEstructurados = TokensZona(z).ToList();
                    if (tokensEstructurados.Count > 0)
                    {
                        int encontrados = tokensEstructurados.Count(t =>
                            estructuradas.Contains(t, StringComparison.OrdinalIgnoreCase));
                        mejor = Math.Max(mejor, (double)encontrados / tokensEstructurados.Count);
                    }
                }

                if (!string.IsNullOrWhiteSpace(respaldo))
                {
                    if (respaldo.Contains(z, StringComparison.OrdinalIgnoreCase))
                        mejor = Math.Max(mejor, 0.85);

                    var tokensRespaldo = TokensZona(z).ToList();
                    if (tokensRespaldo.Count > 0)
                    {
                        int encontrados = tokensRespaldo.Count(t =>
                            respaldo.Contains(t, StringComparison.OrdinalIgnoreCase));
                        mejor = Math.Max(
                            mejor,
                            ((double)encontrados / tokensRespaldo.Count) * 0.75);
                    }
                }
            }

            return mejor;
        }

        private static string DescribirZonaCandidato(InventarioInmuebleViewModel inmueble)
        {
            if (!string.IsNullOrWhiteSpace(inmueble.ZonaPrincipalNombre))
                return inmueble.ZonaPrincipalNombre!;

            if (!string.IsNullOrWhiteSpace(inmueble.ZonasCsv))
                return inmueble.ZonasCsv!;

            if (DireccionUtil(inmueble.Direccion))
                return inmueble.Direccion!;

            return "sin zona estructurada ni dirección útil";
        }

        private static IEnumerable<string> TokensZona(string texto)
        {
            var ignorar = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ZONA", "FRACC", "FRACCIONAMIENTO", "COL", "COLONIA", "CERCA",
                "ALREDEDORES", "ALREDEDOR", "DEL", "DE", "LA", "EL", "LOS", "LAS",
                "POR", "BLVD", "BOULEVARD"
            };

            return texto
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => x.Length >= 4 && !ignorar.Contains(x))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static double CoincidenciaPrecio(RadarMatchingRequest solicitud, double? precio)
        {
            if (!precio.HasValue || precio.Value <= 0)
                return 0.10;

            double p = precio.Value;
            double? min = solicitud.PrecioMinimo.HasValue ? (double)solicitud.PrecioMinimo.Value : null;
            double? max = solicitud.PrecioMaximo.HasValue ? (double)solicitud.PrecioMaximo.Value : null;

            if ((!min.HasValue || p >= min.Value) && (!max.HasValue || p <= max.Value))
                return 1;

            if (max.HasValue && p > max.Value)
            {
                double exceso = (p - max.Value) / Math.Max(max.Value, 1);
                if (exceso <= 0.05) return 0.85;
                if (exceso <= 0.10) return 0.70;
                if (exceso <= 0.20) return 0.45;
                return 0;
            }

            if (min.HasValue && p < min.Value)
            {
                double proporcion = p / Math.Max(min.Value, 1);
                if (proporcion >= 0.90) return 0.90;
                if (proporcion >= 0.80) return 0.75;
                return 0.50;
            }

            return 0;
        }

        private static double CoincidenciaMinimo(int minimo, int? valor)
        {
            if (!valor.HasValue)
                return 0.10;
            if (valor.Value >= minimo)
                return 1;
            if (valor.Value == minimo - 1)
                return 0.5;
            return 0;
        }

        private static double CoincidenciaMinimo(double minimo, double? valor)
        {
            if (!valor.HasValue)
                return 0.10;
            if (valor.Value >= minimo)
                return 1;
            if (valor.Value >= minimo * 0.9)
                return 0.7;
            if (valor.Value >= minimo * 0.8)
                return 0.4;
            return 0;
        }

        private static bool DireccionUtil(string? direccion)
        {
            if (string.IsNullOrWhiteSpace(direccion))
                return false;

            string n = Normalizar(direccion);
            return n is not ("DIRECCION" or "DOMICILIO" or "UBICACION" or "SIN DIRECCION");
        }

        private static bool TieneAmenidad(InventarioInmuebleViewModel inmueble, string codigo)
        {
            if (string.IsNullOrWhiteSpace(inmueble.AmenidadesCsv))
                return false;

            return inmueble.AmenidadesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(x, codigo, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContieneAlguno(string texto, params string[] terminos) =>
            terminos.Any(t => texto.Contains(t, StringComparison.OrdinalIgnoreCase));

        private static string Normalizar(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            string descompuesto = texto.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);

            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }

            return string.Join(' ', sb.ToString()
                .Normalize(NormalizationForm.FormC)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }
}
