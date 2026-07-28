using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MultiBluetoothSync;

public class AudioRouter : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private WasapiOut? _leftOut;
    private WasapiOut? _rightOut;
    private BufferedWaveProvider? _leftBuf;
    private BufferedWaveProvider? _rightBuf;
    private volatile bool _isRunning;

    private float _volL = 1.0f;
    private float _volR = 1.0f;

    // Sync offset: >0 = delay RIGHT, <0 = delay LEFT
    private readonly object _delayLock = new();
    private byte[] _delayQueueL = Array.Empty<byte>();
    private byte[] _delayQueueR = Array.Empty<byte>();
    private int _delayBytesL;
    private int _delayBytesR;

    public bool IsActive => _isRunning;
    public event Action<string>? StatusChanged;
    public event Action<float, float>? LevelUpdated;

    public void Start(string leftDeviceId, string rightDeviceId, float leftVol, float rightVol, int bufferMs = 300)
    {
        if (_isRunning) Stop();

        _volL = leftVol;
        _volR = rightVol;

        var enumerator = new MMDeviceEnumerator();
        _capture = new WasapiLoopbackCapture();
        var wf = _capture.WaveFormat;

        var mono = WaveFormat.CreateIeeeFloatWaveFormat(wf.SampleRate, 1);
        var bufDur = TimeSpan.FromSeconds(2);

        _leftBuf = new BufferedWaveProvider(mono) { BufferDuration = bufDur, DiscardOnBufferOverflow = true };
        _rightBuf = new BufferedWaveProvider(mono) { BufferDuration = bufDur, DiscardOnBufferOverflow = true };

        var leftDevice = enumerator.GetDevice(leftDeviceId);
        var rightDevice = enumerator.GetDevice(rightDeviceId);

        _leftOut = new WasapiOut(leftDevice, AudioClientShareMode.Shared, true, bufferMs);
        _leftOut.Init(new VolProvider(_leftBuf.ToSampleProvider(), () => _volL));
        _leftOut.Play();

        _rightOut = new WasapiOut(rightDevice, AudioClientShareMode.Shared, true, bufferMs);
        _rightOut.Init(new VolProvider(_rightBuf.ToSampleProvider(), () => _volR));
        _rightOut.Play();

        _capture.DataAvailable += OnCapture;
        _capture.RecordingStopped += (s, e) => { if (_isRunning) StatusChanged?.Invoke("采集已停止"); };
        _capture.StartRecording();

        _isRunning = true;
        StatusChanged?.Invoke($"正在路由: {wf.SampleRate}Hz {wf.Channels}ch");
    }

    private void OnCapture(object? sender, WaveInEventArgs e)
    {
        if (!_isRunning || e.BytesRecorded == 0) return;

        var wf = _capture!.WaveFormat;
        int ch = wf.Channels;
        bool isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat;
        int bps = wf.BitsPerSample / 8;
        int total = e.BytesRecorded / (bps * ch);

        float sysVol = PollSystemVolume();

        byte[] monoL = new byte[total * 4];
        byte[] monoR = new byte[total * 4];

        if (isFloat)
        {
            for (int i = 0; i < total; i++)
            {
                int s = i * ch * 4;
                float l = BitConverter.ToSingle(e.Buffer, s) * sysVol;
                float r = ch >= 2 ? BitConverter.ToSingle(e.Buffer, s + 4) * sysVol : l;
                Buffer.BlockCopy(BitConverter.GetBytes(l), 0, monoL, i * 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(r), 0, monoR, i * 4, 4);
            }
        }
        else
        {
            for (int i = 0; i < total; i++)
            {
                int s = i * ch * 2;
                float l = BitConverter.ToInt16(e.Buffer, s) / 32768f * sysVol;
                float r = ch >= 2 ? BitConverter.ToInt16(e.Buffer, s + 2) / 32768f * sysVol : l;
                Buffer.BlockCopy(BitConverter.GetBytes(l), 0, monoL, i * 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(r), 0, monoR, i * 4, 4);
            }
        }

        // Write through delay queues
        PushDelayed(monoL, _delayBytesL, _leftBuf, ref _delayQueueL);
        PushDelayed(monoR, _delayBytesR, _rightBuf, ref _delayQueueR);

        float lLvl = CalcRms(monoL, total);
        float rLvl = CalcRms(monoR, total);
        LevelUpdated?.Invoke(lLvl, rLvl);
    }

    private void PushDelayed(byte[] data, int delayBytes, BufferedWaveProvider? target, ref byte[] queue)
    {
        lock (_delayLock)
        {
            if (delayBytes == 0)
            {
                // No delay — flush any remaining queue then write directly
                if (queue.Length > 0)
                {
                    target?.AddSamples(queue, 0, queue.Length);
                    queue = Array.Empty<byte>();
                }
                target?.AddSamples(data, 0, data.Length);
                return;
            }

            // Append to queue
            byte[] merged = new byte[queue.Length + data.Length];
            Buffer.BlockCopy(queue, 0, merged, 0, queue.Length);
            Buffer.BlockCopy(data, 0, merged, queue.Length, data.Length);
            queue = merged;

            // Flush data beyond the delay threshold
            if (queue.Length > delayBytes)
            {
                int flush = queue.Length - delayBytes;
                target?.AddSamples(queue, 0, flush);
                byte[] held = new byte[delayBytes];
                Buffer.BlockCopy(queue, flush, held, 0, delayBytes);
                queue = held;
            }
        }
    }

    /// <summary>
    /// Set sync offset. Positive = delay RIGHT channel, Negative = delay LEFT channel.
    /// </summary>
    public void SetSyncOffset(float ms)
    {
        lock (_delayLock)
        {
            int sampleRate = _capture?.WaveFormat.SampleRate ?? 48000;
            int bytesPerSec = sampleRate * 4; // mono float32

            if (ms > 0)
            {
                // Delay RIGHT
                _delayBytesR = (int)(bytesPerSec * ms / 1000f);
                _delayBytesL = 0;
                // Flush left queue
                if (_delayQueueL.Length > 0)
                {
                    _leftBuf?.AddSamples(_delayQueueL, 0, _delayQueueL.Length);
                    _delayQueueL = Array.Empty<byte>();
                }
            }
            else if (ms < 0)
            {
                // Delay LEFT
                _delayBytesL = (int)(bytesPerSec * (-ms) / 1000f);
                _delayBytesR = 0;
                // Flush right queue
                if (_delayQueueR.Length > 0)
                {
                    _rightBuf?.AddSamples(_delayQueueR, 0, _delayQueueR.Length);
                    _delayQueueR = Array.Empty<byte>();
                }
            }
            else
            {
                // No delay — flush both
                _delayBytesL = 0;
                _delayBytesR = 0;
                if (_delayQueueL.Length > 0)
                {
                    _leftBuf?.AddSamples(_delayQueueL, 0, _delayQueueL.Length);
                    _delayQueueL = Array.Empty<byte>();
                }
                if (_delayQueueR.Length > 0)
                {
                    _rightBuf?.AddSamples(_delayQueueR, 0, _delayQueueR.Length);
                    _delayQueueR = Array.Empty<byte>();
                }
            }
        }
    }

    // Poll system volume every ~50ms
    private MMDeviceEnumerator? _volEnum;
    private float _cachedSysVol = 1.0f;
    private int _volPollCounter;

    private float PollSystemVolume()
    {
        if (++_volPollCounter % 10 != 0) return _cachedSysVol;
        try
        {
            _volEnum ??= new MMDeviceEnumerator();
            var dev = _volEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _cachedSysVol = dev.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        catch { }
        return _cachedSysVol;
    }

    private static float CalcRms(byte[] buf, int count)
    {
        float sum = 0;
        for (int i = 0; i < count; i++)
        {
            float s = BitConverter.ToSingle(buf, i * 4);
            sum += s * s;
        }
        return MathF.Sqrt(sum / Math.Max(count, 1));
    }

    public void UpdateVolumes(float leftVol, float rightVol)
    {
        _volL = leftVol;
        _volR = rightVol;
    }

    public void Stop()
    {
        _isRunning = false;
        try { _capture?.StopRecording(); } catch { }
        try { _leftOut?.Stop(); } catch { }
        try { _rightOut?.Stop(); } catch { }
        try { _capture?.Dispose(); } catch { }
        try { _leftOut?.Dispose(); } catch { }
        try { _rightOut?.Dispose(); } catch { }
        _capture = null; _leftOut = null; _rightOut = null;
        _leftBuf = null; _rightBuf = null;
        lock (_delayLock)
        {
            _delayQueueL = Array.Empty<byte>();
            _delayQueueR = Array.Empty<byte>();
        }
        StatusChanged?.Invoke("已停止");
    }

    public void Dispose() => Stop();
}

internal class VolProvider : ISampleProvider
{
    private readonly ISampleProvider _src;
    private readonly Func<float> _getVol;

    public VolProvider(ISampleProvider src, Func<float> getVol)
    {
        _src = src;
        _getVol = getVol;
    }

    public WaveFormat WaveFormat => _src.WaveFormat;

    public int Read(float[] buf, int off, int cnt)
    {
        int n = _src.Read(buf, off, cnt);
        float v = _getVol();
        if (v < 0.999f || v > 1.001f)
        {
            for (int i = off; i < off + n; i++)
                buf[i] *= v;
        }
        return n;
    }
}
