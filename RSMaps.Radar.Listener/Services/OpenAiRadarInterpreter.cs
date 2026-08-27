using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public sealed class OpenAiRadarInterpreter : IRadarInterpreter
{
    private const string Endpoint = "https://api.openai.com/v1/responses";

    private const string Instructions = """
Eres el intérprete semántico de RADAR, una plataforma inmobiliaria.
Tu tarea es convertir mensajes de WhatsApp en solicitudes inmobiliarias estructuradas.

Reglas:
- Un mensaje puede contener cero, una o varias solicitudes independientes. Sepáralas.
- Extrae solamente lo que el solicitante realmente busca. No inventes datos.
- En tiposPropiedad usa únicamente tipos base cuando sean conocidos: Casa, Departamento, Terreno, Local, Bodega, Oficina, Rancho o Edificio.
- Los subtipos o términos constructivos como Dúplex u otros definidos por RADAR Knowledge van en subtiposPropiedad, no sustituyen al tipo base. Si RADAR Knowledge indica un tipo base canónico, inclúyelo en tiposPropiedad.
- Distingue ubicaciones de características. "planta baja", "patio amplio", "cerca", "zonas cercanas", "alrededores", "col.", "fracc." y "privado" por sí solos NO son zonas.
- "fraccionamiento privado" describe el tipo de fraccionamiento y debe representarse como tipoFraccionamiento="Privado". No es una zona y no implica por sí mismo caseta de vigilancia.
- Conserva como zonas los nombres o referencias geográficas útiles, por ejemplo colonias, fraccionamientos con nombre propio, sectores, rumbos, zona sur, CIMA o libramiento cuando funcionen como referencia de ubicación.
- "zonas cercanas", "cerca" o "ese rumbo" son flexibilidad geográfica, no nombres de zona.
- Normaliza importes: "2.3 millones" significa 2300000; "20 mil" significa 20000.
- Si hay un rango de precio, usa precioMinimo y precioMaximo. Si sólo hay un monto o presupuesto sin rango explícito, trátalo como precio máximo y deja precioMinimo en null.
- Si el mensaje contiene una respuesta, oferta o texto citado posterior a la solicitud original, no mezcles los datos de esa oferta con los criterios buscados. Frases como "si aún busca", "tengo opciones" o "cuento con" suelen iniciar una respuesta/oferta.
- "recámara en planta baja" es un requisito y NO debe llenar recamarasMin/recamarasMax salvo que el mensaje también indique explícitamente el total de recámaras buscado.
- "recámara en planta baja" tampoco significa necesariamente que toda la casa sea de una planta.
- En modalidadesPago usa valores canónicos cuando sea posible: Infonavit, Fovissste, Banjercito, Crédito bancario, Crédito hipotecario o Contado. "efectivo" equivale a Contado. Detalles como "Total" o "Conyugal" pueden ir en requisitosAdicionales.
- Si no es una solicitud inmobiliaria, devuelve esSolicitudInmobiliaria=false y solicitudes vacías.
- La confianza debe estar entre 0 y 1.
""";

    private const string Schema = """
{
  "type": "object",
  "properties": {
    "esSolicitudInmobiliaria": { "type": "boolean" },
    "confianza": { "type": "number", "minimum": 0, "maximum": 1 },
    "observaciones": { "type": ["string", "null"] },
    "solicitudes": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "operacion": { "type": ["string", "null"], "enum": ["Venta", "Renta", null] },
          "tiposPropiedad": { "type": "array", "items": { "type": "string" } },
          "subtiposPropiedad": { "type": "array", "items": { "type": "string" } },
          "zonas": { "type": "array", "items": { "type": "string" } },
          "tipoFraccionamiento": { "type": ["string", "null"], "enum": ["Privado", null] },
          "precioMinimo": { "type": ["number", "null"] },
          "precioMaximo": { "type": ["number", "null"] },
          "recamarasMin": { "type": ["integer", "null"] },
          "recamarasMax": { "type": ["integer", "null"] },
          "banosMin": { "type": ["integer", "null"] },
          "banosMax": { "type": ["integer", "null"] },
          "terrenoMinM2": { "type": ["number", "null"] },
          "construccionMinM2": { "type": ["number", "null"] },
          "aceptaMascotas": { "type": ["boolean", "null"] },
          "amueblado": { "type": ["boolean", "null"] },
          "unaPlanta": { "type": ["boolean", "null"] },
          "casetaVigilancia": { "type": ["boolean", "null"] },
          "cocheraMinAutos": { "type": ["integer", "null"] },
          "modalidadesPago": { "type": "array", "items": { "type": "string" } },
          "requisitosAdicionales": { "type": ["string", "null"] }
        },
        "required": [
          "operacion", "tiposPropiedad", "subtiposPropiedad", "zonas", "tipoFraccionamiento",
          "precioMinimo", "precioMaximo", "recamarasMin", "recamarasMax", "banosMin", "banosMax",
          "terrenoMinM2", "construccionMinM2", "aceptaMascotas", "amueblado", "unaPlanta",
          "casetaVigilancia", "cocheraMinAutos", "modalidadesPago", "requisitosAdicionales"
        ],
        "additionalProperties": false
      }
    }
  },
  "required": ["esSolicitudInmobiliaria", "confianza", "observaciones", "solicitudes"],
  "additionalProperties": false
}
""";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiRadarInterpreter(
        string apiKey,
        string model = "gpt-5.4-nano",
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("Se requiere una API key de OpenAI.", nameof(apiKey));

        _apiKey = apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? "gpt-5.4-nano" : model;
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        using var schemaDocument = JsonDocument.Parse(Schema);

        var payload = new
        {
            model = _model,
            store = false,
            instructions = Instructions,
            input = mensaje.TextoOriginal,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "radar_interpretation",
                    strict = true,
                    schema = schemaDocument.RootElement
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI API respondió {(int)response.StatusCode}: {ExtraerMensajeError(body)}");
        }

        using var responseDocument = JsonDocument.Parse(body);
        var root = responseDocument.RootElement;
        var outputText = ExtraerOutputText(root);

        if (string.IsNullOrWhiteSpace(outputText))
            throw new InvalidOperationException("OpenAI no devolvió salida estructurada utilizable.");

        var dto = JsonSerializer.Deserialize<OpenAiInterpretationDto>(
            outputText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto is null)
            throw new InvalidOperationException("No fue posible deserializar la interpretación de OpenAI.");

        var result = new RadarInterpretationResult
        {
            Motor = $"OPENAI:{_model}",
            ConfianzaInterpretacion = dto.Confianza,
            Observaciones = dto.Observaciones
        };

        if (root.TryGetProperty("usage", out var usage))
        {
            result.InputTokens = ObtenerEntero(usage, "input_tokens");
            result.OutputTokens = ObtenerEntero(usage, "output_tokens");
            result.TotalTokens = ObtenerEntero(usage, "total_tokens");
        }

        if (!dto.EsSolicitudInmobiliaria)
            return result;

        foreach (var item in dto.Solicitudes)
        {
            result.Solicitudes.Add(new SolicitudInmobiliaria
            {
                ChatOrigen = mensaje.ChatOrigen,
                Autor = mensaje.Autor,
                Telefono = mensaje.Telefono,
                MessageId = mensaje.MessageId,
                MensajeOriginal = mensaje.TextoOriginal,
                DetectadoEn = mensaje.DetectadoEn,
                Operacion = item.Operacion,
                TiposPropiedad = item.TiposPropiedad ?? [],
                SubtiposPropiedad = item.SubtiposPropiedad ?? [],
                Zonas = item.Zonas ?? [],
                TipoFraccionamiento = item.TipoFraccionamiento,
                PrecioMinimo = item.PrecioMinimo,
                PrecioMaximo = item.PrecioMaximo,
                RecamarasMin = item.RecamarasMin,
                RecamarasMax = item.RecamarasMax,
                BanosMin = item.BanosMin,
                BanosMax = item.BanosMax,
                TerrenoMinM2 = item.TerrenoMinM2,
                ConstruccionMinM2 = item.ConstruccionMinM2,
                AceptaMascotas = item.AceptaMascotas,
                Amueblado = item.Amueblado,
                UnaPlanta = item.UnaPlanta,
                CasetaVigilancia = item.CasetaVigilancia,
                CocheraMinAutos = item.CocheraMinAutos,
                ModalidadesPago = item.ModalidadesPago ?? [],
                RequisitosAdicionales = item.RequisitosAdicionales
            });
        }

        return result;
    }

    private static string ExtraerOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return string.Empty;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static int? ObtenerEntero(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static string ExtraerMensajeError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "error no especificado";
            }
        }
        catch
        {
            // Si no es JSON, devolvemos un mensaje genérico sin exponer contenido sensible.
        }

        return "error no especificado";
    }

    private sealed class OpenAiInterpretationDto
    {
        public bool EsSolicitudInmobiliaria { get; set; }
        public double Confianza { get; set; }
        public string? Observaciones { get; set; }
        public List<OpenAiSolicitudDto> Solicitudes { get; set; } = [];
    }

    private sealed class OpenAiSolicitudDto
    {
        public string? Operacion { get; set; }
        public List<string>? TiposPropiedad { get; set; }
        public List<string>? SubtiposPropiedad { get; set; }
        public List<string>? Zonas { get; set; }
        public string? TipoFraccionamiento { get; set; }
        public decimal? PrecioMinimo { get; set; }
        public decimal? PrecioMaximo { get; set; }
        public int? RecamarasMin { get; set; }
        public int? RecamarasMax { get; set; }
        public int? BanosMin { get; set; }
        public int? BanosMax { get; set; }
        public decimal? TerrenoMinM2 { get; set; }
        public decimal? ConstruccionMinM2 { get; set; }
        public bool? AceptaMascotas { get; set; }
        public bool? Amueblado { get; set; }
        public bool? UnaPlanta { get; set; }
        public bool? CasetaVigilancia { get; set; }
        public int? CocheraMinAutos { get; set; }
        public List<string>? ModalidadesPago { get; set; }
        public string? RequisitosAdicionales { get; set; }
    }
}
