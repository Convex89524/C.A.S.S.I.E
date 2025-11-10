using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace C.A.S.S.I.E
{
    public static class WordListService
    {
        public static IReadOnlyList<string> LoadWords(string folderOrArchivePath)
        {
            if (string.IsNullOrWhiteSpace(folderOrArchivePath))
                throw new ArgumentException("路径不能为空。", nameof(folderOrArchivePath));

            if (InMemoryOggDataArchive.IsDataArchive(folderOrArchivePath))
            {
                return InMemoryOggDataArchive.ListWordNames(folderOrArchivePath);
            }

            if (!Directory.Exists(folderOrArchivePath))
                throw new DirectoryNotFoundException($"文件夹不存在：{folderOrArchivePath}");

            var words = Directory.GetFiles(folderOrArchivePath, "*.ogg")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return words;
        }

        public static IReadOnlyList<string> BuildSentenceFromText(
            string inputText,
            IEnumerable<string> availableWords)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<string>();

            if (availableWords == null)
                return Array.Empty<string>();

            var allSet = new HashSet<string>(availableWords, StringComparer.OrdinalIgnoreCase);

            var tokens = inputText.Split(
                new[] { ' ', ',', '.', '!', '?', '\r', '\n', ';', ':', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            var result = new List<string>();
            foreach (var raw in tokens)
            {
                var lower = raw.ToLowerInvariant();
                if (allSet.Contains(lower))
                {
                    result.Add(lower);
                }
            }

            return result;
        }
    }
}
