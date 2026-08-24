using maps4.Models;

namespace maps4.Services
{
    public interface IRadarMatchingService
    {
        Task<RadarMatchingResponse> CompararAsync(string correo, RadarMatchingRequest solicitud);
    }
}
