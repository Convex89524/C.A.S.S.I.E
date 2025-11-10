using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OggDataPacker
{
    internal static class OggDataArchive
    {
        private const string Magic = "OGGDATA1";

        public static void CreateArchive(string sourceFolder, string archivePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
                throw new ArgumentException("sourceFolder 不能为空。", nameof(sourceFolder));

            if (!Directory.Exists(sourceFolder))
                throw new DirectoryNotFoundException("找不到文件夹: " + sourceFolder);

            var oggFiles = Directory.GetFiles(sourceFolder, "*.ogg", SearchOption.AllDirectories);

            if (oggFiles.Length == 0)
                throw new InvalidOperationException("该文件夹内没有找到任何 .ogg 文件。");

            var outDir = Path.GetDirectoryName(Path.GetFullPath(archivePath));
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false))
            {
                var magicBytes = Encoding.ASCII.GetBytes(Magic);
                if (magicBytes.Length != 8)
                    throw new InvalidOperationException("Magic 长度必须为 8 字节。");
                writer.Write(magicBytes);

                writer.Write(oggFiles.Length);

                foreach (var fullPath in oggFiles)
                {
                    var relativePath = GetRelativePath(sourceFolder, fullPath);

                    byte[] data = File.ReadAllBytes(fullPath);

                    writer.Write(relativePath);

                    writer.Write((long)data.Length);
                    writer.Write(data);

                    Console.WriteLine($"打包: {relativePath} ({data.Length} bytes)");
                }
            }
        }
        
        public static void ExtractArchive(string archivePath, string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("archivePath 不能为空。", nameof(archivePath));

            if (!File.Exists(archivePath))
                throw new FileNotFoundException("找不到 .data 文件: " + archivePath);

            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new ArgumentException("outputFolder 不能为空。", nameof(outputFolder));

            Directory.CreateDirectory(outputFolder);

            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false))
            {
                var magicBytes = reader.ReadBytes(8);
                var magicString = Encoding.ASCII.GetString(magicBytes);
                if (!string.Equals(magicString, Magic, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("该文件不是有效的 OGGDATA1 资源包（魔数不匹配）。");
                }

                int fileCount = reader.ReadInt32();
                if (fileCount < 0)
                    throw new InvalidDataException("资源包文件数量非法。");

                Console.WriteLine($"文件数量: {fileCount}");

                for (int i = 0; i < fileCount; i++)
                {
                    string relativePath = reader.ReadString();
                    long length = reader.ReadInt64();

                    if (length < 0)
                        throw new InvalidDataException($"第 {i} 个文件长度非法。");

                    byte[] data = reader.ReadBytes((int)length);
                    if (data.Length != length)
                        throw new EndOfStreamException("资源包数据不完整或已损坏。");

                    string outPath = Path.Combine(outputFolder, relativePath);

                    var outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }

                    File.WriteAllBytes(outPath, data);
                    Console.WriteLine($"解包: {relativePath} -> {outPath}");
                }
            }
        }
        
        private static string GetRelativePath(string baseFolder, string fullPath)
        {
            baseFolder = Path.GetFullPath(baseFolder);
            fullPath = Path.GetFullPath(fullPath);

            if (!baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !baseFolder.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                baseFolder += Path.DirectorySeparatorChar;
            }

            var uriBase = new Uri(baseFolder, UriKind.Absolute);
            var uriFull = new Uri(fullPath, UriKind.Absolute);

            var relativeUri = uriBase.MakeRelativeUri(uriFull);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            return relativePath;
        }
    }
}
