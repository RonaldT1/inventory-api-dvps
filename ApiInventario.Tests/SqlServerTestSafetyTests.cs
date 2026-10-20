namespace ApiInventario.Tests;

public class SqlServerTestSafetyTests
{
    [Theory]
    [InlineData("AttachDbFilename=test.mdf")]
    [InlineData("AttachDbFilename=")]
    [InlineData("extended properties=")]
    [InlineData("initial file name=")]
    [InlineData("aTtAcHdBfIlEnAmE =test.mdf;AttachDbFilename=")]
    [InlineData("extended properties=test.mdf")]
    [InlineData("initial file name=test.mdf")]
    [InlineData("User Instance=true")]
    public void ValidateConnection_RechazaAdjuntarArchivos(string option)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestSafety.ValidateConnection($"Server=localhost;Database=master;{option}"));
        Assert.Contains("No se permite", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=localhost")]
    [InlineData("Server=localhost;Database=InventorySalesDB")]
    [InlineData("Server=localhost;Database=master;UnsupportedOption=private-value")]
    public void ValidateConnection_RechazaConexionInseguraSinExponerValor(string? connection)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestSafety.ValidateConnection(connection));
        Assert.DoesNotContain("private-value", error.Message);
    }

    [Theory]
    [InlineData("Database=master")]
    [InlineData("Initial Catalog=MASTER")]
    public void ValidateConnection_AceptaMaster(string catalog)
    {
        var connection = SqlServerTestSafety.ValidateConnection($"Server=localhost;{catalog}");
        Assert.Equal("master", connection.InitialCatalog, ignoreCase: true);
    }

    [Fact]
    public void GenerateDatabaseName_UtilizaPrefijoYGuid()
    {
        const string prefix = "InventarioConcurrencyTests_";
        var name = SqlServerTestSafety.GenerateDatabaseName();
        Assert.StartsWith(prefix, name);
        Assert.True(Guid.TryParseExact(name[prefix.Length..], "N", out _));
        Assert.NotEqual(name, SqlServerTestSafety.GenerateDatabaseName());
    }
}
