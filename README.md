# C.A.S.S.I.E Sentence Builder

> **A modern, modular voice concatenation tool inspired by the C.A.S.S.I.E. system in _SCP: Secret Laboratory_.**  
> **基于《SCP: Secret Laboratory》C.A.S.S.I.E. 语音系统的现代语音拼接工具。**

---

## 免责声明
1. 您需要拥有合法的本地《SCP: Secret Laboratory》客户端并体验游戏后，方可使用此工具。
2. 您必须自行承担使用本工具所产生的一切后果。
3. 北木工作室（Northwood Studios）拥有相关游戏及音频资源的全部权利。

---

## 项目简介 / Introduction

C.A.S.S.I.E Sentence Builder 是一个基于 **C#、WinForms、NAudio** 的语音拼接器，  
可将多个单词的 `.ogg` 音频拼接成完整的语音句子，支持语速、音高、混响、延迟、提前播放等参数调节。  
适合 **SCP:SL 语音生成、C.A.S.S.I.E 模拟器** 等项目使用。

---
## 所使用的库
- NAudio
- NVorbis
---
## 功能特性 / Features

- 支持 **OGG 音频拼接**（自动匹配文件夹中的单词）
- 可调参数：
  - 播放间隔（Gap）
  - 提前播放时间（Overlap）
  - 延迟（Voice Delay）
  - 语速（Speed）
  - 音高（Pitch）
  - 混响（Reverb Tail）
- 自动保存配置（`cassie_config.json`）
- 支持“预生成”模式，可直接在内存中生成并播放
- 暗色 UI
