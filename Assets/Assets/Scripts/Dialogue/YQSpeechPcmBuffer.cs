using System;

// note: The same bounded decoder serves Unity's network callback and detached protocol regressions.
public sealed class YQSpeechPcmBuffer
{
    public const int SampleRate = 24000;
    private readonly object _gate = new object();
    private readonly float[] _samples = new float[SampleRate * 120];
    private readonly byte[] _header = new byte[12];
    private readonly byte[] _length = new byte[4];
    private readonly byte[] _packet = new byte[96000];
    private int _headerCount, _lengthCount, _packetCount, _packetLength, _read, _write, _count;
    private bool _ended;
    private string _error;
    public int BufferedSamples { get { lock (_gate) return _count; } }
    public bool Ended { get { lock (_gate) return _ended; } }
    public string Error { get { lock (_gate) return _error; } }

    public bool Receive(byte[] bytes, int count)
    {
        lock (_gate)
        {
            if (_error != null || bytes == null || count < 0 || count > bytes.Length)
                return Fail("Invalid packet");
            int offset = 0;
            while (offset < count)
            {
                if (_ended) return Fail("Trailing audio data");
                if (_headerCount < 12)
                {
                    Copy(bytes, ref offset, count, _header, ref _headerCount, 12);
                    if (_headerCount < 12) continue;
                    if (_header[0] != 'Y' || _header[1] != 'Q' || _header[2] != 'S' || _header[3] != 'P' ||
                        !BitConverter.IsLittleEndian || BitConverter.ToUInt32(_header, 4) != SampleRate ||
                        BitConverter.ToUInt32(_header, 8) != 1)
                        return Fail("Unsupported speech format");
                }
                else if (_lengthCount < 4)
                {
                    Copy(bytes, ref offset, count, _length, ref _lengthCount, 4);
                    if (_lengthCount < 4) continue;
                    uint length = BitConverter.ToUInt32(_length, 0);
                    if (length == 0) { _ended = true; continue; }
                    if (length > _packet.Length || length % 4 != 0) return Fail("Invalid audio frame");
                    _packetLength = (int)length;
                }
                else
                {
                    Copy(bytes, ref offset, count, _packet, ref _packetCount, _packetLength);
                    if (_packetCount < _packetLength) continue;
                    if (_count + _packetLength / 4 > _samples.Length) return Fail("Speech buffer limit exceeded");
                    for (int i = 0; i < _packetLength; i += 4)
                    {
                        float value = BitConverter.ToSingle(_packet, i);
                        if (float.IsNaN(value) || float.IsInfinity(value)) return Fail("Non-finite audio");
                        _samples[_write] = Math.Max(-1f, Math.Min(1f, value));
                        _write = (_write + 1) % _samples.Length;
                        _count++;
                    }
                    _packetCount = _lengthCount = 0;
                }
            }
            return true;
        }
    }

    public void Read(float[] output)
    {
        // note: Unity's audio thread consumes preallocated memory and never waits for model inference.
        lock (_gate)
        {
            int take = Math.Min(output.Length, _count);
            int first = Math.Min(take, _samples.Length - _read);
            Buffer.BlockCopy(_samples, _read * 4, output, 0, first * 4);
            if (first < take) Buffer.BlockCopy(_samples, 0, output, first * 4, (take - first) * 4);
            _read = (_read + take) % _samples.Length;
            _count -= take;
            if (take < output.Length) Array.Clear(output, take, output.Length - take);
        }
    }

    public bool Complete()
    {
        lock (_gate) return _error == null && (_ended || Fail("Truncated speech stream"));
    }

    private bool Fail(string error) { _error = error; return false; }
    private static void Copy(byte[] source, ref int offset, int count, byte[] target, ref int filled, int limit)
    {
        int take = Math.Min(count - offset, limit - filled);
        Buffer.BlockCopy(source, offset, target, filled, take);
        offset += take;
        filled += take;
    }
}
