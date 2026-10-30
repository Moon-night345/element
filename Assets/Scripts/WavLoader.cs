// 元素星海·裂核 — 极简 WAV 解析(PCM 16bit),供 StreamingAssets 运行时加载
using System;
using System.IO;
using UnityEngine;

namespace ElementalStarSea.Game
{
    public static class WavLoader
    {
        public static AudioClip Load(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                int pos = 12; // 跳过 RIFF 头
                ushort channels = 1, bits = 16;
                int sampleRate = 44100;
                var dataStart = -1;
                int dataLen = 0;
                while (pos + 8 <= bytes.Length)
                {
                    var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    var size = BitConverter.ToInt32(bytes, pos + 4);
                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToUInt16(bytes, pos + 10);
                        sampleRate = BitConverter.ToInt32(bytes, pos + 12);
                        bits = BitConverter.ToUInt16(bytes, pos + 22);
                    }
                    else if (id == "data")
                    {
                        dataStart = pos + 8;
                        dataLen = Math.Min(size, bytes.Length - dataStart);
                        break;
                    }
                    pos += 8 + size + (size & 1);
                }
                if (dataStart < 0 || bits != 16) return null;

                int sampleCount = dataLen / 2 / channels;
                var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path),
                    sampleCount, channels, sampleRate, false);
                var samples = new float[sampleCount * channels];
                for (int i = 0; i < samples.Length; i++)
                {
                    short v = BitConverter.ToInt16(bytes, dataStart + i * 2);
                    samples[i] = v / 32768f;
                }
                clip.SetData(samples, 0);
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogError("[WavLoader] " + path + ": " + e.Message);
                return null;
            }
        }
    }
}
