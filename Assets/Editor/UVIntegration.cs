using System.IO;
using UnityEditor;
using UnityEngine;

public static class UVIntegration
{
    [MenuItem("Tools/UV/Check UV Version")]
    public static void CheckUVVersion()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string[] candidates = new string[] {
            Path.Combine(projectRoot, "LoveSimulation_plan", ".venv", "Scripts", "uv.exe"),
            Path.Combine(projectRoot, "LoveSimulation_plan", ".venv", "Scripts", "uvx.exe"),
            "uv"
        };

        string uvPath = null;
        foreach (var c in candidates)
        {
            if (File.Exists(c))
            {
                uvPath = c;
                break;
            }
        }

        if (uvPath == null)
            uvPath = "uv";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = uvPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var p = System.Diagnostics.Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log("uv: " + stdout.Trim());
                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning("uv (stderr): " + stderr.Trim());
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Failed to run uv: " + ex.Message);
        }
    }
}
