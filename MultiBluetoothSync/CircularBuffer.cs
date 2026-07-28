namespace MultiBluetoothSync;

/// <summary>
/// Lock-free-ish circular buffer for audio data.
/// Uses a simple lock since we have 1 writer + 2 readers.
/// </summary>
public class CircularBuffer
{
    private readonly byte[] _buffer;
    private readonly int _capacity;
    private int _writePos;
    private int _bytesAvailable;
    private readonly object _lock = new();

    public CircularBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new byte[capacity];
    }

    public int Write(byte[] data, int offset, int count)
    {
        lock (_lock)
        {
            int written = 0;
            for (int i = 0; i < count; i++)
            {
                if (_bytesAvailable >= _capacity)
                {
                    // Buffer full — advance write pointer to drop oldest data
                    _writePos = (_writePos + 1) % _capacity;
                    _bytesAvailable--;
                }
                _buffer[_writePos] = data[offset + i];
                _writePos = (_writePos + 1) % _capacity;
                _bytesAvailable++;
                written++;
            }
            return written;
        }
    }

    public int Read(byte[] output, int offset, int count)
    {
        lock (_lock)
        {
            int toRead = Math.Min(count, _bytesAvailable);
            int readPos = (_writePos - _bytesAvailable + _capacity) % _capacity;
            for (int i = 0; i < toRead; i++)
            {
                output[offset + i] = _buffer[readPos];
                readPos = (readPos + 1) % _capacity;
            }
            _bytesAvailable -= toRead;
            return toRead;
        }
    }

    public int BytesAvailable
    {
        get { lock (_lock) { return _bytesAvailable; } }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _bytesAvailable = 0;
            _writePos = 0;
        }
    }
}
