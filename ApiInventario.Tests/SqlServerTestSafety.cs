using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace ApiInventario.Tests;

internal static class SqlServerTestSafety
{
    internal static SqlConnectionStringBuilder ValidateConnection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Se requiere INVENTARIO_TEST_SQLSERVER.");

        // El parser descarta opciones vacias y puede sobrescribir duplicados.
        // Rechazo conservador sobre el texto: no permitir esas claves ni siquiera vacias.
        if (Regex.IsMatch(value,
            @"(?:^|;)\s*(?:AttachDBFilename|extended\s+properties|initial\s+file\s+name)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidOperationException("No se permite adjuntar archivos en las pruebas.");

        SqlConnectionStringBuilder connection;
        try
        {
            connection = new SqlConnectionStringBuilder(value);
        }
        catch (ArgumentException)
        {
            // No incluir la cadena ni el error del parser: pueden contener secretos.
            throw new InvalidOperationException("La conexion de pruebas contiene opciones invalidas.");
        }

        // SqlClient normaliza tambien los alias 'AttachDBFileName',
        // 'extended properties' e 'initial file name'. Rechazar incluso valores vacios.
        if (connection.ShouldSerialize("AttachDBFilename") || connection.UserInstance)
            throw new InvalidOperationException("No se permite adjuntar archivos ni usar User Instance en las pruebas.");

        if (!string.Equals(connection.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La conexion de pruebas debe indicar Database=master.");

        return connection;
    }

    internal static string GenerateDatabaseName() => $"InventarioConcurrencyTests_{Guid.NewGuid():N}";
}
