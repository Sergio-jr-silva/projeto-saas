using System;
using System.IO;

namespace Nexo.Agent.Core.Storage;

public static class SincStoragePaths
{
    // %ProgramData%\NexoCompras\SINc
    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "NexoCompras",
        "SINc"
    );

    public static string ConfigFilePath => Path.Combine(BaseDirectory, "appsettings.json");
    public static string DatabaseDirectory => Path.Combine(BaseDirectory, "data");
    public static string DatabasePath => Path.Combine(DatabaseDirectory, "sinc_local.db");
    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    public static string ErrorLogPath => Path.Combine(LogsDirectory, "agent_error.log");

    public static void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(BaseDirectory)) Directory.CreateDirectory(BaseDirectory);
            if (!Directory.Exists(DatabaseDirectory)) Directory.CreateDirectory(DatabaseDirectory);
            if (!Directory.Exists(LogsDirectory)) Directory.CreateDirectory(LogsDirectory);

            // Se appsettings.json ainda não existir em %ProgramData%, inicializa a partir do modelo
            if (!File.Exists(ConfigFilePath))
            {
                var exeDir = AppContext.BaseDirectory;
                var exeConfig = Path.Combine(exeDir, "appsettings.json");
                if (File.Exists(exeConfig))
                {
                    File.Copy(exeConfig, ConfigFilePath, overwrite: false);
                }
                else
                {
                    var jsonDefault = @"{
  ""Logging"": { ""LogLevel"": { ""Default"": ""Information"" } },
  ""NexoApi"": {
    ""BaseUrl"": ""https://api.nexoscompras.com.br"",
    ""AgentKey"": """",
    ""TimeoutSeconds"": 30
  },
  ""Database"": { ""Type"": ""Firebird"" },
  ""Firebird"": {
    ""Host"": ""127.0.0.1"",
    ""Port"": 3050,
    ""Database"": """",
    ""User"": ""SYSDBA"",
    ""Password"": ""masterkey"",
    ""Charset"": ""WIN1252"",
    ""Dialect"": 3
  },
  ""PostgreSql"": {
    ""Host"": ""127.0.0.1"",
    ""Port"": 5432,
    ""Database"": ""sistema_comercial"",
    ""User"": ""postgres"",
    ""Password"": """",
    ""SslMode"": ""Prefer"",
    ""Schema"": ""public""
  },
  ""Sync"": {
    ""IntervaloEstoqueMin"": 5,
    ""IntervaloProdutosMin"": 30,
    ""IntervaloFornecedoresMin"": 30,
    ""IntervaloPrecosMin"": 15,
    ""IntervaloHeartbeatSeg"": 60
  }
}";
                    File.WriteAllText(ConfigFilePath, jsonDefault);
                }
            }
        }
        catch { }
    }
}
