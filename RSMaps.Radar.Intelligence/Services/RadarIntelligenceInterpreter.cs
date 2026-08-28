using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public sealed class RadarIntelligenceInterpreter : IRadarInterpreter
{
    private readonly IRadarInterpreter _primario;
    private readonly IRadarInterpreter? _fallback;

    public RadarIntelligenceInterpreter(
        IRadarInterpreter primario,
        IRadarInterpreter? fallback = null)
    {
        _primario = primario;
        _fallback = fallback;
    }

    public async Task<RadarInterpretationResult> InterpretarAsync(
        RadarMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var primario = await _primario.InterpretarAsync(mensaje, cancellationToken);
        RadarInterpretationNormalizer.Normalizar(primario, mensaje);
        RadarInterpretationSemanticCleaner.Limpiar(primario);
        var validacionPrimaria = RadarInterpretationValidator.Validar(primario, mensaje);

        if (!validacionPrimaria.RequiereFallback || _fallback is null)
        {
            DecorarResultado(
                primario,
                motorPrimario: primario.Motor,
                usoFallback: false,
                validacionPrimaria);

            primario.Motor = $"RADAR:{primario.Motor}";
            return primario;
        }

        var secundario = await _fallback.InterpretarAsync(mensaje, cancellationToken);
        RadarInterpretationNormalizer.Normalizar(secundario, mensaje);
        RadarInterpretationSemanticCleaner.Limpiar(secundario);
        var validacionSecundaria = RadarInterpretationValidator.Validar(secundario, mensaje);

        var usarFallback = validacionSecundaria.Problemas.Count <= validacionPrimaria.Problemas.Count;
        var elegido = usarFallback ? secundario : primario;
        var validacionElegida = usarFallback ? validacionSecundaria : validacionPrimaria;

        var inputTotal = (primario.InputTokens ?? 0) + (secundario.InputTokens ?? 0);
        var outputTotal = (primario.OutputTokens ?? 0) + (secundario.OutputTokens ?? 0);
        var tokensTotal = (primario.TotalTokens ?? 0) + (secundario.TotalTokens ?? 0);

        elegido.InputTokens = inputTotal > 0 ? inputTotal : null;
        elegido.OutputTokens = outputTotal > 0 ? outputTotal : null;
        elegido.TotalTokens = tokensTotal > 0 ? tokensTotal : null;

        DecorarResultado(
            elegido,
            motorPrimario: primario.Motor,
            usoFallback: usarFallback,
            validacionElegida);

        elegido.Motor = usarFallback
            ? $"RADAR:{primario.Motor}->FALLBACK:{secundario.Motor}"
            : $"RADAR:{primario.Motor}[fallback descartado:{secundario.Motor}]";

        return elegido;
    }

    private static void DecorarResultado(
        RadarInterpretationResult resultado,
        string motorPrimario,
        bool usoFallback,
        RadarValidationResult validacion)
    {
        resultado.MotorPrimario = motorPrimario;
        resultado.UsoFallback = usoFallback;
        resultado.ProblemasValidacion = [.. validacion.Problemas];
        resultado.AdvertenciasValidacion = [.. validacion.Advertencias];
    }
}
