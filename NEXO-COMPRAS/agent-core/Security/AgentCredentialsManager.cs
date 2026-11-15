using System;
using System.IO;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nexo.Agent.Core.Security;

public class StoredAgentCredentials
{
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public string AgentCredential { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public Guid? FilialId { get; set; }
    public string FilialNome { get; set; } = string.Empty;
    public string FilialCodigo { get; set; } = string.Empty;
    public DateTime AtivadoEm { get; set; } = DateTime.UtcNow;
}

public class AgentCredentialsManager
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NexoCompras_AgentSecureDPAPI_Entropy_2026");
    private readonly string _credentialsPath;
    private readonly string _configPath;
    private StoredAgentCredentials? _cached;

    public AgentCredentialsManager(string? dataDir = null)
    {
        var baseDir = dataDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

        _credentialsPath = Path.Combine(baseDir, "agent_credentials.dat");
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    }

    public static string MaskCredential(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "[Não configurada]";
        if (key.Length <= 8) return "********";
        return key[..8] + new string('*', Math.Min(16, key.Length - 8));
    }

    public bool HasCredentials()
    {
        return File.Exists(_credentialsPath) && LoadCredentials() != null;
    }

    public StoredAgentCredentials? LoadCredentials()
    {
        if (_cached != null) return _cached;

        try
        {
            if (!File.Exists(_credentialsPath)) return null;

            var encryptedBytes = File.ReadAllBytes(_credentialsPath);
            if (encryptedBytes.Length == 0) return null;

            byte[] plainBytes;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                plainBytes = encryptedBytes; // Fallback para ambientes não-Windows
            }

            var json = Encoding.UTF8.GetString(plainBytes);
            _cached = JsonSerializer.Deserialize<StoredAgentCredentials>(json);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    public void SaveCredentials(StoredAgentCredentials creds)
    {
        var previous = LoadCredentials();
        if (previous != null && (previous.TenantId != creds.TenantId || previous.FilialId != creds.FilialId || previous.AgentId != creds.AgentId))
        {
            // Tenant ou filial alterado! Purgar itens offline e caches para garantir isolamento absoluto
            try
            {
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                var queue = new Queue.PersistentTaskQueue(Path.Combine(dataDir, "offline_queue.json"));
                queue.ClearAll();

                new Schema.SchemaMappingStore(dataDir).InvalidateCache();

                if (Directory.Exists(dataDir))
                {
                    foreach (var sf in Directory.GetFiles(dataDir, "sync_state*.json"))
                    {
                        try { File.Delete(sf); } catch { }
                    }
                }
            }
            catch { }
        }

        var json = JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true });
        var plainBytes = Encoding.UTF8.GetBytes(json);

