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
            string folder,
            string[] words,
            string outputPath,
            float gapMs,
            float speed,
            float pitchSemitones,
            float overlapMs,
            float voiceDelayMs,
            float reverbLevel)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("words 不能为空");

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException(folder);

            WaveFormat baseFormat;
            float[] speechSamples = BuildSentenceToFloatArray(
                folder, words, gapMs, overlapMs, speed, pitchSemitones, out baseFormat);

            if (baseFormat == null)
                throw new InvalidOperationException("没有正确加载任何 OGG 文件。");

            double totalSeconds;
            float[] finalSamples = MixWithBackground(
                folder, speechSamples, baseFormat, voiceDelayMs, out totalSeconds);

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
            string folder,
            string[] words,
            Stream outputStream,
            float gapMs,
            float speed,
            float pitchSemitones,
            float overlapMs,
            float voiceDelayMs,
            float reverbLevel)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("单词列表为空。");

            if (!Directory.Exists(folder))
                throw new DirectoryNotFoundException(folder);

            WaveFormat baseFormat;
            float[] speechSamples = BuildSentenceToFloatArray(
                folder, words, gapMs, overlapMs, speed, pitchSemitones, out baseFormat);

            if (baseFormat == null)
                throw new InvalidOperationException("没有正确加载任何 OGG 文件。");

            double totalSeconds;
            float[] finalSamples = MixWithBackground(
                folder, speechSamples, baseFormat, voiceDelayMs, out totalSeconds);

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
            string folder,
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
                string path = Path.Combine(folder, fileName);

                if (!File.Exists(path))
                    throw new FileNotFoundException("找不到单词音频: " + path);

                float[] wordSamples = LoadOggToFloatArray(path, out WaveFormat format, out TimeSpan duration);

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

            string bgPath = SelectBgFile(folder, total);
            if (bgPath == null || !File.Exists(bgPath))
            {
                int delaySamplesOnly = (int)(voiceDelayMs / 1000.0 * sampleRate * channels);
                float[] result = new float[delaySamplesOnly + speechSamples.Length];
                Array.Copy(speechSamples, 0, result, delaySamplesOnly, speechSamples.Length);
                return result;
            }

            float[] bgSamples = LoadOggToFloatArray(bgPath, out WaveFormat bgFormat, out TimeSpan bgDuration);

            if (bgFormat.SampleRate != sampleRate || bgFormat.Channels != channels)
            {
                throw new InvalidOperationException(
                    $"BG 文件 {Path.GetFileName(bgPath)} 的格式与语音不一致。BG: " +
                    $"{bgFormat.SampleRate}Hz/{bgFormat.Channels}ch, " +
                    $"语音: {sampleRate}Hz/{channels}ch");
            }

            int delaySamples = (int)(voiceDelayMs / 1000.0 * sampleRate * channels);
            int totalSamples = Math.Max(bgSamples.Length, speechSamples.Length + delaySamples);

            float[] mixed = new float[totalSamples];

            // 先写入 BG
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

        private static float[] Resample(float[] input, double factor)
        {
            if (factor <= 0.0) factor = 1.0;
            int inLen = input.Length;
            if (inLen == 0) return input;

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
            int inLen = input.Length;

            float levelNorm = Math.Max(0f, Math.Min(1f, reverbLevel / 24f));

            float wet = 0.1f + 0.5f * levelNorm;
            float decayBase = 0.4f + 0.5f * levelNorm;

            float tailSeconds = 0.5f + 2.5f * levelNorm;
            int tailSamples = (int)(tailSeconds * sampleRate * channels);

            int outLen = inLen + tailSamples;
            float[] output = new float[outLen];

            Array.Copy(input, output, inLen);

            int delay1 = (int)(0.060 * sampleRate) * channels;
            int delay2 = (int)(0.095 * sampleRate) * channels;
            int delay3 = (int)(0.140 * sampleRate) * channels;

            for (int n = 0; n < inLen; n++)
            {
                float x = input[n];
                if (Math.Abs(x) < 1e-6f) continue;

                double t = (double)n / (sampleRate * channels);
                float decay = (float)Math.Pow(decayBase, t * 2.0);

                float baseAmp = wet * decay * x;

                int idx1 = n + delay1;
                int idx2 = n + delay2;
                int idx3 = n + delay3;

                if (idx1 < outLen)
                {
                    float v = output[idx1] + baseAmp * 0.8f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx1] = v;
                }

                if (idx2 < outLen)
                {
                    float v = output[idx2] + baseAmp * 0.6f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx2] = v;
                }

                if (idx3 < outLen)
                {
                    float v = output[idx3] + baseAmp * 0.5f;
                    if (v > 1f) v = 1f;
                    else if (v < -1f) v = -1f;
                    output[idx3] = v;
                }
            }

            return output;
        }
    }
}
