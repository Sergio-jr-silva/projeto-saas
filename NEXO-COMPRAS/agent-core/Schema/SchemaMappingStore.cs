using System;
using System.IO;
using System.Text.Json;

namespace Nexo.Agent.Core.Schema;

public class SchemaMappingStore
{
    private readonly string _mappingFilePath;
    private ResolvedTableMapping? _cached;

    public SchemaMappingStore(string? customDir = null)
    {
        var dir = customDir ?? Storage.SincStoragePaths.DatabaseDirectory;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        _mappingFilePath = Path.Combine(dir, "schema_mapping.json");
    }

    public ResolvedTableMapping? LoadMapping()
    {
        if (_cached != null) return _cached;
        try
        {
            if (!File.Exists(_mappingFilePath)) return null;
            var json = File.ReadAllText(_mappingFilePath);
            _cached = JsonSerializer.Deserialize<ResolvedTableMapping>(json);
            return _cached;
        }
        catch
        {
            return null;
        }
    }

    public void SaveMapping(ResolvedTableMapping mapping)
    {
        try
        {
            var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_mappingFilePath, json);
            _cached = mapping;
        }
        catch { }
    }

    public void InvalidateCache()
    {
        _cached = null;
        try
        {
            if (File.Exists(_mappingFilePath)) File.Delete(_mappingFilePath);
        }
        catch { }
    }
}

