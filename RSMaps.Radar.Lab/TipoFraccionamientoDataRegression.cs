using System.ComponentModel.DataAnnotations;
using maps4.Models;

internal static class TipoFraccionamientoDataRegression
{
    public static void Run()
    {
        Asegurar(TipoFraccionamientoCodigos.NormalizarParaPersistencia(null) is null, "NULL debe permanecer NULL.");
        Asegurar(TipoFraccionamientoCodigos.NormalizarParaPersistencia("") is null, "Vacio debe normalizarse a NULL.");
        Asegurar(TipoFraccionamientoCodigos.NormalizarParaPersistencia("PRIVADO") == "PRIVADO", "PRIVADO debe conservarse.");
        Asegurar(TipoFraccionamientoCodigos.NormalizarParaPersistencia("ABIERTO") == "ABIERTO", "ABIERTO debe conservarse.");

        foreach (string invalido in new[] { "   ", "PRIVADA", "Privado", "privado", "CERRADO", "TRUE", "FALSE", "OTRO" })
        {
            bool rechazado = false;
            try
            {
                TipoFraccionamientoCodigos.NormalizarParaPersistencia(invalido);
            }
            catch (ArgumentException)
            {
                rechazado = true;
            }

            Asegurar(rechazado, $"El valor invalido {invalido} debe rechazarse.");
        }

        ValidarModelo(null, esperadoValido: true);
        ValidarModelo("", esperadoValido: true);
        ValidarModelo("PRIVADO", esperadoValido: true);
        ValidarModelo("ABIERTO", esperadoValido: true);
        ValidarModelo("Privado", esperadoValido: false);

        string raiz = EncontrarRaizRepositorio();
        string sql = File.ReadAllText(Path.Combine(raiz, "sql", "RSMaps2", "56_tipo_fraccionamiento_data.sql"));
        Asegurar(sql.Contains("COL_LENGTH(N'dbo.RSMAPS_Inmueble', N'TipoFraccionamientoCodigo') IS NULL", StringComparison.Ordinal), "El schema debe ser idempotente.");
        Asegurar(sql.Contains("TipoFraccionamientoCodigo COLLATE Latin1_General_100_BIN2 IN ('PRIVADO','ABIERTO')", StringComparison.Ordinal), "La BD debe tener allowlist sensible a mayusculas.");
        Asegurar(sql.Contains("DATALENGTH(TipoFraccionamientoCodigo)=7", StringComparison.Ordinal), "El CHECK debe rechazar espacios y longitudes no canonicas.");
        Asegurar(sql.Contains("ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK\r\n    CHECK CONSTRAINT CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo", StringComparison.Ordinal)
            || sql.Contains("ALTER TABLE dbo.RSMAPS_Inmueble WITH CHECK\n    CHECK CONSTRAINT CK_RSMAPS_Inmueble_TipoFraccionamientoCodigo", StringComparison.Ordinal), "La reejecucion debe habilitar y confiar el CHECK.");
        Asegurar(sql.Contains("@actualizarTipoFraccionamiento BIT=0", StringComparison.Ordinal), "El parametro legacy debe preservar por defecto.");
        Asegurar(sql.Contains("WHEN @actualizarTipoFraccionamiento=1 THEN @tipoFraccionamientoCodigo", StringComparison.Ordinal), "La actualizacion explicita debe permitir PRIVADO, ABIERTO o NULL.");
        Asegurar(sql.Contains("ELSE TipoFraccionamientoCodigo END", StringComparison.Ordinal), "Un guardado parcial debe conservar el valor.");
        Asegurar(!sql.Contains("UPDATE dbo.RSMAPS_Inmueble SET TipoFraccionamientoCodigo=NULL", StringComparison.OrdinalIgnoreCase), "No debe existir backfill a NULL.");

        string repositorio = File.ReadAllText(Path.Combine(raiz, "Repositorios", "Implementacion", "BorradorInmuebleRepository.cs"));
        Asegurar(repositorio.Contains("dr[\"TipoFraccionamientoCodigo\"]", StringComparison.Ordinal), "La lectura debe mapear la columna por nombre.");
        Asegurar(repositorio.Contains("@actualizarTipoFraccionamiento", StringComparison.Ordinal), "La escritura debe enviar la intencion explicita.");

        string borradorController = File.ReadAllText(Path.Combine(raiz, "Controllers", "BorradorController.cs"));
        Asegurar(Contar(borradorController, "Request.Form.ContainsKey(nameof(modelo.TipoFraccionamientoCodigo))") == 2, "Editar y GuardarAntesDeFotos deben distinguir campo omitido.");
        Asegurar(Contar(borradorController, "Request.Form[nameof(modelo.TipoFraccionamientoCodigo)].Count != 1") == 2, "Editar y GuardarAntesDeFotos deben rechazar valores multiples.");

        string publicacionController = File.ReadAllText(Path.Combine(raiz, "Controllers", "PublicacionController.cs"));
        Asegurar(Contar(publicacionController, "Request.Form.ContainsKey(nameof(modelo.TipoFraccionamientoCodigo))") == 1, "Publicar debe distinguir campo omitido.");
        Asegurar(Contar(publicacionController, "Request.Form[nameof(modelo.TipoFraccionamientoCodigo)].Count != 1") == 1, "Publicar debe rechazar valores multiples.");

        Console.WriteLine("LECTURA_PRIVADO_OK");
        Console.WriteLine("LECTURA_ABIERTO_OK");
        Console.WriteLine("LECTURA_NULL_OK");
        Console.WriteLine("ESCRITURA_PRIVADO_OK");
        Console.WriteLine("ESCRITURA_ABIERTO_OK");
        Console.WriteLine("ESCRITURA_NULL_OK");
        Console.WriteLine("GUARDADO_PARCIAL_CONSERVA_VALOR_OK");
        Console.WriteLine("TIPO_FRACCIONAMIENTO_DATA_REGRESSION_OK");
    }

    private static void ValidarModelo(string? valor, bool esperadoValido)
    {
        BorradorEdicionViewModel modelo = new() { TipoFraccionamientoCodigo = valor };
        List<ValidationResult> resultados = new();
        bool valido = Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        Asegurar(valido == esperadoValido, $"Validacion inesperada para '{valor ?? "NULL"}'.");
    }

    private static string EncontrarRaizRepositorio()
    {
        DirectoryInfo? actual = new(AppContext.BaseDirectory);
        while (actual is not null)
        {
            if (File.Exists(Path.Combine(actual.FullName, "maps4.csproj")))
                return actual.FullName;
            actual = actual.Parent;
        }

        throw new InvalidOperationException("No se encontro la raiz del repositorio.");
    }

    private static void Asegurar(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
    }

    private static int Contar(string texto, string valor)
    {
        int total = 0;
        int indice = 0;
        while ((indice = texto.IndexOf(valor, indice, StringComparison.Ordinal)) >= 0)
        {
            total++;
            indice += valor.Length;
        }
        return total;
    }
}
