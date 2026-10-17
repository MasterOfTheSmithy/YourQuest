    internal static bool JsonEqualsCaptured(object value, string capturedJson, Newtonsoft.Json.JsonSerializerSettings settings)
    {
        // note: Preserve complete serializer traversal and exact ordinal output equality without allocating another complete document string.
        var serializer = Newtonsoft.Json.JsonSerializer.CreateDefault(settings);
        using (var sink = new CapturedJsonComparisonWriter(capturedJson))
        {
            using (var writer = new Newtonsoft.Json.JsonTextWriter(sink) { Formatting = serializer.Formatting, CloseOutput = false })
            {
                serializer.Serialize(writer, value);
                writer.Flush();
            }
            return sink.MatchesSnapshot;
        }
    }

    private sealed class CapturedJsonComparisonWriter : System.IO.TextWriter
    {
        private readonly string captured;
        private readonly char[] comparisonBuffer;
        private int buffered;
        private long position;
        private bool matches;

        internal CapturedJsonComparisonWriter(string captured)
        {
            this.captured = captured;
            matches = captured != null;
            comparisonBuffer = new char[Math.Min(4096, Math.Max(16, captured != null ? captured.Length : 16))];
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.Unicode;
        public override IFormatProvider FormatProvider => System.Globalization.CultureInfo.InvariantCulture;
        internal bool MatchesSnapshot
        {
            get { Flush(); return matches && position == captured.Length; }
        }

        public override void Write(char value)
        {
            if (!matches) return;
            comparisonBuffer[buffered++] = value;
            if (buffered == comparisonBuffer.Length) Flush();
        }

        public override void Write(string value)
        {
            if (value == null || !matches) return;
            // note: Large immutable string chunks compare directly; short numeric/property writes share one bounded comparison buffer.
            if (buffered == 0 && value.Length >= comparisonBuffer.Length)
            {
                if (position > captured.Length || value.Length > captured.Length - position ||
                    string.CompareOrdinal(value, 0, captured, (int)position, value.Length) != 0) matches = false;
                position += value.Length;
                return;
            }
            int offset = 0;
            while (offset < value.Length && matches)
            {
                int count = Math.Min(comparisonBuffer.Length - buffered, value.Length - offset);
                value.CopyTo(offset, comparisonBuffer, buffered, count);
                buffered += count; offset += count;
                if (buffered == comparisonBuffer.Length) Flush();
            }
        }

        public override void Write(char[] buffer, int index, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - index < count) throw new ArgumentException("The buffer range is invalid.");
            // note: A mismatch discards output, not serializer traversal; later getters/converters and their errors still run.
            while (count > 0 && matches)
            {
                int copy = Math.Min(comparisonBuffer.Length - buffered, count);
                new ReadOnlySpan<char>(buffer, index, copy).CopyTo(new Span<char>(comparisonBuffer, buffered, copy));
                buffered += copy; index += copy; count -= copy;
                if (buffered == comparisonBuffer.Length) Flush();
            }
        }

        public override void Flush()
        {
            if (buffered == 0) return;
            if (matches && (position > captured.Length || buffered > captured.Length - position ||
                !System.MemoryExtensions.SequenceEqual<char>(new ReadOnlySpan<char>(comparisonBuffer, 0, buffered),
                    System.MemoryExtensions.AsSpan(captured, (int)position, buffered)))) matches = false;
            position += buffered;
            buffered = 0;
        }
    }
