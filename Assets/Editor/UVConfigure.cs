using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class UVConfigure
{
    const string UvxPrefKey = "MCPForUnity.UvxPath";

    [MenuItem("Tools/UV/Auto Configure UV for MCP")]
    public static void AutoConfigureUv()
    {
        string found = FindBestUvxPath();
        if (string.IsNullOrEmpty(found))
        {
            Debug.LogWarning("Could not locate uv/uvx automatically. Please install uv or choose the path manually.");
            return;
        }

        try
        {
            EditorPrefs.SetString(UvxPrefKey, found);
            Debug.Log($"Saved MCP uvx override: {found}");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to save EditorPrefs: " + ex.Message);
        }
    }

    [MenuItem("Tools/UV/Clear UV MCP Override")]
    public static void ClearUvOverride()
    {
        if (EditorPrefs.HasKey(UvxPrefKey))
        {
            EditorPrefs.DeleteKey(UvxPrefKey);
            Debug.Log("Cleared MCP uvx override.");
        }
        else
        {
            Debug.Log("No MCP uvx override was set.");
        }
    }

    static string FindBestUvxPath()
    {
        // Preferred stable locations on Windows
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new string[] {
            Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "uvx.exe"),
            Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "uv.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet", "Links", "uvx.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet", "Links", "uv.exe"),
            // Project venv (if present)
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LoveSimulation_plan", ".venv", "Scripts", "uvx.exe"),
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LoveSimulation_plan", ".venv", "Scripts", "uv.exe")
        };

        foreach (var c in candidates)
            if (!string.IsNullOrEmpty(c) && File.Exists(c))
                return c;

        // Fallback: search PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var p1 = Path.Combine(dir, "uvx.exe");
                var p2 = Path.Combine(dir, "uv.exe");
                if (File.Exists(p1)) return p1;
                if (File.Exists(p2)) return p2;
            }
            catch { }
        }

        // Last resort: try `where` to resolve in environment
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where",
                Arguments = "uvx uv",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(2000);
                if (!string.IsNullOrEmpty(outp))
                {
                    var lines = outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var l in lines)
                        if (File.Exists(l)) return l;
                }
            }
        }
        catch { }

        return null;
    }
}
