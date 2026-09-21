using maps4.Models;
using maps4.Repositorios.Contrato;
using maps4.Services;
using RSMaps.Radar.Listener.Models;
using RSMaps.Radar.Listener.Services;

internal static class SolicitudAccionableRegression
{
    private sealed record Caso(
        string Id,
        SolicitudInmobiliaria Solicitud,
        bool InterpretacionValida,
        RadarSolicitudAccionabilidadEstado EsperadoA,
        RadarSolicitudAccionabilidadEstado EsperadoB,
        RadarSolicitudAccionabilidadEstado EsperadoC);

    public static void Run()
    {
        Caso[] casos =
        [
            C("A-op-tipo-zona-presupuesto", S(op: true, tipo: true, zona: true, precio: true), true, A, A, A),
            C("B-op-tipo-zona", S(op: true, tipo: true, zona: true), true, A, A, A),
            C("C-op-tipo-presupuesto", S(op: true, tipo: true, precio: true), true, A, A, A),
            C("D-op-tipo-poco-especifica", S(op: true, tipo: true), true, N, A, A),
            C("E-solo-operacion", S(op: true), true, N, N, N),
            C("F-solo-tipo", S(tipo: true), true, N, N, N),
            C("G-solo-zona", S(zona: true), true, N, N, N),
            C("H-solo-presupuesto", S(precio: true), true, N, N, N),
            C("I-sin-criterios", S(), true, N, N, N),
            C("J-tipo-zona-sin-operacion", S(tipo: true, zona: true), true, N, A, N),
            C("K-operacion-zona-sin-tipo", S(op: true, zona: true), true, N, A, N),
            C("L-rango-contradictorio", S(op: true, tipo: true, zona: true, precioContradictorio: true), true, I, I, I),
            C("M-tipo-no-canonico", S(op: true, tipo: true, zona: true), false, I, I, I),
            C("N-zona-no-respaldada", S(op: true, tipo: true, zona: true), false, I, I, I),
            C("O-multiples-criterios", S(op: true, tipo: true, zona: true, precio: true, recamaras: true, banos: true), true, A, A, A),
            C("P-un-criterio", S(tipo: true), true, N, N, N),
            C("Q-opcionales-sin-operacion", S(tipo: true, zona: true, precio: true, recamaras: true, banos: true, superficie: true), true, N, A, N),
            C("R-opcionales-sin-tipo", S(op: true, zona: true, precio: true, recamaras: true, banos: true, superficie: true), true, N, A, N),
            C("sin-ubicacion", S(op: true, tipo: true, precio: true), true, A, A, A),
            C("sin-presupuesto", S(op: true, tipo: true, zona: true), true, A, A, A),
            C("sin-recamaras", S(op: true, tipo: true, zona: true, precio: true), true, A, A, A),
            C("requisitos-adicionales-no-cuentan", S(op: true, tipo: true, requisitos: true), true, N, A, A),
            C("limites-superficie-validos", S(op: true, tipo: true, superficie: true), true, A, A, A),
            C("recamaras-contradictorias", S(op: true, tipo: true, recamarasContradictorias: true), true, I, I, I),
            C("banos-contradictorios", S(op: true, tipo: true, banosContradictorios: true), true, I, I, I),
            C("interpretacion-invalida", S(op: true, tipo: true, zona: true), false, I, I, I)
        ];

        Console.WriteLine("Caso | CriteriosFuertes | PoliticaA | PoliticaB | PoliticaC | MotivosA | MotivosB | MotivosC");
        int coincidenAbc = 0;
        int difierenAbc = 0;

        foreach (Caso caso in casos)
        {
            RadarSolicitudAccionabilidadDecision a = Evaluar(caso, RadarSolicitudAccionabilidadPolitica.Conservadora);
            RadarSolicitudAccionabilidadDecision b = Evaluar(caso, RadarSolicitudAccionabilidadPolitica.Flexible);
            RadarSolicitudAccionabilidadDecision c = Evaluar(caso, RadarSolicitudAccionabilidadPolitica.OperacionYTipo);
            Exigir(a.Estado == caso.EsperadoA, $"{caso.Id}: Política A esperada {caso.EsperadoA}, real {a.Estado}.");
            Exigir(b.Estado == caso.EsperadoB, $"{caso.Id}: Política B esperada {caso.EsperadoB}, real {b.Estado}.");
            Exigir(c.Estado == caso.EsperadoC, $"{caso.Id}: Política C esperada {caso.EsperadoC}, real {c.Estado}.");
            Exigir(a.CriteriosFuertes == b.CriteriosFuertes && b.CriteriosFuertes == c.CriteriosFuertes, $"{caso.Id}: conteos distintos entre políticas.");

            bool iguales = a.Estado == b.Estado && b.Estado == c.Estado;
            if (iguales) coincidenAbc++; else difierenAbc++;
            Console.WriteLine(
                $"{caso.Id} | {a.CriteriosFuertes} | {a.Estado} | {b.Estado} | {c.Estado} | " +
                $"{Mostrar(a.Motivos)} | {Mostrar(b.Motivos)} | {Mostrar(c.Motivos)}");
        }

        VerificarInvariantes();
        VerificarMatchingOperacionYTipo().GetAwaiter().GetResult();
        Console.WriteLine($"POLITICA_A_B_C_RESUMEN: coinciden={coincidenAbc}; difieren={difierenAbc}; total={casos.Length}");
        Console.WriteLine("OPERACION_TIPO_ACCIONABLE_PERO_POCO_ESPECIFICA");
        Console.WriteLine("SOLICITUD_ACCIONABLE_REGRESSION_OK");
    }

