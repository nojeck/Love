using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Phase 7.3: 자동 녹음 컨트롤러
/// 
/// 타임 리미트 기반 자동 녹음 시스템
/// - 첫 마디 10초 제한
/// - 최대 30초 녹음
/// - 음성 감지 시 자동 녹음 시작
/// - 침묵 시 자동 종료
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AutoRecordingController : MonoBehaviour
{
    #region Settings
    
    [Header("Timer Settings")]
    [Tooltip("첫 마디 응답 제한 시간 (초)")]
    public float firstResponseLimit = 10f;
    
    [Tooltip("최대 녹음 시간 (초)")]
    public float maxRecordingTime = 30f;
    
    [Tooltip("음성 감지 임계값 (RMS)")]
    public float voiceThreshold = 0.02f;
    
    [Tooltip("침묵 지속 시간 (초) - 이 시간 동안 침묵이면 녹음 종료")]
    public float silenceDuration = 2f;
    
    [Header("Microphone Settings")]
    [Tooltip("마이크 장치 인덱스 (-1: 기본)")]
    public int microphoneDeviceIndex = -1;
    
    [Tooltip("샘플 레이트")]
    public int sampleRate = 16000;
    
    [Tooltip("버퍼 길이")]
    public int bufferLength = 1;
    
    #endregion
    
    #region UI References
    
    [Header("Timer UI")]
    [Tooltip("타이머 프로그레스 바")]
    public Slider timerBar;
    
    [Tooltip("타이머 텍스트 (초)")]
    public TMP_Text timerText;
    
    [Tooltip("상태 라벨")]
    public TMP_Text statusLabel;
    
    [Header("Recording UI")]
    [Tooltip("녹음 인디케이터")]
    public GameObject recordingIndicator;
    
    [Tooltip("녹음 시간 표시")]
    public TMP_Text recordingTimeText;
    
    [Tooltip("마이크 아이콘")]
    public Image micIcon;
    
    [Tooltip("파형 표시")]
    public Image waveformDisplay;
    
    [Header("Server Config")]
    [Tooltip("서버 URL")]
    public string serverUrl = "http://127.0.0.1:5000";
    
    #endregion
    
    #region Events
    
    [Header("Events")]
    [Tooltip("녹음 모드 시작")]
    public UnityEvent OnRecordingModeStart;
    
    [Tooltip("녹음 시작")]
    public UnityEvent OnRecordingStart;
    
    [Tooltip("녹음 완료 (지속 시간)")]
    public UnityEvent<float, AudioClip> OnRecordingComplete;
    
    [Tooltip("침묵 타임아웃 (10초 초과)")]
    public UnityEvent OnSilenceTimeout;
    
    [Tooltip("최대 시간 초과 (30초 초과)")]
    public UnityEvent<float> OnMaxTimeExceeded;
    
    [Tooltip("상태 변경")]
    public UnityEvent<RecordingState> OnStateChanged;
    
    #endregion
    
    #region State
    
    public enum RecordingState
    {
        Idle,           // 대기
        WaitingVoice,   // 음성 대기 (10초 타이머)
        Recording,      // 녹음 중
        Processing      // 분석 중
    }
    
    private RecordingState currentState = RecordingState.Idle;
    private float stateTimer = 0f;
    private float recordingDuration = 0f;
    private float silenceTimer = 0f;
    
    private AudioClip recordedClip;
    private string microphoneDevice;
    private bool isMicrophoneInitialized = false;
    
    private float[] sampleBuffer;
    private const int SAMPLE_BUFFER_SIZE = 1024;
    
    #endregion
    
    #region Properties
    
    public RecordingState CurrentState => currentState;
    public float CurrentTimer => stateTimer;
    public float RecordingDuration => recordingDuration;
    
    #endregion
    
    #region Unity Lifecycle
    
    void Awake()
    {
        // 버퍼 초기화
        sampleBuffer = new float[SAMPLE_BUFFER_SIZE];
        
        // 마이크 장치 설정
        if (Microphone.devices.Length > 0)
        {
            if (microphoneDeviceIndex >= 0 && microphoneDeviceIndex < Microphone.devices.Length)
            {
                microphoneDevice = Microphone.devices[microphoneDeviceIndex];
            }
            else
            {
                microphoneDevice = Microphone.devices[0];
            }
            Debug.Log($"[AutoRecording] Microphone device: {microphoneDevice}");
        }
        else
        {
            Debug.LogError("[AutoRecording] No microphone device found!");
        }
    }
    
    void Start()
    {
        InitializeUI();
    }
    
    void Update()
    {
        UpdateStateMachine();
    }
    
    void OnDestroy()
    {
        StopAllCoroutines();
        
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
    }
    
    #endregion
    
    #region Initialization
    
    private void InitializeUI()
    {
        // 타이머 숨김
        if (timerBar != null)
            timerBar.gameObject.SetActive(false);
        
        // 녹음 인디케이터 숨김
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        
        // 상태 라벨
        if (statusLabel != null)
            statusLabel.text = "대기 중";
    }
    
    #endregion
    
    #region State Machine
    
    private void UpdateStateMachine()
    {
        switch (currentState)
        {
            case RecordingState.Idle:
                // 대기 상태 - 아무것도 안 함
                break;
                
            case RecordingState.WaitingVoice:
                UpdateWaitingVoice();
                break;
                
            case RecordingState.Recording:
                UpdateRecording();
                break;
                
            case RecordingState.Processing:
                // 분석 중 - 대기
                break;
        }
    }
    
    private void UpdateWaitingVoice()
    {
        // 타이머 업데이트
        stateTimer += Time.deltaTime;
        UpdateTimerDisplay(stateTimer, firstResponseLimit, false);
        
        // 음성 감지
        if (DetectVoice())
        {
            // 음성 감지 → 녹음 시작
            StartRecording();
            return;
        }
        
        // 10초 초과 체크
        if (stateTimer >= firstResponseLimit)
        {
            HandleSilenceTimeout();
        }
    }
    
    private void UpdateRecording()
    {
        // 녹음 시간 업데이트
        stateTimer += Time.deltaTime;
        recordingDuration = stateTimer;
        UpdateRecordingDisplay(recordingDuration);
        
        // 30초 초과 체크
        if (stateTimer >= maxRecordingTime)
        {
            HandleMaxTimeExceeded();
            return;
        }
        
        // 침묵 감지
        if (!DetectVoice())
        {
            silenceTimer += Time.deltaTime;
            
            if (silenceTimer >= silenceDuration)
            {
                // 2초 침묵 → 녹음 종료
                StopRecordingAndAnalyze();
            }
        }
        else
        {
            silenceTimer = 0f;
        }
    }
    
    private void ChangeState(RecordingState newState)
    {
        var oldState = currentState;
        currentState = newState;
        stateTimer = 0f;
        silenceTimer = 0f;
        
        Debug.Log($"[AutoRecording] State changed: {oldState} → {newState}");
        OnStateChanged?.Invoke(newState);
    }
    
    #endregion
    
    #region Voice Detection
    
    private bool DetectVoice()
    {
        if (!isMicrophoneInitialized || !Microphone.IsRecording(microphoneDevice))
            return false;
        
        // 마이크 데이터 가져오기
        int position = Microphone.GetPosition(microphoneDevice);
        if (position < 0 || recordedClip == null)
            return false;
        
        // 샘플 읽기
        recordedClip.GetData(sampleBuffer, position - SAMPLE_BUFFER_SIZE);
        
        // RMS 계산
        float rms = CalculateRMS(sampleBuffer);
        
        // 파형 표시 업데이트
        UpdateWaveform(rms);
        
        return rms > voiceThreshold;
    }
    
    private float CalculateRMS(float[] samples)
    {
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }
        return Mathf.Sqrt(sum / samples.Length);
    }
    
    #endregion
    
    #region Recording Control
    
    /// <summary>
    /// 녹음 모드 시작 (NPC 대화 완료 시 호출)
    /// </summary>
    public void StartRecordingMode()
    {
        Debug.Log("[AutoRecording] Starting recording mode...");
        
        // 상태 전환
        ChangeState(RecordingState.WaitingVoice);
        
        // 마이크 초기화 (녹음 시작)
        InitializeMicrophone();
        
        // UI 업데이트
        if (timerBar != null)
        {
            timerBar.gameObject.SetActive(true);
            timerBar.maxValue = firstResponseLimit;
            timerBar.value = 0f;
        }
        
        if (statusLabel != null)
            statusLabel.text = "응답 대기 중...";
        
        // 이벤트
        OnRecordingModeStart?.Invoke();
    }
    
    private void InitializeMicrophone()
    {
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            Debug.LogError("[AutoRecording] No microphone device available!");
            return;
        }
        
        // 마이크 녹음 시작 (루프)
        recordedClip = Microphone.Start(microphoneDevice, true, bufferLength, sampleRate);
        isMicrophoneInitialized = true;
        
        Debug.Log($"[AutoRecording] Microphone initialized: {microphoneDevice}, {sampleRate}Hz");
    }
    
    private void StartRecording()
    {
        Debug.Log("[AutoRecording] Voice detected! Starting recording...");
        
        // 상태 전환
        ChangeState(RecordingState.Recording);
        recordingDuration = 0f;
        
        // UI 업데이트
        if (timerBar != null)
            timerBar.gameObject.SetActive(false);
        
        if (recordingIndicator != null)
            recordingIndicator.SetActive(true);
        
        if (statusLabel != null)
            statusLabel.text = "녹음 중...";
        
        // 이벤트
        OnRecordingStart?.Invoke();
    }
    
    private void StopRecordingAndAnalyze()
    {
        Debug.Log($"[AutoRecording] Recording stopped. Duration: {recordingDuration:F1}s");
        
        // 상태 전환
        ChangeState(RecordingState.Processing);
        
        // 마이크 정지
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        // UI 업데이트
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        
        if (statusLabel != null)
            statusLabel.text = "분석 중...";
        
        // 녹음된 오디오 추출
        AudioClip finalClip = ExtractRecordedAudio();
        
        // 이벤트
        OnRecordingComplete?.Invoke(recordingDuration, finalClip);
    }
    
    private AudioClip ExtractRecordedAudio()
    {
        if (recordedClip == null)
            return null;
        
        // 실제 녹음된 길이만큼의 샘플 추출
        int sampleCount = Mathf.RoundToInt(recordingDuration * sampleRate);
        float[] samples = new float[sampleCount];
        
        recordedClip.GetData(samples, 0);
        
        // 새 클립 생성
        AudioClip clip = AudioClip.Create("RecordedAudio", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        
        return clip;
    }
    
    #endregion
    
    #region Timeout Handlers
    
    private void HandleSilenceTimeout()
    {
        Debug.Log("[AutoRecording] Silence timeout! (10s exceeded)");
        
        // 상태 전환
        ChangeState(RecordingState.Processing);
        
        // 마이크 정지
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        // UI 업데이트
        if (timerBar != null)
            timerBar.gameObject.SetActive(false);
        
        if (statusLabel != null)
            statusLabel.text = "시간 초과...";
        
        // 이벤트
        OnSilenceTimeout?.Invoke();
    }
    
    private void HandleMaxTimeExceeded()
    {
        Debug.Log("[AutoRecording] Max time exceeded! (30s)");
        
        // 상태 전환
        ChangeState(RecordingState.Processing);
        
        // 마이크 정지
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        // UI 업데이트
        if (recordingIndicator != null)
            recordingIndicator.SetActive(false);
        
        if (statusLabel != null)
            statusLabel.text = "발화 시간 초과...";
        
        // 이벤트
        OnMaxTimeExceeded?.Invoke(recordingDuration);
    }
    
    #endregion
    
    #region UI Updates
    
    private void UpdateTimerDisplay(float current, float max, bool countUp)
    {
        if (timerBar != null)
        {
            timerBar.value = countUp ? current : (max - current);
        }
        
        if (timerText != null)
        {
            float displayTime = countUp ? current : (max - current);
            timerText.text = Mathf.CeilToInt(displayTime).ToString();
        }
    }
    
    private void UpdateRecordingDisplay(float duration)
    {
        if (recordingTimeText != null)
        {
            int minutes = Mathf.FloorToInt(duration / 60f);
            int seconds = Mathf.FloorToInt(duration % 60f);
            recordingTimeText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    private void UpdateWaveform(float rms)
    {
        if (waveformDisplay != null)
        {
            // 파형 크기 조절 (RMS 기반)
            float scale = Mathf.Clamp01(rms * 10f);
            waveformDisplay.transform.localScale = new Vector3(scale, 1f, 1f);
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// 녹음 모드 강제 종료
    /// </summary>
    public void CancelRecording()
    {
        Debug.Log("[AutoRecording] Recording cancelled");
        
        // 마이크 정지
        if (Microphone.IsRecording(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        // 상태 초기화
        ChangeState(RecordingState.Idle);
        
        // UI 초기화
        InitializeUI();
    }
    
    /// <summary>
    /// Idle 상태로 복귀
    /// </summary>
    public void ReturnToIdle()
    {
        ChangeState(RecordingState.Idle);
        InitializeUI();
    }
    
    #endregion
}
