namespace maps4.Services;

// Only deterministic message defects opt in. Provider, model and unknown errors retry.
public sealed class RadarPermanentMessageException : Exception
{
    public RadarPermanentMessageException() : base("Mensaje central inválido de forma permanente.") { }
}