    private static void VerificarInvariantes()
    {
        SolicitudInmobiliaria baseSolicitud = S(op: true, tipo: true);
        SolicitudInmobiliaria enriquecida = S(op: true, tipo: true, zona: true);

        foreach (RadarSolicitudAccionabilidadPolitica politica in Enum.GetValues<RadarSolicitudAccionabilidadPolitica>())
        {
            RadarSolicitudAccionabilidadDecision primera = RadarSolicitudAccionabilidadEvaluator.Evaluar(baseSolicitud, politica);
            RadarSolicitudAccionabilidadDecision repetida = RadarSolicitudAccionabilidadEvaluator.Evaluar(baseSolicitud, politica);
            RadarSolicitudAccionabilidadDecision mayor = RadarSolicitudAccionabilidadEvaluator.Evaluar(enriquecida, politica);
            RadarSolicitudAccionabilidadDecision contradictoria = RadarSolicitudAccionabilidadEvaluator.Evaluar(
                S(op: true, tipo: true, zona: true, precioContradictorio: true), politica);
            RadarSolicitudAccionabilidadDecision vacia = RadarSolicitudAccionabilidadEvaluator.Evaluar(S(), politica);

            Exigir(
                primera.Estado == repetida.Estado &&
                primera.CriteriosFuertes == repetida.CriteriosFuertes &&
                primera.Motivos.SequenceEqual(repetida.Motivos) &&
                primera.CriteriosPresentes.SequenceEqual(repetida.CriteriosPresentes),
                $"{politica}: el mismo input no produjo la misma decisión.");
            Exigir(contradictoria.Estado != RadarSolicitudAccionabilidadEstado.Accionable, $"{politica}: contradicción accionable.");
            Exigir(vacia.Estado != RadarSolicitudAccionabilidadEstado.Accionable, $"{politica}: cero criterios accionable.");
            Exigir(
                primera.Estado != RadarSolicitudAccionabilidadEstado.Accionable ||
                mayor.Estado == RadarSolicitudAccionabilidadEstado.Accionable,
                $"{politica}: agregar información válida empeoró una decisión accionable.");
        }

        RadarSolicitudAccionabilidadPolitica c = RadarSolicitudAccionabilidadPolitica.OperacionYTipo;
        foreach (SolicitudInmobiliaria sinOperacion in new[]
                 {
                     S(tipo: true),
                     S(tipo: true, zona: true, precio: true, recamaras: true, banos: true, superficie: true)
                 })
        {
            Exigir(RadarSolicitudAccionabilidadEvaluator.Evaluar(sinOperacion, c).Estado != A,
                "Política C hizo accionable una solicitud sin operación.");
        }

        foreach (SolicitudInmobiliaria sinTipo in new[]
                 {
                     S(op: true),
                     S(op: true, zona: true, precio: true, recamaras: true, banos: true, superficie: true)
                 })
        {
            Exigir(RadarSolicitudAccionabilidadEvaluator.Evaluar(sinTipo, c).Estado != A,
                "Política C hizo accionable una solicitud sin tipo.");
        }

        foreach (SolicitudInmobiliaria solicitudEnriquecida in new[]
                 {
                     S(op: true, tipo: true),
                     S(op: true, tipo: true, zona: true),
                     S(op: true, tipo: true, precio: true),
                     S(op: true, tipo: true, zona: true, precio: true, recamaras: true, superficie: true)
                 })
        {
            Exigir(RadarSolicitudAccionabilidadEvaluator.Evaluar(solicitudEnriquecida, c).Estado == A,
                "Política C degradó operación + tipo al agregar información válida.");
        }
    }

