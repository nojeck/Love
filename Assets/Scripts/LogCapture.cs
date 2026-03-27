using UnityEngine;
using System.IO;
using System.Text;

/// <summary>
/// Unity 게임 실행 중 발생하는 모든 로그를 파일로 저장하는 유틸리티
/// </summary>
public class LogCapture : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("로그 파일명 (타임스탬프 자동 추가됨)")]
    public string logFileName = "game_log";
    
    [Tooltip("로그 파일 저장 폴더 (빈 값이면 persistentDataPath 사용)")]
    public string customLogPath = "";
    
    [Tooltip("로그 레벨 필터")]
    public bool captureNormalLogs = true;
    public bool captureWarnings = true;
    public bool captureErrors = true;
    
    [Tooltip("게임 시작 시 기존 로그 파일 삭제")]
    public bool clearOnStart = false;
    
    [Tooltip("콘솔에도 로그 출력")]
    public bool alsoPrintToConsole = true;

    private string logFilePath;
    private StringBuilder logBuffer;
    private StreamWriter writer;
    private bool isInitialized = false;

    public static LogCapture Instance { get; private set; }

    void Awake()
    {
        // 싱글톤 패턴
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeLogFile();
    }

    void InitializeLogFile()
    {
        // 로그 파일 경로 설정
        string basePath = string.IsNullOrEmpty(customLogPath) 
            ? Application.persistentDataPath 
            : customLogPath;
        
        // 디렉토리가 없으면 생성
        if (!Directory.Exists(basePath))
        {
            Directory.CreateDirectory(basePath);
        }
        
        // 타임스탬프가 포함된 파일명 생성
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(basePath, $"{logFileName}_{timestamp}.log");
        
        // 기존 파일 삭제 옵션
        if (clearOnStart && File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }
        
        // 로그 버퍼 초기화
        logBuffer = new StringBuilder();
        
        // 파일 헤더 작성
        string header = $"=== Unity Game Log ===\n" +
                       $"Created: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       $"Unity Version: {Application.unityVersion}\n" +
                       $"Platform: {Application.platform}\n" +
                       $"Device: {SystemInfo.deviceModel}\n" +
                       $"{"=".Repeat(50)}\n\n";
        
        File.WriteAllText(logFilePath, header);
        
        // 로그 이벤트 등록
        Application.logMessageReceived += OnLogMessageReceived;
        
        isInitialized = true;
        
        Debug.Log($"[LogCapture] 로그 파일 생성됨: {logFilePath}");
    }

    void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (!isInitialized) return;
        
        // 로그 타입 필터링
        switch (type)
        {
            case LogType.Log:
                if (!captureNormalLogs) return;
                break;
            case LogType.Warning:
                if (!captureWarnings) return;
                break;
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                if (!captureErrors) return;
                break;
        }
        
        // 로그 포맷팅
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        string typeStr = type.ToString().ToUpper().PadRight(9);
        string logEntry = $"[{timestamp}] [{typeStr}] {condition}";
        
        // 스택 트레이스는 에러/익셉션만 포함
        if (type == LogType.Error || type == LogType.Exception)
        {
            logEntry += $"\nStack Trace:\n{stackTrace}\n";
        }
        
        // 파일에 즉시 작성
        try
        {
            File.AppendAllText(logFilePath, logEntry + "\n");
        }
        catch (System.Exception e)
        {
            if (alsoPrintToConsole)
            {
                Debug.LogWarning($"[LogCapture] 파일 쓰기 실패: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 커스텀 로그 메시지 추가
    /// </summary>
    public void Log(string message, LogType type = LogType.Log)
    {
        if (!isInitialized) return;
        
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
        string typeStr = type.ToString().ToUpper().PadRight(9);
        string logEntry = $"[{timestamp}] [{typeStr}] [CUSTOM] {message}";
        
        File.AppendAllText(logFilePath, logEntry + "\n");
        
        if (alsoPrintToConsole)
        {
            switch (type)
            {
                case LogType.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogType.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }

    /// <summary>
    /// 현재 로그 파일 경로 반환
    /// </summary>
    public string GetLogFilePath()
    {
        return logFilePath;
    }

    /// <summary>
    /// 로그 파일 내용 반환
    /// </summary>
    public string GetLogContent()
    {
        if (File.Exists(logFilePath))
        {
            return File.ReadAllText(logFilePath);
        }
        return "";
    }

    /// <summary>
    /// 로그 파일을 지정된 경로로 복사
    /// </summary>
    public bool CopyLogTo(string destinationPath)
    {
        try
        {
            if (File.Exists(logFilePath))
            {
                File.Copy(logFilePath, destinationPath, true);
                Debug.Log($"[LogCapture] 로그 파일 복사됨: {destinationPath}");
                return true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LogCapture] 복사 실패: {e.Message}");
        }
        return false;
    }

    void OnDestroy()
    {
        // 이벤트 해제
        Application.logMessageReceived -= OnLogMessageReceived;
        
        // 종료 로그 작성
        if (isInitialized && File.Exists(logFilePath))
        {
            string footer = $"\n{"=".Repeat(50)}\n" +
                           $"Session ended: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            File.AppendAllText(logFilePath, footer);
        }
    }

    void OnApplicationQuit()
    {
        // 앱 종료 시 로그 파일 경로 출력
        Debug.Log($"[LogCapture] 로그 파일 저장 위치: {logFilePath}");
    }
}

// 문자열 확장 메서드
public static class StringExtensions
{
    public static string Repeat(this string s, int count)
    {
        var sb = new StringBuilder(s.Length * count);
        for (int i = 0; i < count; i++)
            sb.Append(s);
        return sb.ToString();
    }
}
