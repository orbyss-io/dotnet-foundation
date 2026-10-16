using System.Text.Json.Nodes;
using Microsoft.Build.Framework;

namespace Orbyss.Foundation.Build;

/// <summary>Produces reviewable current source hashes without rewriting declarations or compiled provenance.</summary>
public sealed class InspectSettingsSourceBindings : Microsoft.Build.Utilities.Task
{
    /// <summary>Gets or sets the native publisher project directory.</summary>
    [Required] public string ProjectDirectory { get; set; } = "";
    /// <summary>Gets or sets the evaluated native compiler source inventory.</summary>
    [Required] public ITaskItem[] SourceFiles { get; set; } = [];
    /// <summary>Gets or sets the separate inspection report destination.</summary>
    [Required] public string OutputFile { get; set; } = "";

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            Require(SourceFiles.Length is > 0 and <= 512, "Settings source inventory requires 1 to 512 items.");
            var directory = Path.GetFullPath(ProjectDirectory);
            var output = Path.GetFullPath(OutputFile);
            var hashes = new JsonObject();
            long total = 0;
            foreach (var item in SourceFiles)
            {
                var path = Path.GetFullPath(Path.Combine(directory, item.ItemSpec));
                var name = Path.GetRelativePath(directory, path).Replace('\\', '/');
                if (name.StartsWith("obj/", StringComparison.Ordinal)) continue;
                Require(!Path.IsPathRooted(name) && !name.Contains(':')
                    && !name.Split('/').Any(part => part is "" or "." or ".."), "Settings sources must be contained in the publisher project.");
                Require(!string.Equals(path, output, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
                    "Inspection output cannot replace an input source.");
                for (var parent = new FileInfo(path) as FileSystemInfo; parent is not null && parent.FullName != directory;
                    parent = parent is FileInfo info ? info.Directory : ((DirectoryInfo)parent).Parent)
                    Require((parent.Attributes & FileAttributes.ReparsePoint) == 0, "Linked settings sources are unsupported.");
                var length = new FileInfo(path).Length;
                total += length;
                Require(length <= 1_048_576 && total <= 16_777_216, "Settings source exceeds 1 MiB/file or 16 MiB/inventory limit.");
                Require(!hashes.ContainsKey(name), "Duplicate settings source.");
                hashes.Add(name, SettingsSourceText.Hash(SettingsSourceText.Read(path)));
            }
            Require(hashes.Count > 0, "Settings source inventory is empty.");
            var report = new JsonObject { ["schemaVersion"] = 1,
                ["contract"] = "utf8-optional-bom-crlf-to-lf-v1", ["sourceSha256"] = hashes,
                ["authority"] = "Current source inspection only; review and merge into the declaration before normal build and pack validation." };
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Exception? operationFailure = null;
            try
            {
                File.WriteAllText(temporary, report.ToJsonString() + "\n");
                File.Move(temporary, output, overwrite: true);
            }
            catch (Exception error) { operationFailure = error; throw; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception cleanupError) when (operationFailure is not null)
                {
                    Log.LogError($"PKSM001 inspection cleanup failed; original operation error retained. Residual: {temporary}; cleanup: {cleanupError.Message}");
                }
            }
            return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
        {
            Log.LogError($"PKSM001 invalid settings source inspection: {error.Message}");
            return false;
        }
    }

    /// <summary>Rejects unsupported inspection input before replacing the report.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
