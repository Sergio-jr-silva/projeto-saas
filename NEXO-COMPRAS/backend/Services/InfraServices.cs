using Microsoft.EntityFrameworkCore;
using Nexo.Api.Data;
using Nexo.Api.Models;
using Nexo.Api.MultiTenancy;
using Nexo.Api.Services.Fiscal;

namespace Nexo.Api.Services;

public interface ITenantConfigService
{
    Task<ConfiguracaoTenant> GetAsync();
    static TributacaoConfig ToTributacao(ConfiguracaoTenant c) => new(c.RegimeTributario, c.CreditaIcms, c.CreditaIpi, c.PercCreditoPisCofins);
}

public class TenantConfigService(NexoDbContext db, ITenantContext tenant) : ITenantConfigService
{
    public async Task<ConfiguracaoTenant> GetAsync()
    {
        if (!tenant.CurrentTenantId.HasValue) throw new InvalidOperationException("Sem tenant no contexto.");
        var tid = tenant.CurrentTenantId.Value;

        var cfg = await db.ConfiguracoesTenant
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tid);

        if (cfg != null) return cfg;

        cfg = new ConfiguracaoTenant { TenantId = tid };
        try
        {
            db.ConfiguracoesTenant.Add(cfg);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Evita erro de chave duplicada caso outra requisição concorrente tenha criado
            db.Entry(cfg).State = EntityState.Detached;
            cfg = await db.ConfiguracoesTenant.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tid);
            if (cfg != null) return cfg;
            throw;
        }
        return cfg;
    }
}

/// <summary>Armazenamento de anexos. Local em desenvolvimento; a interface permite trocar por Azure Blob Storage sem alterar os controllers.</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Guid tenantId, Guid respostaId, string fileName, Stream content);
    Task<string> SaveLogoAsync(Guid tenantId, string extension, Stream content);
    bool Exists(string path);
    string GetFullPath(string path);
    Stream Open(string path);
    void Delete(string path);
}

public class LocalFileStorage(IConfiguration cfg, IHostEnvironment env) : IFileStorage
{
    private string Root => Path.GetFullPath(cfg["Storage:Path"] ?? Path.Combine(env.ContentRootPath, "storage"));

    public async Task<string> SaveAsync(Guid tenantId, Guid respostaId, string fileName, Stream content)
    {
        var rel = Path.Combine(tenantId.ToString("N"), respostaId.ToString("N"), $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}");
        var full = Path.Combine(Root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs);
        return rel;
    }

    public async Task<string> SaveLogoAsync(Guid tenantId, string extension, Stream content)
    {
        var dirRel = Path.Combine(tenantId.ToString("N"), "logo");
        var dirFull = Path.Combine(Root, dirRel);
        if (Directory.Exists(dirFull))
        {
            try
            {
                var antigos = Directory.GetFiles(dirFull);
                foreach (var a in antigos) { try { File.Delete(a); } catch { } }
            }
            catch { }
        }
        else
        {
            Directory.CreateDirectory(dirFull);
        }

        var fileName = $"logo_{DateTime.UtcNow.Ticks}{extension.ToLowerInvariant()}";
        var rel = Path.Combine(dirRel, fileName);
        var full = Path.Combine(Root, rel);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs);
        return rel;
    }

    public bool Exists(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return File.Exists(Resolve(path)); } catch { return false; }
    }

    public string GetFullPath(string path) => Resolve(path);

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(Root, path));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Caminho inválido.");
        return full;
    }
    public Stream Open(string path) => File.OpenRead(Resolve(path));
    public void Delete(string path) { var f = Resolve(path); if (File.Exists(f)) File.Delete(f); }
}