        byte[] encryptedBytes;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            encryptedBytes = plainBytes;
        }

        File.WriteAllBytes(_credentialsPath, encryptedBytes);
        _cached = creds;
    }

    public Task<(bool Sucesso, string Mensagem)> AtivarComCodigoAsync(
        string baseUrl,
        string codigoAtivacao,
        Action<string, string>? logger = null)
    {
        return AtivarComTokenECnpjBaseAsync(baseUrl, codigoAtivacao, null, logger);
    }

    public async Task<(bool Sucesso, string Mensagem)> AtivarComTokenECnpjBaseAsync(
        string baseUrl,
        string token,
        string? cnpjBase,
        Action<string, string>? logger = null)
    {
        try
        {
            var isStoreId = Guid.TryParse(token.Trim(), out _);
            logger?.Invoke("INFO", "Iniciando autenticação...");
            logger?.Invoke("INFO", $"Validando token de integração no servidor...");

            using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            http.Timeout = TimeSpan.FromSeconds(25);

            var req = new
            {
                CodigoAtivacao = token.Trim(),
                CnpjBase = string.IsNullOrWhiteSpace(cnpjBase) ? null : cnpjBase.Trim(),
                NomeMaquina = Environment.MachineName,
                Ambiente = new
                {
                    OS = Environment.OSVersion.ToString(),
                    Processadores = Environment.ProcessorCount,
                    DataAtivacao = DateTime.UtcNow.ToString("s")
                }
            };

            var res = await http.PostAsJsonAsync("api/agent/v1/activate", req);
            var content = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                string msgErro = "Falha na validação do token.";
                try
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("erro", out var e))
                        msgErro = e.GetString() ?? msgErro;
                }
                catch { }

                logger?.Invoke("ERROR", $"Erro na autenticação: {msgErro} (HTTP {(int)res.StatusCode})");
                return (false, msgErro);
            }

            using var docOk = JsonDocument.Parse(content);
            var root = docOk.RootElement;

            var agentId = root.GetProperty("agentId").GetGuid();
            var tenantId = root.GetProperty("tenantId").GetGuid();
            var agentCredential = root.GetProperty("agentCredential").GetString() ?? "";

            var emp = root.GetProperty("empresa");
            var razaoSocial = emp.GetProperty("razaoSocial").GetString() ?? "";
            var nomeFantasia = emp.GetProperty("nomeFantasia").GetString() ?? "";
            var cnpj = emp.GetProperty("cnpj").GetString() ?? "";

            Guid? filialId = null;
            if (root.TryGetProperty("filialId", out var fIdEl) && fIdEl.ValueKind == JsonValueKind.String && Guid.TryParse(fIdEl.GetString(), out var fParsed))
                filialId = fParsed;
            else if (emp.TryGetProperty("filialId", out var efIdEl) && efIdEl.ValueKind == JsonValueKind.String && Guid.TryParse(efIdEl.GetString(), out var efParsed))
                filialId = efParsed;

            string filialNome = "";
            if (root.TryGetProperty("filialNome", out var fnEl) && fnEl.ValueKind == JsonValueKind.String)
                filialNome = fnEl.GetString() ?? "";
            else if (emp.TryGetProperty("filialNome", out var efnEl) && efnEl.ValueKind == JsonValueKind.String)
                filialNome = efnEl.GetString() ?? "";

            string filialCodigo = "";
            if (root.TryGetProperty("filialCodigo", out var fcEl) && fcEl.ValueKind == JsonValueKind.String)
                filialCodigo = fcEl.GetString() ?? "";
            else if (emp.TryGetProperty("filialCodigo", out var efcEl) && efcEl.ValueKind == JsonValueKind.String)
                filialCodigo = efcEl.GetString() ?? "";

            // Salva credenciais com criptografia Windows DPAPI
            var stored = new StoredAgentCredentials
            {
                AgentId = agentId,
                TenantId = tenantId,
                AgentCredential = agentCredential,
                RazaoSocial = razaoSocial,
                NomeFantasia = nomeFantasia,
                Cnpj = cnpj,
                FilialId = filialId,
                FilialNome = filialNome,
                FilialCodigo = filialCodigo,
                AtivadoEm = DateTime.UtcNow
            };
            SaveCredentials(stored);

            logger?.Invoke("SUCCESS", "Token validado com sucesso.");
            logger?.Invoke("SUCCESS", $"Empresa identificada: {razaoSocial} (CNPJ: {cnpj}).");
            if (!string.IsNullOrEmpty(filialNome))
            {
                logger?.Invoke("SUCCESS", $"Filial identificada: {filialNome} (Código: {filialCodigo}).");
            }

            // Atualiza appsettings.json com parâmetros autorizados do banco e intervalos
            AtualizarAppSettingsAposAtivacao(root, baseUrl, agentCredential, logger);

            return (true, $"Sincronizador ativado com sucesso para {razaoSocial}!");
        }
        catch (Exception ex)
        {
            logger?.Invoke("ERROR", $"Exceção durante a ativação: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private void AtualizarAppSettingsAposAtivacao(JsonElement root, string baseUrl, string agentCredential, Action<string, string>? logger)
    {
        try
        {
            if (!File.Exists(_configPath)) return;

            var jsonText = File.ReadAllText(_configPath);
            var rootNode = JsonNode.Parse(jsonText)?.AsObject() ?? new JsonObject();

            // Atualiza credencial e URL
            if (!rootNode.ContainsKey("NexoApi")) rootNode["NexoApi"] = new JsonObject();
            rootNode["NexoApi"]!["BaseUrl"] = baseUrl;
            rootNode["NexoApi"]!["AgentKey"] = agentCredential;

            // Atualiza banco se retornado
            if (root.TryGetProperty("bancoConfig", out var bc))
            {
                var tipo = bc.GetProperty("tipo").GetString() ?? "Firebird";
                if (!rootNode.ContainsKey("Database")) rootNode["Database"] = new JsonObject();
                rootNode["Database"]!["Type"] = tipo;

                if (tipo == "Firebird" && rootNode.ContainsKey("Firebird"))
                {
                    var fb = rootNode["Firebird"]!.AsObject();
                    if (bc.TryGetProperty("host", out var h) && !string.IsNullOrWhiteSpace(h.GetString())) fb["Host"] = h.GetString();
                    if (bc.TryGetProperty("porta", out var p) && p.GetInt32() > 0) fb["Port"] = p.GetInt32();
                    if (bc.TryGetProperty("caminhoOuBanco", out var c))
                    {
                        var dbVal = c.GetString();
                        // Só atualiza se o servidor retornou um caminho válido e específico (não vazio e não demo BM.FDB)
                        if (!string.IsNullOrWhiteSpace(dbVal) && !dbVal.EndsWith(@"BM.FDB", StringComparison.OrdinalIgnoreCase))
                        {
                            fb["Database"] = dbVal;
                        }
                    }
                    if (bc.TryGetProperty("usuario", out var u) && !string.IsNullOrWhiteSpace(u.GetString())) fb["User"] = u.GetString();
                    if (bc.TryGetProperty("senha", out var s) && !string.IsNullOrEmpty(s.GetString())) fb["Password"] = s.GetString();
                    if (bc.TryGetProperty("charset", out var ch) && !string.IsNullOrWhiteSpace(ch.GetString())) fb["Charset"] = ch.GetString();
                }
                else if (tipo == "PostgreSQL" && rootNode.ContainsKey("PostgreSql"))
                {
                    var pg = rootNode["PostgreSql"]!.AsObject();
                    if (bc.TryGetProperty("host", out var h) && !string.IsNullOrWhiteSpace(h.GetString())) pg["Host"] = h.GetString();
                    if (bc.TryGetProperty("porta", out var p) && p.GetInt32() > 0) pg["Port"] = p.GetInt32();
                    if (bc.TryGetProperty("caminhoOuBanco", out var c))
                    {
                        var dbVal = c.GetString();
                        if (!string.IsNullOrWhiteSpace(dbVal))
                        {
                            pg["Database"] = dbVal;
                        }
                    }
                    if (bc.TryGetProperty("usuario", out var u) && !string.IsNullOrWhiteSpace(u.GetString())) pg["User"] = u.GetString();
                    if (bc.TryGetProperty("senha", out var s) && !string.IsNullOrEmpty(s.GetString())) pg["Password"] = s.GetString();
                }
            }

            // Atualiza intervalos se retornados
            if (root.TryGetProperty("intervalos", out var inter))
            {
                if (!rootNode.ContainsKey("Sync")) rootNode["Sync"] = new JsonObject();
                var sync = rootNode["Sync"]!.AsObject();
                if (inter.TryGetProperty("estoqueMin", out var em)) sync["IntervaloEstoqueMin"] = em.GetInt32();
                if (inter.TryGetProperty("produtosMin", out var pm)) sync["IntervaloProdutosMin"] = pm.GetInt32();
                if (inter.TryGetProperty("fornecedoresMin", out var fm)) sync["IntervaloFornecedoresMin"] = fm.GetInt32();
                if (inter.TryGetProperty("heartbeatSeg", out var hs)) sync["IntervaloHeartbeatSeg"] = hs.GetInt32();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configPath, rootNode.ToJsonString(options));
            logger?.Invoke("INFO", "Configurações autorizadas locais atualizadas com os parâmetros do servidor.");
        }
        catch (Exception ex)
        {
            logger?.Invoke("WARN", $"Aviso ao atualizar appsettings.json: {ex.Message}");
        }
    }
}

