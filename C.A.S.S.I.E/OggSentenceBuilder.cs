using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Vorbis;
using NAudio.Wave;

namespace C.A.S.S.I.E
{
    public static class OggSentenceBuilder
    {
        public static void BuildSentence(
            string folderOrArchive,
            string[] words,
            string outputPath,
            float gapMs,
            float speed,
            float pitchSemitones,
            float overlapMs,
            float voiceDelayMs,
            float reverbLevel,
            bool enableBackground)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("words 不能为空");

            if (string.IsNullOrWhiteSpace(folderOrArchive))
                throw new ArgumentException("路径不能为空。", nameof(folderOrArchive));

            bool isArchive = InMemoryOggDataArchive.IsDataArchive(folderOrArchive);
            if (!isArchive && !Directory.Exists(folderOrArchive))
                throw new DirectoryNotFoundException(folderOrArchive);

            Dictionary<string, byte[]> archive = isArchive
                ? InMemoryOggDataArchive.GetArchive(folderOrArchive)
                : null;

            WaveFormat baseFormat;
            float[] speechSamples = BuildSentenceToFloatArray(
                folderOrArchive, archive, words, gapMs, overlapMs, speed, pitchSemitones, out baseFormat);

            if (baseFormat == null)
                throw new InvalidOperationException("没有正确加载任何 OGG 文件。");

            double totalSeconds;
            float[] finalSamples = archive != null
                ? MixWithBackgroundFromArchive(archive, speechSamples, baseFormat, voiceDelayMs, enableBackground, out totalSeconds)
                : MixWithBackground(folderOrArchive, speechSamples, baseFormat, voiceDelayMs, enableBackground, out totalSeconds);

            float[] withReverb = ApplyReverbTail(finalSamples, baseFormat, reverbLevel);

            WaveFormat outFormat = new WaveFormat(baseFormat.SampleRate, 16, baseFormat.Channels);

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            using (var writer = new WaveFileWriter(outputPath, outFormat))
            {
                writer.WriteSamples(withReverb, 0, withReverb.Length);
            }
        }

        public static void BuildSentence(
            string folderOrArchive,
            string[] words,
            Stream outputStream,
            float gapMs,
            float speed,
            float pitchSemitones,
            float overlapMs,
            float voiceDelayMs,
            float reverbLevel,
            bool enableBackground)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("单词列表为空。");

            if (string.IsNullOrWhiteSpace(folderOrArchive))
                throw new ArgumentException("路径不能为空。", nameof(folderOrArchive));

            bool isArchive = InMemoryOggDataArchive.IsDataArchive(folderOrArchive);
            if (!isArchive && !Directory.Exists(folderOrArchive))
                throw new DirectoryNotFoundException(folderOrArchive);

            Dictionary<string, byte[]> archive = isArchive
                ? InMemoryOggDataArchive.GetArchive(folderOrArchive)
                : null;

            WaveFormat baseFormat;
            float[] speechSamples = BuildSentenceToFloatArray(
                folderOrArchive, archive, words, gapMs, overlapMs, speed, pitchSemitones, out baseFormat);

            if (baseFormat == null)
                throw new InvalidOperationException("没有正确加载任何 OGG 文件。");

            double totalSeconds;
            float[] finalSamples = archive != null
                ? MixWithBackgroundFromArchive(archive, speechSamples, baseFormat, voiceDelayMs, enableBackground, out totalSeconds)
                : MixWithBackground(folderOrArchive, speechSamples, baseFormat, voiceDelayMs, enableBackground, out totalSeconds);

            float[] withReverb = ApplyReverbTail(finalSamples, baseFormat, reverbLevel);

            WaveFormat outFormat = new WaveFormat(baseFormat.SampleRate, 16, baseFormat.Channels);

            using (var writer = new WaveFileWriter(outputStream, outFormat))
            {
                writer.WriteSamples(withReverb, 0, withReverb.Length);
                writer.Flush();
            }

