using System;
using System.IO;
using NAudio.Wave;

namespace C.A.S.S.I.E
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer _outputDevice;
        private WaveStream _waveStream;

        public void Play(string filePath)
        {
            Stop();

            _outputDevice = new WaveOutEvent();
            _waveStream = new AudioFileReader(filePath);

            _outputDevice.Init(_waveStream);
            _outputDevice.Play();
        }

        public void Play(MemoryStream audioStream)
        {
            Stop();

            audioStream.Position = 0;
            _outputDevice = new WaveOutEvent();
            _waveStream = new WaveFileReader(audioStream);

            _outputDevice.Init(_waveStream);
            _outputDevice.Play();
        }

        public void Stop()
        {
            if (_outputDevice != null)
            {
                _outputDevice.Stop();
                _outputDevice.Dispose();
                _outputDevice = null;
            }

            if (_waveStream != null)
            {
                _waveStream.Dispose();
                _waveStream = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}