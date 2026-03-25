using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class UnityMicRecorder : MonoBehaviour
{
    public string uploadUrl = "http://127.0.0.1:5000/analyze";
    public int sampleRate = 16000;
    private AudioClip recording;
    // optional: device name to record from (null = default)
    public string deviceName = null;
    private string activeDeviceName = null;
    private bool isRecording = false;
    private byte[] lastWav = null;

    // Callback invoked with the raw server response string when upload finishes
    public Action<string> OnServerResponse;
    // Callback invoked with status updates (e.g., "Recording", "Uploading", "Error: ...")
    public Action<string> OnStatus;

    public bool IsRecording { get { return isRecording; } }

    // whether we have a saved recording available to save/upload
    public bool HasRecording { get { return lastWav != null; } }
    public void SetDevice(string name)
    {
        deviceName = string.IsNullOrEmpty(name) ? null : name;
        Debug.Log($"UnityMicRecorder: device set to '{deviceName}'");
    }

    void Awake()
    {
        Debug.Log($"UnityMicRecorder Awake: uploadUrl={uploadUrl} sampleRate={sampleRate}");
    }

    public void StartRecording()
    {
        if (isRecording) return;
        Debug.Log("StartRecording called");
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone devices found.");
            OnStatus?.Invoke("No microphone device");
            return;
        }

        // Prefer the user-selected device when it exists, otherwise use default (null).
        string dev = deviceName;
        if (!string.IsNullOrEmpty(dev) && System.Array.IndexOf(Microphone.devices, dev) < 0)
        {
            Debug.LogWarning("Requested device not available: " + dev + ", using default.");
            dev = null;
        }

        recording = Microphone.Start(dev, false, 10, sampleRate);
        activeDeviceName = dev;
        OnStatus?.Invoke("Recording");
        isRecording = true;
    }

    public void StopAndSend()
    {
        if (!isRecording) return;
        Debug.Log("StopAndSend called");
        int length = Microphone.GetPosition(activeDeviceName);
        Microphone.End(activeDeviceName);
        activeDeviceName = null;
        isRecording = false;

        if (length <= 0)
        {
            Debug.LogWarning("No audio captured.");
            OnStatus?.Invoke("No audio captured");
            OnServerResponse?.Invoke("ERROR_NO_AUDIO");
            return;
        }

        float[] samples = new float[recording.samples * recording.channels];
        recording.GetData(samples, 0);
        float[] clipped = new float[length * recording.channels];
        Array.Copy(samples, clipped, clipped.Length);

        byte[] wav = ConvertToWav(clipped, recording.channels, sampleRate);
        // keep a copy for local saving
        lastWav = wav;
        OnStatus?.Invoke("Uploading");
        StartCoroutine(UploadWav(wav));
    }

    // Save last recorded wav to disk. Returns saved path or null on failure.
    public string SaveLastRecording(string fileName = null)
    {
        if (lastWav == null)
        {
            Debug.LogWarning("No recording available to save.");
            return null;
        }

        try
        {
            string folder = System.IO.Path.Combine(Application.dataPath, "..", "LoveSimulation_plan", "prototype", "episode1", "sample_wavs");
            if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
            if (string.IsNullOrEmpty(fileName)) fileName = "recording_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
            string path = System.IO.Path.Combine(folder, fileName);
            System.IO.File.WriteAllBytes(path, lastWav);
            Debug.Log("Saved recording to: " + path);
            // use specified device if available
            string dev = deviceName;
            if (!string.IsNullOrEmpty(dev))
            {
                var devices = Microphone.devices;
                if (devices == null || System.Array.IndexOf(devices, dev) < 0)
                {
                    Debug.LogWarning("Requested device not available: " + dev + ", using default.");
                    dev = null;
                }
            }
            recording = Microphone.Start(dev, false, 10, sampleRate);
            OnStatus?.Invoke("Recording");
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to save recording: " + ex.Message);
            return null;
        }
    }

    private IEnumerator UploadWav(byte[] wavData)
    {
        string clientReqId = Guid.NewGuid().ToString("N");
        Debug.Log($"UploadWav: sending {wavData.Length} bytes to {uploadUrl}, client_req_id={clientReqId}");
        OnStatus?.Invoke("Uploading: sending to server");
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        form.AddField("client_req_id", clientReqId);
        Debug.Log($"UploadWav: form fields: file (audio.wav) size={wavData.Length}, client_req_id={clientReqId}");
        using (UnityWebRequest www = UnityWebRequest.Post(uploadUrl, form))
        {
            www.timeout = 120;  // 120초 타임아웃 설정 (Deepgram 호출 대기용)
            var op = www.SendWebRequest();
            float lastProgressTime = Time.realtimeSinceStartup;
            while (!op.isDone)
            {
                float p = www.uploadProgress; // 0..1 or -1 if unknown
                if (p >= 0f)
                    OnStatus?.Invoke($"Uploading: {Mathf.RoundToInt(p * 100f)}%");
                else
                    OnStatus?.Invoke("Uploading...");
                
                // 진행률 로그 (5초마다)
                float currentTime = Time.realtimeSinceStartup;
                if (currentTime - lastProgressTime > 5f)
                {
                    Debug.Log($"UploadWav: still uploading... progress={p:F2}, elapsed={currentTime - lastProgressTime:F1}s");
                    lastProgressTime = currentTime;
                }
                yield return null;
            }

            // Log response code and headers for debugging
            try
            {
                long code = www.responseCode;
                Debug.Log($"UploadWav: responseCode={code}");
                var respHeaders = www.GetResponseHeaders();
                if (respHeaders != null)
                {
                    foreach (var kv in respHeaders)
                    {
                        Debug.Log($"UploadWav: resp header: {kv.Key} = {kv.Value}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("UploadWav: failed to read response headers: " + ex.Message);
            }

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Upload failed: " + www.error);
                OnStatus?.Invoke("Upload failed: " + www.error);
                OnServerResponse?.Invoke("ERROR: " + www.error);
            }
            else
            {
                string text = www.downloadHandler.text;
                Debug.Log("Server response received: " + text.Substring(0, Mathf.Min(200, text.Length)));
                // if the response is unexpectedly empty, write additional debug info
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning("UploadWav: server returned empty body — check server logs and deepgram_logs directory");
                }
                OnStatus?.Invoke("Server responded");
                OnServerResponse?.Invoke(www.downloadHandler.text);
                // clear to idle shortly after response
                OnStatus?.Invoke("Idle");
            }
        }
    }

    // Convert float samples to 16-bit WAV byte array
    private byte[] ConvertToWav(float[] samples, int channels, int sampleRate)
    {
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];
        const float rescaleFactor = 32767; // to convert float to Int16

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            bytesData[i * 2] = byteArr[0];
            bytesData[i * 2 + 1] = byteArr[1];
        }

        MemoryStream stream = new MemoryStream();
        // RIFF header
        stream.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4);
        stream.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"), 0, 4);
        // fmt subchunk
        stream.Write(System.Text.Encoding.UTF8.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4); // subchunk1 size
        stream.Write(BitConverter.GetBytes((short)1), 0, 2); // audio format (1 = PCM)
        stream.Write(BitConverter.GetBytes((short)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
        stream.Write(BitConverter.GetBytes(sampleRate * channels * 2), 0, 4); // byte rate
        stream.Write(BitConverter.GetBytes((short)(channels * 2)), 0, 2); // block align
        stream.Write(BitConverter.GetBytes((short)16), 0, 2); // bits per sample
        // data subchunk
        stream.Write(System.Text.Encoding.UTF8.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
        stream.Write(bytesData, 0, bytesData.Length);

        return stream.ToArray();
    }
}