    private static async Task VerificarMatchingOperacionYTipo()
    {
        var inventario = new List<InventarioInmuebleViewModel>
        {
            Inmueble(301, 3_000_000, "Casa en venta"),
            Inmueble(302, 1_000_000, "Casa en venta"),
            Inmueble(303, 2_000_000, "Casa en venta"),
            Inmueble(304, 900_000, "Casa en renta")
        };
        var matching = new RadarMatchingService(new InventarioSinteticoRepository(inventario));
        RadarMatchingResponse resultado = await matching.CompararAsync(
            "qa@example.invalid",
            new RadarMatchingRequest
            {
                Operacion = "Venta",
                TiposPropiedad = ["Casa"],
                MaxResultados = 10
            });

        Exigir(resultado.TotalInventarioEvaluado == 4, "Matching sintético no evaluó todo el inventario.");
        Exigir(resultado.TotalCandidatos == 3, "Matching sintético no filtró la renta incompatible.");
        Exigir(resultado.Resultados.Count == 3, "Matching sintético devolvió una cantidad inesperada.");
        Exigir(resultado.Resultados.All(x => x.Puntuacion == 100), "Operación + tipo no produjo el score esperado.");
        Exigir(resultado.Resultados.All(x => x.EsRecomendacionAutomatica), "Operación + tipo no produjo recomendaciones automáticas.");
        Exigir(resultado.Resultados.Select(x => x.IdInmueble).SequenceEqual(new[] { 302, 303, 301 }),
            "El desempate no ordenó por precio ascendente.");

        Console.WriteLine(
            "MATCHING_OPERACION_TIPO: evaluados=4; candidatos=3; resultados=3; " +
            "scores=100,100,100; automaticas=3; desempate=precio_ascendente; mejor=302");
    }

    private static InventarioInmuebleViewModel Inmueble(int id, double precio, string observaciones) =>
        new()
        {
            IdInmueble = id,
            TipoNombre = "Casa",
            Precio = precio,
            Observaciones = observaciones,
            EstadoCodigo = "PUBLICADO"
        };

    private static RadarSolicitudAccionabilidadDecision Evaluar(
        Caso caso,
        RadarSolicitudAccionabilidadPolitica politica) =>
        RadarSolicitudAccionabilidadEvaluator.Evaluar(
            caso.Solicitud,
            politica,
            caso.InterpretacionValida);

    private static Caso C(
        string id,
        SolicitudInmobiliaria solicitud,
        bool valida,
        RadarSolicitudAccionabilidadEstado esperadoA,
        RadarSolicitudAccionabilidadEstado esperadoB,
        RadarSolicitudAccionabilidadEstado esperadoC) =>
        new(id, solicitud, valida, esperadoA, esperadoB, esperadoC);

    private static SolicitudInmobiliaria S(
        bool op = false,
        bool tipo = false,
        bool zona = false,
        bool precio = false,
        bool recamaras = false,
        bool banos = false,
        bool superficie = false,
        bool requisitos = false,
        bool precioContradictorio = false,
        bool recamarasContradictorias = false,
        bool banosContradictorios = false) =>
        new()
        {
            Operacion = op ? "Venta" : null,
            TiposPropiedad = tipo ? ["Casa"] : [],
            Zonas = zona ? ["Zona Prueba"] : [],
            PrecioMinimo = precioContradictorio ? 2_000_000 : null,
            PrecioMaximo = precioContradictorio ? 1_000_000 : precio ? 2_000_000 : null,
            RecamarasMin = recamarasContradictorias ? 4 : recamaras ? 3 : null,
            RecamarasMax = recamarasContradictorias ? 2 : recamaras ? 3 : null,
            BanosMin = banosContradictorios ? 3 : banos ? 2 : null,
            BanosMax = banosContradictorios ? 1 : banos ? 2 : null,
            TerrenoMinM2 = superficie ? 120 : null,
            ConstruccionMinM2 = superficie ? 100 : null,
            RequisitosAdicionales = requisitos ? "Requisito de prueba" : null
        };

    private static string Mostrar(IReadOnlyList<RadarSolicitudAccionabilidadMotivo> motivos) =>
        motivos.Count == 0 ? "-" : string.Join(',', motivos);

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private const RadarSolicitudAccionabilidadEstado A = RadarSolicitudAccionabilidadEstado.Accionable;
    private const RadarSolicitudAccionabilidadEstado N = RadarSolicitudAccionabilidadEstado.NecesitaMasDatos;
    private const RadarSolicitudAccionabilidadEstado I = RadarSolicitudAccionabilidadEstado.Inconsistente;

    private sealed class InventarioSinteticoRepository(List<InventarioInmuebleViewModel> inventario)
        : IInventarioRepository
    {
        public Task<List<InventarioInmuebleViewModel>> ListarAutorizadosAsync(string correo) =>
            Task.FromResult(inventario);

        public Task<List<InventarioInmuebleViewModel>> ListarAsync(int idCuenta, int idAsesor) =>
            throw new NotSupportedException();

        public Task<InventarioAutorizacionContexto?> ObtenerContextoAutorizacionAsync(string correo) =>
            throw new NotSupportedException();

        public Task CambiarEstadoOVisibilidadAsync(int idInmueble, string correo, string? estadoNuevo, string? visibilidadNueva, string? motivo) =>
            throw new NotSupportedException();

        public Task CerrarOperacionAsync(int idInmueble, string correo, string tipoOperacion, decimal precioCierre, DateTime fechaCierreUtc, string? notasCierre) =>
            throw new NotSupportedException();
    }
}
