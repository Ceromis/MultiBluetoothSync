using NAudio.Wave;

namespace MultiBluetoothSync;

public class SimpleVolumeProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public SimpleVolumeProvider(ISampleProvider source)
    {
        _source = source;
        Volume = 1.0f;
    }

    public float Volume { get; set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (Volume != 1.0f)
        {
            for (int i = offset; i < offset + read; i++)
            {
                buffer[i] *= Volume;
            }
        }
        return read;
    }
}