            if (outputStream.CanSeek)
                outputStream.Position = 0;
        }

        private static float[] BuildSentenceToFloatArray(
            string folderOrArchive,
            Dictionary<string, byte[]> archive,
            string[] words,
            float gapMs,
            float overlapMs,
            float speed,
            float pitchSemitones,
            out WaveFormat baseFormat)
        {
            List<float> allSamples = new List<float>();

            baseFormat = null;
            int sampleRate = 0;
            int channels = 0;

            double pitchFactor = Math.Pow(2.0, pitchSemitones / 12.0);

            double speedFactor = Math.Max(0.1, speed);

            double resampleFactor = speedFactor * pitchFactor;
            if (resampleFactor <= 0.0) resampleFactor = 1.0;

            if (overlapMs < 0) overlapMs = 0;

            for (int w = 0; w < words.Length; w++)
            {
                string fileName = words[w] + ".ogg";

                float[] wordSamples;
                WaveFormat format;
                TimeSpan duration;

                if (archive != null)
                {
                    if (!InMemoryOggDataArchive.TryOpenOgg(archive, fileName, out var ms))
                        throw new FileNotFoundException("在 .data 资源包中找不到单词音频: " + fileName);

                    using (ms)
                    {
                        wordSamples = LoadOggToFloatArray(ms, out format, out duration);
                    }
                }
                else
                {
                    string path = Path.Combine(folderOrArchive, fileName);

                    if (!File.Exists(path))
                        throw new FileNotFoundException("找不到单词音频: " + path);

                    wordSamples = LoadOggToFloatArray(path, out format, out duration);
                }

                if (baseFormat == null)
                {
                    baseFormat = format;
                    sampleRate = format.SampleRate;
                    channels = format.Channels;
                }
                else
                {
                    if (format.SampleRate != sampleRate || format.Channels != channels)
                    {
                        throw new InvalidOperationException(
                            $"文件 {fileName} 的格式与第一份音频不一致，" +
                            $"当前: {format.SampleRate}Hz/{format.Channels}ch, " +
                            $"期望: {sampleRate}Hz/{channels}ch");
                    }
                }

                float[] processedSamples = Resample(wordSamples, resampleFactor);

                allSamples.AddRange(processedSamples);

                if (w != words.Length - 1)
                {
                    double effectiveGapMs = gapMs - overlapMs;
                    if (effectiveGapMs < 0) effectiveGapMs = 0;

                    if (effectiveGapMs > 0)
                    {
                        int silenceSamples = (int)(sampleRate * channels * (effectiveGapMs / 1000.0));
                        for (int i = 0; i < silenceSamples; i++)
                            allSamples.Add(0f);
                    }
                }
            }

            return allSamples.ToArray();
        }

        private static float[] MixWithBackground(
            string folder,
            float[] speechSamples,
            WaveFormat baseFormat,
            float voiceDelayMs,
            bool enableBackground,
            out double totalSeconds)
        {
            int sampleRate = baseFormat.SampleRate;
            int channels = baseFormat.Channels;

            if (sampleRate <= 0 || channels <= 0)
                throw new InvalidOperationException("基础格式不正确。");

            double speechSeconds = (double)speechSamples.Length / (sampleRate * channels);
            if (speechSeconds < 0) speechSeconds = 0;

            if (voiceDelayMs < 0) voiceDelayMs = 0;
            double total = speechSeconds + voiceDelayMs / 1000.0;
            if (total <= 0) total = speechSeconds;
            if (total <= 0) total = 4.0;

            totalSeconds = total;

            int delaySamplesOnly = (int)(voiceDelayMs / 1000.0 * sampleRate * channels);

            if (!enableBackground)
            {
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            string bgPath = SelectBgFile(folder, total);
            if (bgPath == null || !File.Exists(bgPath))
            {
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            float[] bgSamples = LoadOggToFloatArray(bgPath, out WaveFormat bgFormat, out TimeSpan bgDuration);

            if (bgFormat.SampleRate != sampleRate || bgFormat.Channels != channels)
            {
                throw new InvalidOperationException(
                    $"背景音频格式与语音不一致，" +
                    $"背景: {bgFormat.SampleRate}Hz/{bgFormat.Channels}ch, " +
                    $"语音: {sampleRate}Hz/{channels}ch");
            }

            int delaySamples = delaySamplesOnly;
            int totalSamples = Math.Max(bgSamples.Length, speechSamples.Length + delaySamples);

            float[] mixed = new float[totalSamples];

            int bgLen = Math.Min(bgSamples.Length, totalSamples);
            for (int i = 0; i < bgLen; i++)
            {
                mixed[i] = bgSamples[i];
            }

            for (int i = 0; i < speechSamples.Length; i++)
            {
                int idx = i + delaySamples;
                if (idx >= totalSamples) break;

                float v = mixed[idx] + speechSamples[i];
                if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                mixed[idx] = v;
            }

            return mixed;
        }

        private static float[] MixWithBackgroundFromArchive(
            Dictionary<string, byte[]> archive,
            float[] speechSamples,
            WaveFormat baseFormat,
            float voiceDelayMs,
            bool enableBackground,
            out double totalSeconds)
        {
            int sampleRate = baseFormat.SampleRate;
            int channels = baseFormat.Channels;

            if (sampleRate <= 0 || channels <= 0)
                throw new InvalidOperationException("基础格式不正确。");

            double speechSeconds = (double)speechSamples.Length / (sampleRate * channels);
            if (speechSeconds < 0) speechSeconds = 0;

            if (voiceDelayMs < 0) voiceDelayMs = 0;
            double total = speechSeconds + voiceDelayMs / 1000.0;
            if (total <= 0) total = speechSeconds;
            if (total <= 0) total = 4.0;

            totalSeconds = total;

            int delaySamplesOnly = (int)(voiceDelayMs / 1000.0 * sampleRate * channels);

            if (!enableBackground)
            {
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            string bgKey = SelectBgFileFromArchive(archive, total);
            if (bgKey == null)
            {
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            float[] bgSamples;
            WaveFormat bgFormat;
            TimeSpan bgDuration;

            if (!InMemoryOggDataArchive.TryOpenOgg(archive, bgKey, out var bgStream))
            {
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            using (bgStream)
            {
                bgSamples = LoadOggToFloatArray(bgStream, out bgFormat, out bgDuration);
            }

            if (bgFormat.SampleRate != sampleRate || bgFormat.Channels != channels)
            {
                throw new InvalidOperationException(
                    $"背景音频格式与语音不一致，" +
                    $"背景: {bgFormat.SampleRate}Hz/{bgFormat.Channels}ch, " +
                    $"语音: {sampleRate}Hz/{channels}ch");
            }

            int delaySamples = delaySamplesOnly;
            int totalSamples = Math.Max(bgSamples.Length, speechSamples.Length + delaySamples);

            float[] mixed = new float[totalSamples];

            int bgLen = Math.Min(bgSamples.Length, totalSamples);
            for (int i = 0; i < bgLen; i++)
            {
                mixed[i] = bgSamples[i];
            }

            for (int i = 0; i < speechSamples.Length; i++)
            {
                int idx = i + delaySamples;
                if (idx >= totalSamples) break;

                float v = mixed[idx] + speechSamples[i];
                if (v > 1f) v = 1f;
                else if (v < -1f) v = -1f;
                mixed[idx] = v;
            }

            return mixed;
        }

        private static string SelectBgFile(string folder, double totalSeconds)
        {
            int target = (int)Math.Ceiling(totalSeconds);
            if (target < 4) target = 4;
            if (target > 40) target = 40;

            string directPath = Path.Combine(folder, $"BG_{target}.ogg");
            if (File.Exists(directPath))
                return directPath;

            var candidates = Directory.GetFiles(folder, "BG_*.ogg");
            if (candidates.Length == 0)
                return null;

            string bestPath = null;
            double bestDiff = double.MaxValue;

            foreach (var path in candidates)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                var parts = name.Split('_');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[1], out int sec)) continue;

                double diff = Math.Abs(sec - totalSeconds);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestPath = path;
                }
            }

            return bestPath;
        }

        private static string SelectBgFileFromArchive(Dictionary<string, byte[]> archive, double totalSeconds)
        {
            var candidates = InMemoryOggDataArchive.ListBackgroundCandidates(archive);
            if (candidates.Count == 0)
                return null;

            int target = (int)Math.Ceiling(totalSeconds);
            if (target < 4) target = 4;
            if (target > 40) target = 40;

            string bestKey = null;
            double bestDiff = double.MaxValue;

            foreach (var key in candidates)
            {
                string name = Path.GetFileNameWithoutExtension(key);
                var parts = name.Split('_');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[1], out int sec)) continue;

                double diff = Math.Abs(sec - totalSeconds);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestKey = key;
                }
            }

            return bestKey;
        }


        private static float[] LoadOggToFloatArray(
            string path,
            out WaveFormat format,
            out TimeSpan duration)
        {
            using (var vorbisReader = new VorbisWaveReader(path))
            {
                format = vorbisReader.WaveFormat;
                duration = vorbisReader.TotalTime;

                ISampleProvider sampleProvider = vorbisReader.ToSampleProvider();

                List<float> samples = new List<float>();
                float[] buffer = new float[format.SampleRate * format.Channels];

                while (true)
                {
                    int read = sampleProvider.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    for (int i = 0; i < read; i++)
                        samples.Add(buffer[i]);
                }

                return samples.ToArray();
            }
        }

        private static float[] LoadOggToFloatArray(
            Stream source,
            out WaveFormat format,
            out TimeSpan duration)
        {
            using (var vorbisReader = new VorbisWaveReader(source))
            {
                format = vorbisReader.WaveFormat;
                duration = vorbisReader.TotalTime;

                ISampleProvider sampleProvider = vorbisReader.ToSampleProvider();

                List<float> samples = new List<float>();
                float[] buffer = new float[format.SampleRate * format.Channels];

                while (true)
                {
                    int read = sampleProvider.Read(buffer, 0, buffer.Length);
                    if (read <= 0) break;

                    for (int i = 0; i < read; i++)
                        samples.Add(buffer[i]);
                }

                return samples.ToArray();
            }
        }

        private static float[] Resample(float[] input, double factor)
        {
            if (factor <= 0.0001)
                factor = 1.0;

            if (Math.Abs(factor - 1.0) < 0.0001)
                return (float[])input.Clone();

            int inLen = input.Length;
            if (inLen <= 1)
                return (float[])input.Clone();

            int outLen = (int)(inLen / factor);
            if (outLen <= 1) outLen = 1;

            float[] output = new float[outLen];

            for (int i = 0; i < outLen; i++)
            {
                double srcPos = i * factor;
                int i0 = (int)srcPos;
                int i1 = Math.Min(i0 + 1, inLen - 1);
                float frac = (float)(srcPos - i0);

                float s0 = input[i0];
                float s1 = input[i1];

                output[i] = s0 + (s1 - s0) * frac;
            }

            return output;
        }

        private static float[] ApplyReverbTail(float[] input, WaveFormat format, float reverbLevel)
        {
            if (input == null || input.Length == 0)
                return input;

            if (reverbLevel <= 0.01f)
                return input;

            int sampleRate = format.SampleRate;
            int channels = format.Channels;

            int reverbSamples = (int)(sampleRate * channels);
            float[] output = new float[input.Length + reverbSamples];

            Array.Copy(input, output, input.Length);

            float baseAmp = Math.Min(1f, reverbLevel / 100f);

            for (int i = 0; i < input.Length; i++)
            {
                float sample = input[i] * baseAmp;
                int idx1 = i + (int)(0.08 * sampleRate * channels);
                int idx2 = i + (int)(0.16 * sampleRate * channels);
                int idx3 = i + (int)(0.24 * sampleRate * channels);

                if (idx1 < output.Length)
                {
                    float v = output[idx1] + sample * 0.5f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx1] = v;
                }

                if (idx2 < output.Length)
                {
                    float v = output[idx2] + sample * 0.3f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx2] = v;
                }

                if (idx3 < output.Length)
                {
                    float v = output[idx3] + sample * 0.15f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx3] = v;
                }
            }

            return output;
        }
    }
}
