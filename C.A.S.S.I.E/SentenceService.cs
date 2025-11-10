using System;
using System.IO;
using System.Threading.Tasks;

namespace C.A.S.S.I.E
{
    public class SentenceOptions
    {
        public float GapMs { get; set; }
        public float OverlapMs { get; set; }
        public float SpeedPercent { get; set; }
        public float PitchSemitones { get; set; }
        public float VoiceDelayMs { get; set; }
        public float ReverbLevel { get; set; }
        public bool EnableBackground { get; set; }

        public float GetSpeedFactor()
        {
            return Math.Max(0.1f, SpeedPercent / 100f);
        }
    }

    public static class SentenceService
    {
        /// <summary>
        /// 生成到文件（后台线程）
        /// </summary>
        public static Task GenerateToFileAsync(
            string folder,
            string[] words,
            string outputPath,
            SentenceOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            return Task.Run(() =>
            {
                OggSentenceBuilder.BuildSentence(
                    folder,
                    words,
                    outputPath,
                    options.GapMs,
                    options.GetSpeedFactor(),
                    options.PitchSemitones,
                    options.OverlapMs,
                    options.VoiceDelayMs,
                    options.ReverbLevel,
                    options.EnableBackground);
            });
        }

        /// <summary>
        /// 生成到内存（预生成）
        /// </summary>
        public static Task<MemoryStream> GenerateToMemoryAsync(
            string folder,
            string[] words,
            SentenceOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            return Task.Run(() =>
            {
                using (var tmp = new MemoryStream())
                {
                    OggSentenceBuilder.BuildSentence(
                        folder,
                        words,
                        tmp,
                        options.GapMs,
                        options.GetSpeedFactor(),
                        options.PitchSemitones,
                        options.OverlapMs,
                        options.VoiceDelayMs,
                        options.ReverbLevel,
                        options.EnableBackground);

                    return new MemoryStream(tmp.ToArray());
                }
            });
        }
    }
}
