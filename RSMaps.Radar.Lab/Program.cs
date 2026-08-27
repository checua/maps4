using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var engine = (Environment.GetEnvironmentVariable("RADAR_LAB_INTERPRETER") ?? "rules")
    .Trim()
    .ToLowerInvariant();

IRadarInterpreter interpreter = engine switch
{
    "openai" => CrearOpenAiInterpreter(),
    "rules" or "rule_based" => new RuleBasedRadarInterpreter(),
    _ => throw new InvalidOperationException(
        $"RADAR_LAB_INTERPRETER='{engine}' no es válido. Usa 'rules' u 'openai'.")
};

var casos = new (string Id, string Texto)[]
{
    (
        "local-cima-libramiento",
        "Buenos días. Busco local por el CIMA o libramiento para refaccionaria, agradezco sus aportes."
    ),
    (
        "planta-baja-patio",
        "Busco casa de 3 a 4 recámaras, indispensable que tenga 1 o 2 en la planta baja y un patio amplio. Es por Crédito Infonavit Total de $1,800,000 a $2,000,000 máximo."
    ),
    (
        "varias-zonas-renta",
        "Hola buenas tardes. Busco una casa para renta zona del Real del Mezquital, Artemisas, Nuevo Durango, Rancho San Miguel o ese rumbo. Presupuesto hasta $13,000."
    ),
    (
        "zonas-con-ruido",
        "Busco casa en venta para cliente. Ubicación ideal: Col. El Ciprés, La Piedrera, Fco. Zarco o cerca. Presupuesto hasta $2,000,000. Indispensable mínimo 3 habitaciones y patio grande."
    ),
    (
        "tres-solicitudes-en-un-mensaje",
        "Busco: Casa en venta (urge) de $1,350,000 zona sur de la ciudad, efectivo. Casa en renta zona sur de la ciudad de $7,000 máximo. Casa en renta en Colinas, Paseo del Saltito, Cerro de los Remedios, Lomas del Parque de $20,000 con recámara en planta baja."
    ),
    (
        "millones-y-respuesta-citada",
        "Hola buenas tardes. Busco casa al sur de la ciudad, presupuesto máximo $2.3 millones, una planta, fraccionamiento privado, pago al contado. Si aún busca esta casa, tengo opciones en Las Villas Residencial."
    ),
    (
        "casa-o-departamento",
        "Solicito casa/departamento en renta. Monto máximo $10,000. Espacio para dos personas. Ubicación en Alamedas, Analco, Remedios o Zona Centro."
    ),
    (
        "altamira-infonavit",
        "Busco casa por el rumbo de Fracc. Altamira, hasta $1,400,000, que pase por crédito Infonavit Conyugal. También puede ser en zonas cercanas, no muy retiradas."
    )
};

Console.WriteLine("==============================================");
Console.WriteLine("          RADAR INTERPRETER LAB");
Console.WriteLine("==============================================");
Console.WriteLine($"Motor: {interpreter.GetType().Name}");
Console.WriteLine($"Casos: {casos.Length}");
Console.WriteLine();

var totalInputTokens = 0;
var totalOutputTokens = 0;
var totalTokens = 0;

foreach (var caso in casos)
{
    var mensaje = new RadarMessage
    {
        MessageId = caso.Id,
        ChatOrigen = "RADAR-LAB",
        TextoOriginal = caso.Texto,
        DetectadoEn = DateTime.Now
    };

    try
    {
        var resultado = await interpreter.InterpretarAsync(mensaje);

        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"CASO: {caso.Id}");
        Console.WriteLine($"Motor: {resultado.Motor}");
        Console.WriteLine($"Confianza: {MostrarConfianza(resultado.ConfianzaInterpretacion)}");
        Console.WriteLine($"Solicitudes: {resultado.Solicitudes.Count}");

        if (!string.IsNullOrWhiteSpace(resultado.Observaciones))
            Console.WriteLine($"Observaciones: {resultado.Observaciones}");

        if (resultado.TotalTokens.HasValue)
        {
            Console.WriteLine(
                $"Tokens: entrada {resultado.InputTokens ?? 0}, salida {resultado.OutputTokens ?? 0}, total {resultado.TotalTokens.Value}");

            totalInputTokens += resultado.InputTokens ?? 0;
            totalOutputTokens += resultado.OutputTokens ?? 0;
            totalTokens += resultado.TotalTokens.Value;
        }

        for (var i = 0; i < resultado.Solicitudes.Count; i++)
        {
            var s = resultado.Solicitudes[i];
            Console.WriteLine($"  Solicitud #{i + 1}");
            Console.WriteLine($"    Operación: {s.Operacion ?? "-"}");
            Console.WriteLine($"    Tipos: {Mostrar(s.TiposPropiedad)}");
            Console.WriteLine($"    Zonas: {Mostrar(s.Zonas)}");
            Console.WriteLine($"    Precio mín.: {MostrarDinero(s.PrecioMinimo)}");
            Console.WriteLine($"    Precio máx.: {MostrarDinero(s.PrecioMaximo)}");
            Console.WriteLine($"    Recámaras: {MostrarRango(s.RecamarasMin, s.RecamarasMax)}");
            Console.WriteLine($"    Baños: {MostrarRango(s.BanosMin, s.BanosMax)}");
            Console.WriteLine($"    Una planta: {MostrarBooleano(s.UnaPlanta)}");
            Console.WriteLine($"    Pago: {Mostrar(s.ModalidadesPago)}");

            if (!string.IsNullOrWhiteSpace(s.RequisitosAdicionales))
                Console.WriteLine($"    Requisitos: {s.RequisitosAdicionales}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"CASO: {caso.Id}");
        Console.WriteLine($"ERROR: {ex.Message}");
    }

    Console.WriteLine();
}

if (totalTokens > 0)
{
    Console.WriteLine("==============================================");
    Console.WriteLine("USO TOTAL DEL BENCHMARK");
    Console.WriteLine($"Tokens entrada: {totalInputTokens}");
    Console.WriteLine($"Tokens salida:  {totalOutputTokens}");
    Console.WriteLine($"Tokens total:   {totalTokens}");
    Console.WriteLine("==============================================");
}

static IRadarInterpreter CrearOpenAiInterpreter()
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        throw new InvalidOperationException(
            "Para usar OpenAI define la variable de entorno OPENAI_API_KEY. La clave nunca debe guardarse en Git.");
    }

    var model = Environment.GetEnvironmentVariable("RADAR_OPENAI_MODEL") ?? "gpt-5-mini";
    return new OpenAiRadarInterpreter(apiKey, model);
}

static string Mostrar(IEnumerable<string> valores)
{
    var lista = valores.ToList();
    return lista.Count == 0 ? "-" : string.Join(" | ", lista);
}

static string MostrarDinero(decimal? valor) =>
    valor.HasValue ? valor.Value.ToString("C0") : "-";

static string MostrarRango(int? min, int? max)
{
    if (!min.HasValue && !max.HasValue)
        return "-";

    if (min == max)
        return min?.ToString() ?? "-";

    return $"{min?.ToString() ?? "-"} a {max?.ToString() ?? "-"}";
}

static string MostrarBooleano(bool? valor) => valor switch
{
    true => "Sí",
    false => "No",
    null => "-"
};

static string MostrarConfianza(double? valor) =>
    valor.HasValue ? $"{valor.Value:P0}" : "-";
