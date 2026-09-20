namespace RSMaps.Radar.Listener.Services;

public enum RadarMessageClassification
{
    Demanda,
    Oferta,
    Otro
}

public static class RadarMessageClassifier
{
    public static RadarMessageClassification Clasificar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        string text = Normalizar(texto);

        string[] demandaFuerte =
        {
            "busco", "buscando", "buscamos", "ando buscando", "estoy buscando",
            "estamos buscando", "sigo en busqueda", "aun sigo en busqueda",
            "solicito para cliente", "solicito renta", "solicito casa",
            "solicito terreno", "solicito departamento", "necesito", "necesitamos",
            "requiero", "requerimos", "cliente busca", "mi cliente busca",
            "para un cliente", "para cliente", "alguien tendra", "alguien traera",
            "algun compañero tiene", "alguien tiene", "me pudiera compartir",
            "me pueden compartir opciones", "agradezco sus opciones",
            "recibo propuesta", "recibo propuestas"
        };

        string[] ofertaFuerte =
        {
            "ofrezco", "vendo", "rento", "se vende", "se renta",
            "pongo a su disposicion", "pongo a la disposicion",
            "tenemos a la venta", "tenemos en venta", "tenemos a la renta",
            "tenemos en renta", "propiedad en preventa", "casa en preventa",
            "casa en venta", "departamento en renta", "terreno en venta",
            "local en renta", "bodega en renta", "tenemos disponible", "tengo disponible"
        };

        string[] exclusionesDemanda =
        {
            "solicitar la licencia", "solicitar licencia", "solicitar informacion",
            "solicitar constancia", "solicitar informes"
        };

        if (exclusionesDemanda.Any(text.Contains) && !demandaFuerte.Any(text.Contains))
            return RadarMessageClassification.Otro;

        if (demandaFuerte.Any(text.Contains))
            return RadarMessageClassification.Demanda;

        if (ofertaFuerte.Any(text.Contains))
            return RadarMessageClassification.Oferta;

        string[] demandaDebil =
        {
            "tendran", "tendras", "alguna propiedad", "alguna casa",
            "algun terreno", "alguna bodega", "algun local"
        };

        return demandaDebil.Any(text.Contains)
            ? RadarMessageClassification.Demanda
            : RadarMessageClassification.Otro;
    }

    public static string Normalizar(string texto) => texto
        .ToLowerInvariant()
        .Replace("á", "a")
        .Replace("é", "e")
        .Replace("í", "i")
        .Replace("ó", "o")
        .Replace("ú", "u")
        .Replace("ü", "u")
        .Replace("ñ", "n");
}
