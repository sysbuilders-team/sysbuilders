using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Core.Discovery.Mdns
{
    public enum MdnsRecordType : ushort
    {
        A = 1,
        PTR = 12,
        TXT = 16,
        SRV = 33,
        ANY = 255
    }

    public sealed class MdnsQuestion
    {
        public string Name { get; }
        public MdnsRecordType Type { get; }
        public bool UnicastResponse { get; }

        public MdnsQuestion(string name, MdnsRecordType type, bool unicastResponse = false)
        {
            Name = name;
            Type = type;
            UnicastResponse = unicastResponse;
        }

        public override string ToString() => $"{Name} type={(ushort)Type}{(UnicastResponse ? " QU" : string.Empty)}";
    }

    /// <summary>
    /// Already decoded mDNS record. The rdata is interpreted during parse (not kept raw) because
    /// names inside the rdata may use compression and only make sense with the whole buffer.
    /// </summary>
    public sealed class MdnsRecord
    {
        public string Name { get; set; }
        public MdnsRecordType Type { get; set; }
        public bool CacheFlush { get; set; }
        public int Ttl { get; set; }

        // PTR / SRV
        public string TargetName { get; set; }

        // SRV
        public int SrvPriority { get; set; }
        public int SrvWeight { get; set; }
        public int SrvPort { get; set; }

        // TXT
        public IReadOnlyList<string> Text { get; set; }

        // A
        public IPAddress Address { get; set; }

        // Types we do not interpret (NSEC etc.)
        public byte[] RawRData { get; set; }

        public static MdnsRecord Ptr(string name, string target, TimeSpan ttl, bool cacheFlush = false)
            => new MdnsRecord { Name = name, Type = MdnsRecordType.PTR, TargetName = target, Ttl = Seconds(ttl), CacheFlush = cacheFlush };

        public static MdnsRecord Srv(string name, int port, string target, TimeSpan ttl,
            int priority = 0, int weight = 0, bool cacheFlush = false)
            => new MdnsRecord
            {
                Name = name,
                Type = MdnsRecordType.SRV,
                SrvPriority = priority,
                SrvWeight = weight,
                SrvPort = port,
                TargetName = target,
                Ttl = Seconds(ttl),
                CacheFlush = cacheFlush
            };

        public static MdnsRecord Txt(string name, IReadOnlyList<string> text, TimeSpan ttl, bool cacheFlush = false)
            => new MdnsRecord { Name = name, Type = MdnsRecordType.TXT, Text = text, Ttl = Seconds(ttl), CacheFlush = cacheFlush };

        public static MdnsRecord AddressA(string name, IPAddress address, TimeSpan ttl, bool cacheFlush = false)
            => new MdnsRecord { Name = name, Type = MdnsRecordType.A, Address = address, Ttl = Seconds(ttl), CacheFlush = cacheFlush };

        private static int Seconds(TimeSpan ttl) => ttl <= TimeSpan.Zero ? 0 : (int)Math.Min(int.MaxValue, ttl.TotalSeconds);

        public override string ToString() => $"{Name} {(ushort)Type} ttl={Ttl}{(CacheFlush ? " flush" : string.Empty)}";
    }

    public sealed class MdnsMessage
    {
        public ushort Id { get; set; }
        public bool IsResponse { get; set; }
        public bool IsAuthoritative { get; set; }
        public bool IsTruncated { get; set; }

        public List<MdnsQuestion> Questions { get; } = new List<MdnsQuestion>();
        public List<MdnsRecord> Answers { get; } = new List<MdnsRecord>();
        public List<MdnsRecord> Authorities { get; } = new List<MdnsRecord>();
        public List<MdnsRecord> Additionals { get; } = new List<MdnsRecord>();

        public bool HasQuestion(string name, MdnsRecordType type)
        {
            foreach (var question in Questions)
            {
                if (question.Type != type && question.Type != MdnsRecordType.ANY) continue;
                if (MdnsCodec.NamesEqual(question.Name, name)) return true;
            }
            return false;
        }

        public bool AnyUnicastResponseRequested()
        {
            foreach (var question in Questions)
            {
                if (question.UnicastResponse) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Minimal DNS codec for mDNS (RFC 6762/6763) in the part the game uses: PTR, SRV, TXT and A.
    /// Encode without compression (allowed: compression is optional for senders; the decode accepts
    /// pointers, which every responder out there uses) and defensive decode: a malformed packet
    /// returns false and never throws.
    /// </summary>
    public static class MdnsCodec
    {
        public const int MaxMessageBytes = 9000;
        private const int HeaderBytes = 12;
        private const int MaxRecords = 256;
        private const int MaxPointerJumps = 32;

        private const ushort FlagResponse = 0x8000;
        private const ushort FlagAuthoritative = 0x0400;
        private const ushort FlagTruncated = 0x0200;
        private const ushort ClassIn = 0x0001;
        private const ushort ClassUnicastResponse = 0x8000;
        private const ushort ClassCacheFlush = 0x8000;

        // encode

        public static byte[] EncodeQuery(ushort id, string name, MdnsRecordType type, bool unicastResponse)
        {
            using var stream = new MemoryStream(64);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            WriteHeader(writer, id, flags: 0, questions: 1, answers: 0, authorities: 0, additionals: 0);
            WriteName(writer, name);
            WriteUShort(writer, (ushort)type);
            WriteUShort(writer, (ushort)(ClassIn | (unicastResponse ? ClassUnicastResponse : 0)));

            return stream.ToArray();
        }

        public static byte[] EncodeResponse(ushort id, bool authoritative, IReadOnlyList<MdnsRecord> answers,
            IReadOnlyList<MdnsRecord> additionals = null)
        {
            answers ??= Array.Empty<MdnsRecord>();
            additionals ??= Array.Empty<MdnsRecord>();

            using var stream = new MemoryStream(256);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            ushort flags = FlagResponse;
            if (authoritative) flags |= FlagAuthoritative;

            WriteHeader(writer, id, flags, questions: 0, answers: answers.Count, authorities: 0, additionals: additionals.Count);

            foreach (var record in answers) WriteRecord(writer, record);
            foreach (var record in additionals) WriteRecord(writer, record);

            return stream.ToArray();
        }

        public static byte[] EncodeName(string name)
        {
            using var stream = new MemoryStream(64);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);
            WriteName(writer, name);
            return stream.ToArray();
        }

        public static void WriteName(BinaryWriter writer, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                writer.Write((byte)0);
                return;
            }

            foreach (string label in name.Split('.'))
            {
                if (label.Length == 0) continue;

                byte[] bytes = Encoding.UTF8.GetBytes(label);
                if (bytes.Length > 63) bytes = new byte[63];
                if (bytes.Length == 0) continue;

                writer.Write((byte)bytes.Length);
                writer.Write(bytes);
            }

            writer.Write((byte)0);
        }

        private static void WriteRecord(BinaryWriter writer, MdnsRecord record)
        {
            WriteName(writer, record.Name);
            WriteUShort(writer, (ushort)record.Type);
            WriteUShort(writer, (ushort)(ClassIn | (record.CacheFlush ? ClassCacheFlush : 0)));
            WriteUInt(writer, (uint)Math.Max(0, record.Ttl));

            byte[] rdata = BuildRData(record);
            WriteUShort(writer, (ushort)rdata.Length);
            writer.Write(rdata);
        }

        private static byte[] BuildRData(MdnsRecord record)
        {
            switch (record.Type)
            {
                case MdnsRecordType.PTR:
                    return EncodeName(record.TargetName);

                case MdnsRecordType.SRV:
                {
                    using var stream = new MemoryStream(64);
                    using var writer = new BinaryWriter(stream, Encoding.UTF8);
                    WriteUShort(writer, (ushort)record.SrvPriority);
                    WriteUShort(writer, (ushort)record.SrvWeight);
                    WriteUShort(writer, (ushort)record.SrvPort);
                    WriteName(writer, record.TargetName);
                    return stream.ToArray();
                }

                case MdnsRecordType.TXT:
                {
                    using var stream = new MemoryStream(128);
                    using var writer = new BinaryWriter(stream, Encoding.UTF8);
                    if (record.Text != null)
                    {
                        foreach (string entry in record.Text)
                        {
                            if (entry == null) continue;

                            byte[] bytes = Encoding.UTF8.GetBytes(entry);
                            if (bytes.Length > 255) bytes = new byte[255];
                            writer.Write((byte)bytes.Length);
                            if (bytes.Length > 0) writer.Write(bytes);
                        }
                    }

                    if (stream.Length == 0) writer.Write((byte)0);
                    return stream.ToArray();
                }

                case MdnsRecordType.A:
                {
                    byte[] bytes = record.Address?.GetAddressBytes() ?? new byte[4];
                    if (bytes.Length != 4) return new byte[4];
                    return bytes;
                }

                default:
                    return record.RawRData ?? Array.Empty<byte>();
            }
        }

        // decode

        /// <summary>Never throws: any inconsistency returns false.</summary>
        public static bool TryDecode(byte[] buffer, out MdnsMessage message) => TryDecode(buffer, buffer?.Length ?? 0, out message);

        public static bool TryDecode(byte[] buffer, int length, out MdnsMessage message)
        {
            message = null;

            if (buffer == null || length < HeaderBytes || length > buffer.Length || length > MaxMessageBytes) return false;

            try
            {
                var result = new MdnsMessage
                {
                    Id = ReadUShort(buffer, 0),
                    IsResponse = (ReadUShort(buffer, 2) & FlagResponse) != 0,
                    IsAuthoritative = (ReadUShort(buffer, 2) & FlagAuthoritative) != 0,
                    IsTruncated = (ReadUShort(buffer, 2) & FlagTruncated) != 0
                };

                int questionCount = ReadUShort(buffer, 4);
                int answerCount = ReadUShort(buffer, 6);
                int authorityCount = ReadUShort(buffer, 8);
                int additionalCount = ReadUShort(buffer, 10);

                if (questionCount > MaxRecords || answerCount > MaxRecords ||
                    authorityCount > MaxRecords || additionalCount > MaxRecords) return false;

                int offset = HeaderBytes;

                for (int i = 0; i < questionCount; i++)
                {
                    if (!TryReadName(buffer, ref offset, out string name)) return false;
                    if (offset + 4 > length) return false;

                    var type = (MdnsRecordType)ReadUShort(buffer, offset);
                    ushort cls = ReadUShort(buffer, offset + 2);
                    offset += 4;

                    result.Questions.Add(new MdnsQuestion(name, type, (cls & ClassUnicastResponse) != 0));
                }

                if (!TryReadRecords(buffer, length, ref offset, answerCount, result.Answers)) return false;
                if (!TryReadRecords(buffer, length, ref offset, authorityCount, result.Authorities)) return false;
                if (!TryReadRecords(buffer, length, ref offset, additionalCount, result.Additionals)) return false;

                message = result;
                return true;
            }
            catch (Exception)
            {
                message = null;
                return false;
            }
        }

        private static bool TryReadRecords(byte[] buffer, int length, ref int offset, int count, List<MdnsRecord> target)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryReadRecord(buffer, length, ref offset, out MdnsRecord record)) return false;
                target.Add(record);
            }
            return true;
        }

        private static bool TryReadRecord(byte[] buffer, int length, ref int offset, out MdnsRecord record)
        {
            record = null;

            if (!TryReadName(buffer, ref offset, out string name)) return false;
            if (offset + 10 > length) return false;

            var type = (MdnsRecordType)ReadUShort(buffer, offset);
            ushort cls = ReadUShort(buffer, offset + 2);
            long ttl = ReadUInt(buffer, offset + 4);
            int rdLength = ReadUShort(buffer, offset + 8);
            offset += 10;

            if (offset + rdLength > length) return false;

            int rdataStart = offset;
            int rdataEnd = offset + rdLength;
            var parsed = new MdnsRecord
            {
                Name = name,
                Type = type,
                CacheFlush = (cls & ClassCacheFlush) != 0,
                Ttl = ttl > int.MaxValue ? int.MaxValue : (int)ttl
            };

            int cursor = rdataStart;

            switch (type)
            {
                case MdnsRecordType.PTR:
                    if (!TryReadName(buffer, ref cursor, out string target)) return false;
                    parsed.TargetName = target;
                    break;

                case MdnsRecordType.SRV:
                    if (rdLength < 6) return false;
                    parsed.SrvPriority = ReadUShort(buffer, rdataStart);
                    parsed.SrvWeight = ReadUShort(buffer, rdataStart + 2);
                    parsed.SrvPort = ReadUShort(buffer, rdataStart + 4);
                    cursor = rdataStart + 6;
                    if (!TryReadName(buffer, ref cursor, out string srvTarget)) return false;
                    parsed.TargetName = srvTarget;
                    break;

                case MdnsRecordType.TXT:
                {
                    var entries = new List<string>();
                    while (cursor < rdataEnd)
                    {
                        int textLength = buffer[cursor++];
                        if (cursor + textLength > rdataEnd) return false;

                        entries.Add(Encoding.UTF8.GetString(buffer, cursor, textLength));
                        cursor += textLength;
                    }
                    parsed.Text = entries;
                    break;
                }

                case MdnsRecordType.A:
                    if (rdLength != 4) return false;
                    parsed.Address = new IPAddress(new[]
                    {
                        buffer[rdataStart], buffer[rdataStart + 1], buffer[rdataStart + 2], buffer[rdataStart + 3]
                    });
                    break;

                default:
                    parsed.RawRData = new byte[rdLength];
                    Buffer.BlockCopy(buffer, rdataStart, parsed.RawRData, 0, rdLength);
                    break;
            }

            // The cursor may have followed pointers: the next record always starts after the rdata.
            offset = rdataEnd;
            record = parsed;
            return true;
        }

        /// <summary>Reads a DNS name, following compression pointers (with loop protection).</summary>
        public static bool TryReadName(byte[] buffer, ref int offset, out string name)
        {
            name = null;
            if (buffer == null || offset < 0 || offset >= buffer.Length) return false;

            var labels = new List<string>();
            int position = offset;
            int resumeAt = offset;
            int jumps = 0;

            while (true)
            {
                if (position >= buffer.Length) return false;

                byte length = buffer[position];

                if (length == 0)
                {
                    position++;
                    if (jumps == 0) resumeAt = position;
                    break;
                }

                if ((length & 0xC0) == 0xC0)
                {
                    if (position + 1 >= buffer.Length) return false;

                    int pointer = ((length & 0x3F) << 8) | buffer[position + 1];
                    if (jumps == 0) resumeAt = position + 2;
                    if (++jumps > MaxPointerJumps) return false;
                    if (pointer >= position || pointer >= buffer.Length) return false;

                    position = pointer;
                    continue;
                }

                if ((length & 0xC0) != 0) return false;

                position++;
                if (position + length > buffer.Length) return false;

                labels.Add(Encoding.UTF8.GetString(buffer, position, length));
                position += length;

                if (labels.Count > 64) return false;
            }

            offset = resumeAt;
            name = string.Join(".", labels);
            return true;
        }

        public static bool NamesEqual(string a, string b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;

            return string.Equals(TrimDot(a), TrimDot(b), StringComparison.OrdinalIgnoreCase);
        }

        public static string TrimDot(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.EndsWith(".", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
        }

        private static void WriteHeader(BinaryWriter writer, ushort id, ushort flags, int questions, int answers,
            int authorities, int additionals)
        {
            // DNS is network order (big-endian). BinaryWriter writes little-endian: that is why the
            // multi-byte fields go through the helpers instead of writer.Write((ushort)x).
            WriteUShort(writer, id);
            WriteUShort(writer, flags);
            WriteUShort(writer, (ushort)questions);
            WriteUShort(writer, (ushort)answers);
            WriteUShort(writer, (ushort)authorities);
            WriteUShort(writer, (ushort)additionals);
        }

        private static void WriteUShort(BinaryWriter writer, ushort value)
        {
            writer.Write((byte)(value >> 8));
            writer.Write((byte)(value & 0xFF));
        }

        private static void WriteUInt(BinaryWriter writer, uint value)
        {
            writer.Write((byte)(value >> 24));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static ushort ReadUShort(byte[] buffer, int offset) => (ushort)((buffer[offset] << 8) | buffer[offset + 1]);

        private static long ReadUInt(byte[] buffer, int offset) =>
            ((long)buffer[offset] << 24) | ((long)buffer[offset + 1] << 16) | ((long)buffer[offset + 2] << 8) | buffer[offset + 3];
    }
}
