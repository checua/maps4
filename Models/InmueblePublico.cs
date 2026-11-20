using System.Security.Claims;

namespace maps4.Models
{
    /// <summary>
    /// Proyección pública de un inmueble para los endpoints sin autenticación
    /// (mapa, ficha y deep links). La API pública nunca expone el correo del
    /// asesor; el teléfono solo se incluye cuando hay sesión iniciada.
    /// La decisión de editar la calcula el servidor (PuedeEditar) para que el
    /// JavaScript no tenga que comparar correos.
    /// </summary>
    public static class InmueblePublico
    {
        // Cambiar a false si se decide que el teléfono de contacto sea público
        // también para visitantes sin sesión. El correo nunca se expone.
        public const bool TelefonoSoloAutenticados = true;

        public static string? CorreoAutenticado(ClaimsPrincipal usuario)
        {
            var correo = usuario?.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(correo))
                return correo;
            var nombre = usuario?.Identity?.Name;
            return string.IsNullOrWhiteSpace(nombre) ? null : nombre;
        }

        public static object Proyectar(Inmueble item, string? correoAutenticado, bool mostrarTelefono)
        {
            var refUsuario = item.RefUsuario;
            var esPropio = !string.IsNullOrWhiteSpace(correoAutenticado)
                && !string.IsNullOrWhiteSpace(refUsuario?.correo)
                && string.Equals(correoAutenticado.Trim(), refUsuario.correo.Trim(), StringComparison.OrdinalIgnoreCase);

            return new
            {
                item.IdInmueble,
                item.refInmobiliaria,
                RefUsuario = refUsuario == null ? null : new
                {
                    refUsuario.idAsesor,
                    refUsuario.nombres,
                    refUsuario.aPaterno,
                    refUsuario.aMaterno,
                    refUsuario.refInmobiliaria,
                    refUsuario.nick,
                    refUsuario.foto
                },
                item.Direccion,
                item.Lat,
                item.Lng,
                item.IdTipo,
                Telefono = mostrarTelefono ? item.Telefono : null,
                item.Terreno,
                item.Construccion,
                item.Precio,
                item.Observaciones,
                item.Exclusiva,
                item.Link,
                item.Contacto,
                item.Imagenes,
                item.Recamaras,
                item.BanosCompletos,
                item.MediosBanos,
                item.Estacionamientos,
                item.Niveles,
                item.AntiguedadAnos,
                item.AmenidadesCsv,
                PuedeEditar = esPropio
            };
        }
    }
}
