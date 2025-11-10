using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace C.A.S.S.I.E
{
    internal static class InMemoryOggDataArchive
    {
        private const string Magic = "OGGDATA1";

        private static readonly Dictionary<string, Dictionary<string, byte[]>> Cache
            = new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);

        public static bool IsDataArchive(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                   && File.Exists(path)
                   && string.Equals(Path.GetExtension(path), ".data", StringComparison.OrdinalIgnoreCase);
        }

        public static Dictionary<string, byte[]> GetArchive(string archivePath)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("archivePath 不能为空。", nameof(archivePath));

            archivePath = Path.GetFullPath(archivePath);

            lock (Cache)
            {
                if (Cache.TryGetValue(archivePath, out var cached))
                    return cached;

                var loaded = LoadArchiveCore(archivePath);
                Cache[archivePath] = loaded;
                return loaded;
            }
        }

        private static Dictionary<string, byte[]> LoadArchiveCore(string archivePath)
        {
            if (!File.Exists(archivePath))
                throw new FileNotFoundException("找不到 .data 文件: " + archivePath);

            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false))
            {
                // Magic
                var magicBytes = reader.ReadBytes(8);
                var magicString = Encoding.ASCII.GetString(magicBytes);
                if (!string.Equals(magicString, Magic, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("该文件不是有效的 OGGDATA1 资源包（魔数不匹配）。");
                }

                int fileCount = reader.ReadInt32();
                if (fileCount < 0)
                    throw new InvalidDataException("资源包文件数量非法。");

                for (int i = 0; i < fileCount; i++)
                {
                    string relativePath = reader.ReadString();
                    long length = reader.ReadInt64();

                    if (length < 0)
                        throw new InvalidDataException($"第 {i} 个文件长度非法。");

                    byte[] data = reader.ReadBytes((int)length);
                    if (data.Length != length)
                        throw new EndOfStreamException("资源包数据不完整或已损坏。");

                    result[relativePath] = data;
                }
            }

            return result;
        }

        public static IReadOnlyList<string> ListWordNames(string archivePath)
        {
            var dict = GetArchive(archivePath);

            var words = dict.Keys
                .Where(p => p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return words;
        }

        public static bool TryOpenOgg(
            Dictionary<string, byte[]> archive,
            string fileName,
            out MemoryStream stream)
        {
            stream = null;
            if (archive == null || string.IsNullOrWhiteSpace(fileName))
                return false;

            if (archive.TryGetValue(fileName, out var data))
            {
                stream = new MemoryStream(data, writable: false);
                return true;
            }

            string normalized = fileName.Replace('\\', '/');

            foreach (var kv in archive)
            {
                string keyNorm = kv.Key.Replace('\\', '/');

                if (keyNorm.EndsWith("/" + normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(keyNorm, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    stream = new MemoryStream(kv.Value, writable: false);
                    return true;
                }
            }

            return false;
        }

        public static IReadOnlyList<string> ListBackgroundCandidates(Dictionary<string, byte[]> archive)
        {
            if (archive == null)
                return Array.Empty<string>();

            return archive.Keys
                .Where(p =>
                {
                    if (!p.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                        return false;

                    string name = Path.GetFileNameWithoutExtension(p);
                    return name.StartsWith("BG_", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }
    }
}
