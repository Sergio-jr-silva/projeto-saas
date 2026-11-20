namespace Nexo.Api.Models;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}

/// <summary>Toda entidade operacional implementa esta interface. O TenantId é preenchido pelo backend, nunca pelo cliente.</summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
