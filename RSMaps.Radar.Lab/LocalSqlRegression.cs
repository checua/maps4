using Microsoft.Data.SqlClient;

internal static class LocalSqlRegression
{
    public static string MasterConnectionString()
    {
        string instance = Environment.GetEnvironmentVariable("RADAR_SQL_TEST_INSTANCE") ?? "MSSQLLocalDB";
        if (instance is not ("MSSQLLocalDB" or "RADAROfficeTests"))
            throw new InvalidOperationException("RADAR_SQL_TEST_INSTANCE must name an approved local test instance.");

        return new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\" + instance,
            InitialCatalog = "master",
            IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Optional,
            ConnectTimeout = 20
        }.ConnectionString;
    }
}