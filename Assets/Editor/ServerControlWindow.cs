 #if UNITY_EDITOR
 using UnityEditor;
 using UnityEngine;
 using System.Diagnostics;
 using System.IO;
 using System.Text;
 using System;
 using Debug = UnityEngine.Debug;

 public class ServerControlWindow : EditorWindow
 {
     private static Process serverProcess;
     private static StringBuilder outputBuilder = new StringBuilder();
     private Vector2 scroll;
     private bool autoOpen = false;
     private string deepgramApiKey = "";
     private bool showApiKey = false;
     private const string ApiKeyPrefKey = "LoveSim_DeepgramApiKey";

    // Persistent log file writers — same paths as manual runs (server_out.log / server_err.log)
    private static StreamWriter logOutWriter = null;
    private static StreamWriter logErrWriter = null;

     [MenuItem("LoveSim/Server Control")]
     public static void ShowWindow()
     {
         GetWindow<ServerControlWindow>("Server Control");
     }

     private void OnEnable()
     {
        EditorApplication.quitting += OnEditorQuitting;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        // Restore saved key
        deepgramApiKey = EditorPrefs.GetString(ApiKeyPrefKey, "");
        // Also check system env as fallback
        if (string.IsNullOrEmpty(deepgramApiKey))
            deepgramApiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY") ?? "";
     }

     private void OnDisable()
     {
        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
     }

     private void OnEditorQuitting()
     {
         StopServer();
     }

     void OnGUI()
     {
         GUILayout.Label("Local Python Server Control (Editor only)", EditorStyles.boldLabel);

         // --- API Key field ---
         GUILayout.BeginHorizontal();
         GUILayout.Label("DEEPGRAM_API_KEY:", GUILayout.Width(160));
         showApiKey = EditorGUILayout.Toggle("Show", showApiKey, GUILayout.Width(70));
         GUILayout.EndHorizontal();

         EditorGUI.BeginChangeCheck();
         string newKey = showApiKey
             ? EditorGUILayout.TextField(deepgramApiKey)
             : EditorGUILayout.PasswordField(deepgramApiKey);
         if (EditorGUI.EndChangeCheck())
         {
             deepgramApiKey = newKey;
             EditorPrefs.SetString(ApiKeyPrefKey, deepgramApiKey);
         }

         bool hasKey = !string.IsNullOrEmpty(deepgramApiKey);
         if (!hasKey)
             EditorGUILayout.HelpBox("API Key가 없으면 Deepgram 전사가 동작하지 않습니다.", MessageType.Warning);
         else
             EditorGUILayout.HelpBox($"Key 설정됨 (길이 {deepgramApiKey.Length})", MessageType.Info);

         GUILayout.Space(4);
         // ---------------------

         if (serverProcess != null && !serverProcess.HasExited)
         {
             GUILayout.Label($"Status: RUNNING (PID {serverProcess.Id})");
             if (GUILayout.Button("Stop Server")) StopServer();
         }
         else
         {
             GUILayout.Label("Status: stopped");
             if (GUILayout.Button("Start Server")) StartServer();
         }

         GUILayout.Space(8);
         autoOpen = EditorGUILayout.Toggle("Auto-open console output", autoOpen);

         GUILayout.Label("Server Output (tail):", EditorStyles.label);
         scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(240));
         GUILayout.TextArea(GetOutputTail(), GUILayout.ExpandHeight(true));
         GUILayout.EndScrollView();

         if (GUILayout.Button("Refresh Output")) Repaint();

         GUILayout.Space(6);
         if (GUILayout.Button("Open server.py location"))
         {
             var serverPath = GetServerScriptPath();
             if (File.Exists(serverPath)) EditorUtility.RevealInFinder(serverPath);
             else EditorUtility.DisplayDialog("Not found", serverPath + " not found", "OK");
         }
     }

     private static string GetProjectRoot()
     {
         return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
     }

     private static string GetPythonExecutablePath()
     {
         var root = GetProjectRoot();
         var venvRoot = Path.Combine(root, "LoveSimulation_plan", ".venv");
         var candidate = Path.Combine(venvRoot, "Scripts", "python.exe");
         var pyvenvCfg = Path.Combine(venvRoot, "pyvenv.cfg");

        // A valid venv on Windows must include pyvenv.cfg at the venv root.
        // However some committed venvs contain launchers that point to an
        // original Python installation (e.g. C:\Python314) which may not
        // exist on this machine. If candidate exists, attempt to run
        // `python --version` to validate it; otherwise fall back to PATH `python`.
        if (File.Exists(candidate) && File.Exists(pyvenvCfg))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc != null)
                    {
                        // wait briefly for the version output
                        if (proc.WaitForExit(2000) && proc.ExitCode == 0)
                        {
                            return candidate;
                        }
                        // fall through to fallback
                    }
                }
            }
            catch {
                // ignore and fall back to PATH python
            }
        }

        return "python"; // fallback to PATH python
     }

     private static string GetServerScriptPath()
     {
         var root = GetProjectRoot();
         return Path.Combine(root, "LoveSimulation_plan", "prototype", "episode1", "server.py");
     }

     private void StartServer()
     {
         if (serverProcess != null && !serverProcess.HasExited)
         {
             Debug.Log("Server already running.");
             return;
         }

         var python = GetPythonExecutablePath();
         var script = GetServerScriptPath();
         if (!File.Exists(script))
         {
             EditorUtility.DisplayDialog("Error", "server.py not found at: " + script, "OK");
             return;
         }

         // Surface a clear warning when .venv exists but is incomplete.
         var root = GetProjectRoot();
         var venvRoot = Path.Combine(root, "LoveSimulation_plan", ".venv");
         var venvPython = Path.Combine(venvRoot, "Scripts", "python.exe");
         var venvCfg = Path.Combine(venvRoot, "pyvenv.cfg");
         if (python == "python" && File.Exists(venvPython) && !File.Exists(venvCfg))
         {
             AppendOutput("WARNING: .venv is incomplete (pyvenv.cfg missing). Falling back to PATH python.");
         }

         var startInfo = new ProcessStartInfo
         {
             FileName = python,
             Arguments = $"\"{script}\"",
             UseShellExecute = false,
             CreateNoWindow = true,
             RedirectStandardOutput = true,
             RedirectStandardError = true,
             WorkingDirectory = Path.GetDirectoryName(script)
         };

         // propagate DEEPGRAM_API_KEY — prefer field value, fall back to system env
         try
         {
             var key = deepgramApiKey;
             if (string.IsNullOrEmpty(key))
                 key = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY");
             if (!string.IsNullOrEmpty(key))
             {
                 startInfo.EnvironmentVariables["DEEPGRAM_API_KEY"] = key;
                 AppendOutput($"DEEPGRAM_API_KEY injected (length {key.Length})");
             }
             else
             {
                 AppendOutput("WARNING: DEEPGRAM_API_KEY not set — Deepgram will be skipped");
             }
         }
         catch { }

         // Open persistent log files (same paths as manual runs)
         var episode1Dir = Path.GetDirectoryName(script);
         try
         {
             logOutWriter = new StreamWriter(Path.Combine(episode1Dir, "server_out.log"), append: true) { AutoFlush = true };
             logErrWriter = new StreamWriter(Path.Combine(episode1Dir, "server_err.log"), append: true) { AutoFlush = true };
             var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
             logOutWriter.WriteLine($"\n=== ServerControlWindow started at {stamp} ===");
             logErrWriter.WriteLine($"\n=== ServerControlWindow started at {stamp} ===");
         }
         catch (Exception ex)
         {
             Debug.LogWarning("ServerControlWindow: could not open log files: " + ex.Message);
         }

         try
         {
             serverProcess = new Process();
             serverProcess.StartInfo = startInfo;
             serverProcess.EnableRaisingEvents = true;
             serverProcess.OutputDataReceived += (s, e) =>
             {
                 if (string.IsNullOrEmpty(e.Data)) return;
                 AppendOutput(e.Data);
                 try { logOutWriter?.WriteLine(e.Data); } catch { }
             };
             serverProcess.ErrorDataReceived += (s, e) =>
             {
                 if (string.IsNullOrEmpty(e.Data)) return;
                 AppendOutput("ERR: " + e.Data);
                 try { logErrWriter?.WriteLine(e.Data); } catch { }
             };
             serverProcess.Exited += (s, e) =>
             {
                 var msg = $"Process exited (code {serverProcess.ExitCode})";
                 AppendOutput(msg);
                 try { logOutWriter?.WriteLine(msg); logOutWriter?.Close(); logOutWriter = null; } catch { }
                 try { logErrWriter?.Close(); logErrWriter = null; } catch { }
             };
             serverProcess.Start();
             serverProcess.BeginOutputReadLine();
             serverProcess.BeginErrorReadLine();
             AppendOutput($"Started server (PID {serverProcess.Id})\n");
             Repaint();
         }
         catch (System.Exception ex)
         {
             EditorUtility.DisplayDialog("Start Failed", ex.Message, "OK");
             Debug.LogException(ex);
         }
     }

     private static void StopServer()
     {
         try
         {
             if (serverProcess != null && !serverProcess.HasExited)
             {
                 AppendOutput("Stopping server...\n");
                 try { serverProcess.Kill(); } catch { serverProcess.Close(); }
             }
         }
         catch (System.Exception ex)
         {
             Debug.LogException(ex);
         }
         finally
         {
             serverProcess = null;
         }
     }

     private static void AppendOutput(string line)
     {
         lock (outputBuilder)
         {
             outputBuilder.AppendLine(line);
             // keep tail reasonably small
             if (outputBuilder.Length > 20000) outputBuilder.Remove(0, outputBuilder.Length - 20000);
         }
         EditorApplication.delayCall += () => { var w = GetWindow<ServerControlWindow>(); if (w != null) w.Repaint(); };
     }

     private string GetOutputTail()
     {
         lock (outputBuilder)
         {
             return outputBuilder.ToString();
         }
     }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        try
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                AppendOutput("Play mode entered — restarting server\n");
                // stop then start to ensure a fresh server process
                StopServer();
                StartServer();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                AppendOutput("Exiting play mode — stopping server\n");
                StopServer();
            }
               try { logOutWriter?.Close(); logOutWriter = null; } catch { }
               try { logErrWriter?.Close(); logErrWriter = null; } catch { }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
 }
 #endif