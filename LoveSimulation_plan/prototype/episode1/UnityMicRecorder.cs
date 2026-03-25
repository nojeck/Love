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
    private bool isRecording = false;

    public void StartRecording()
    {
        if (isRecording) return;
        recording = Microphone.Start(null, false, 10, sampleRate);
        isRecording = true;
    }

    public void StopAndSend()
    {
        if (!isRecording) return;
        int length = Microphone.GetPosition(null);
        Microphone.End(null);
        isRecording = false;

        if (length <= 0)
        {
            Debug.LogWarning("No audio captured.");
            return;
        }

        float[] samples = new float[recording.samples * recording.channels];
        recording.GetData(samples, 0);
        float[] clipped = new float[length * recording.channels];
        Array.Copy(samples, clipped, clipped.Length);

        byte[] wav = ConvertToWav(clipped, recording.channels, sampleRate);
        StartCoroutine(UploadWav(wav));
    }

    private IEnumerator UploadWav(byte[] wavData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        using (UnityWebRequest www = UnityWebRequest.Post(uploadUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Upload failed: " + www.error);
            }
            else
            {
                Debug.Log("Server response: " + www.downloadHandler.text);
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
