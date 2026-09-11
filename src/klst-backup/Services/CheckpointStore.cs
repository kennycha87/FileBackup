using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KlstBackup.Models;

namespace KlstBackup.Services;

public class CheckpointStore
{
    private readonly string _checkpointDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public CheckpointStore(string appDataPath)
    {
        _checkpointDir = Path.Combine(appDataPath, "checkpoints");
        Directory.CreateDirectory(_checkpointDir);
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public void SaveCheckpoint(Guid taskId, CheckpointState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        var path = GetCheckpointPath(taskId);
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public CheckpointState? LoadCheckpoint(Guid taskId)
    {
        var path = GetCheckpointPath(taskId);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CheckpointState>(json);
    }

    public void DeleteCheckpoint(Guid taskId)
    {
        var path = GetCheckpointPath(taskId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public List<CheckpointState> ListCheckpoints()
    {
        var checkpoints = new List<CheckpointState>();
        foreach (var file in Directory.GetFiles(_checkpointDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json);
                if (checkpoint != null)
                    checkpoints.Add(checkpoint);
            }
            catch
            {
                // Skip corrupted checkpoints
            }
        }
        return checkpoints;
    }

    public bool ValidateCheckpoint(Guid taskId)
    {
        var checkpoint = LoadCheckpoint(taskId);
        if (checkpoint == null)
            return false;

        // Check required fields
        if (checkpoint.TaskId == Guid.Empty)
            return false;
        if (checkpoint.Files == null)
            return false;
        if (string.IsNullOrEmpty(checkpoint.SourcePath))
            return false;
        if (string.IsNullOrEmpty(checkpoint.DestPath))
            return false;

        // Check paths exist
        if (!Directory.Exists(checkpoint.SourcePath))
            return false;
        if (!Directory.Exists(checkpoint.DestPath))
            return false;

        return true;
    }

    public void CleanupOldCheckpoints(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var file in Directory.GetFiles(_checkpointDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<CheckpointState>(json);
                if (checkpoint != null && checkpoint.UpdatedAt < cutoff)
                    File.Delete(file);
            }
            catch
            {
                // Skip corrupted checkpoints
            }
        }
    }

    private string GetCheckpointPath(Guid taskId)
    {
        return Path.Combine(_checkpointDir, $"{taskId}.json");
    }
}
