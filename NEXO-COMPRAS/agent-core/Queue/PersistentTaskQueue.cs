using System.Text.Json;

namespace Nexo.Agent.Core.Queue;

public class QueueItem
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid AgentId { get; set; }
    public string TaskType { get; set; } = string.Empty; // estoque, produtos, fornecedores, precos
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; } = 0;
    public string? LastError { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Processing, Failed
}

public class PersistentTaskQueue
{
    private readonly string _queueFilePath;
    private readonly object _lock = new();

    public PersistentTaskQueue(string? customPath = null)
    {
        var dir = customPath != null
            ? Path.GetDirectoryName(customPath)!
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _queueFilePath = customPath ?? Path.Combine(dir, "offline_queue.json");

        lock (_lock)
        {
            if (!File.Exists(_queueFilePath))
                File.WriteAllText(_queueFilePath, "[]");
        }
    }

    public int Count => GetPendingCount();

    public int GetPendingCount(Guid? filterTenantId = null)
    {
        lock (_lock)
        {
            var list = LoadList();
            if (filterTenantId.HasValue && filterTenantId.Value != Guid.Empty)
                return list.Count(i => i.Status != "Completed" && i.TenantId == filterTenantId.Value);
            return list.Count(i => i.Status != "Completed");
        }
    }

    public Guid Enqueue(string taskType, object payload, Guid tenantId = default, Guid? branchId = null, Guid agentId = default)
    {
        lock (_lock)
        {
            var list = LoadList();
            var item = new QueueItem
            {
                OperationId = Guid.NewGuid(),
                TenantId = tenantId,
                BranchId = branchId,
                AgentId = agentId,
                TaskType = taskType,
                PayloadJson = JsonSerializer.Serialize(payload),
                EnqueuedAt = DateTime.UtcNow,
                Status = "Pending"
            };
            list.Add(item);
            SaveList(list);
            return item.OperationId;
        }
    }

    public List<QueueItem> GetPendingItems(Guid? filterTenantId = null, Guid? filterAgentId = null)
    {
        lock (_lock)
        {
            var list = LoadList();
            var q = list.Where(i => i.Status != "Completed");
            if (filterTenantId.HasValue && filterTenantId.Value != Guid.Empty)
                q = q.Where(i => i.TenantId == filterTenantId.Value);
            if (filterAgentId.HasValue && filterAgentId.Value != Guid.Empty)
                q = q.Where(i => i.AgentId == filterAgentId.Value);

            return q.OrderBy(i => i.EnqueuedAt).ToList();
        }
    }

    public int PurgeOrArchiveForeignItems(Guid currentTenantId, Guid currentAgentId)
    {
        lock (_lock)
        {
            var list = LoadList();
            var foreign = list.Where(i => i.TenantId != currentTenantId || (currentAgentId != Guid.Empty && i.AgentId != Guid.Empty && i.AgentId != currentAgentId)).ToList();
            if (foreign.Count == 0) return 0;

            // Remove itens que pertencem a outro tenant/agente para evitar contaminação
            list.RemoveAll(i => i.TenantId != currentTenantId || (currentAgentId != Guid.Empty && i.AgentId != Guid.Empty && i.AgentId != currentAgentId));
            SaveList(list);
            return foreign.Count;
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            SaveList(new List<QueueItem>());
        }
    }

    public void MarkCompleted(Guid operationId)
    {
        lock (_lock)
        {
            var list = LoadList();
            list.RemoveAll(i => i.OperationId == operationId);
            SaveList(list);
        }
    }

    public void MarkFailed(Guid operationId, string error)
    {
        lock (_lock)
        {
            var list = LoadList();
            var it = list.FirstOrDefault(i => i.OperationId == operationId);
            if (it != null)
            {
                it.AttemptCount++;
                it.LastError = error;
                it.Status = "Pending"; // Repõe como pendente para tentar novamente no próximo ciclo
                SaveList(list);
            }
        }
    }

    private List<QueueItem> LoadList()
    {
        try
        {
            if (!File.Exists(_queueFilePath)) return new List<QueueItem>();
            var json = File.ReadAllText(_queueFilePath);
            return JsonSerializer.Deserialize<List<QueueItem>>(json) ?? new List<QueueItem>();
        }
        catch
        {
            return new List<QueueItem>();
        }
    }

    private void SaveList(List<QueueItem> list)
    {
        try
        {
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_queueFilePath, json);
        }
        catch { /* concorrência de escrita */ }
    }
}
