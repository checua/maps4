using RSMaps.Radar.Listener.Models;

namespace RSMaps.Radar.Listener.Services;

public enum RadarWhatsAppCaptureDisposition
{
    Retry,
    Ignorar,
    Demanda
}

public sealed record RadarWhatsAppCaptureDecision(
    RadarWhatsAppCaptureDisposition Disposicion,
    RadarMessageClassification? Clasificacion,
    bool DebeMarcarComoConocido,
    bool DebeCrearRadarMessage,
    bool DebeInvocarIntelligence);

public static class RadarWhatsAppCaptureFlowDecision
{
    public static RadarWhatsAppCaptureDecision Evaluar(RadarWhatsAppMessageCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        if (!capture.Confirmado)
            return Crear(RadarWhatsAppCaptureDisposition.Retry, null, false, false, false);

        if (string.IsNullOrWhiteSpace(capture.TextoPropio))
            return Crear(RadarWhatsAppCaptureDisposition.Ignorar, null, true, false, false);

        RadarMessageClassification classification =
            RadarMessageClassifier.Clasificar(capture.TextoPropio);

        return classification == RadarMessageClassification.Demanda
            ? Crear(RadarWhatsAppCaptureDisposition.Demanda, classification, false, true, true)
            : Crear(RadarWhatsAppCaptureDisposition.Ignorar, classification, true, false, false);
    }

    private static RadarWhatsAppCaptureDecision Crear(
        RadarWhatsAppCaptureDisposition disposicion,
        RadarMessageClassification? clasificacion,
        bool conocido,
        bool crearMensaje,
        bool intelligence) =>
        new(disposicion, clasificacion, conocido, crearMensaje, intelligence);
}
