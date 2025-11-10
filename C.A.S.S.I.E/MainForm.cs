using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows.Forms;

namespace C.A.S.S.I.E
{
    [DataContract]
    public class AppConfig
    {
        [DataMember] public string FolderPath { get; set; }
        [DataMember] public string OutputPath { get; set; }

        [DataMember] public decimal GapMs { get; set; }
        [DataMember] public decimal OverlapMs { get; set; }
        [DataMember] public decimal VoiceDelayMs { get; set; }
        [DataMember] public decimal SpeedPercent { get; set; }
        [DataMember] public decimal PitchSemitones { get; set; }

        [DataMember] public string SentenceInput { get; set; }

        [DataMember] public decimal ReverbLevel { get; set; }
    }

    public class MainForm : Form
    {
        private TextBox txtFolder;
        private Button btnBrowseFolder;
        private ListBox lstAvailableWords;
        private ListBox lstSentenceWords;
        private Button btnAddWord;
        private Button btnRemoveWord;
        private Button btnMoveUp;
        private Button btnMoveDown;
        private Button btnClearSentence;

        private NumericUpDown numGapMs;
        private NumericUpDown numOverlapMs;
        private NumericUpDown numSpeed;
        private NumericUpDown numPitch;
        private NumericUpDown numVoiceDelayMs;

        private NumericUpDown numReverb;

        private TextBox txtOutput;
        private Button btnBrowseOutput;

        private Button btnGenerate;
        private Button btnPreGenerate;
        private Button btnPlay;
        private Button btnStop;
        private Label lblStatus;

        private TextBox txtSentenceInput;
        private Button btnApplySentence;

        private AudioPlayer _player = new AudioPlayer();
        private System.IO.MemoryStream _previewAudio;

        private readonly string _configPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cassie_config.json");

        public MainForm()
        {
            this.Text = "C.A.S.S.I.E Sentence Builder";
            this.Width = 1000;
            this.Height = 640;
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.FromArgb(220, 220, 220);
            this.Font = new Font("Consolas", 10f, FontStyle.Regular);

            var panelLeft = new Panel
            {
                Left = 10,
                Top = 10,
                Width = 420,
                Height = 550,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            var panelRight = new Panel
            {
                Left = 440,
                Top = 10,
                Width = 530,
                Height = 550,
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // 文件夹选择
            var lblFolder = new Label { Text = "OGG 文件夹", Left = 10, Top = 15, AutoSize = true };
            txtFolder = new TextBox
            {
                Left = 10,
                Top = 40,
                Width = 300,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White
            };
            btnBrowseFolder = CreateButton("浏览", 320, 38, BtnBrowseFolder_Click);
            var btnRefresh = CreateButton("刷新", 320, 70, BtnRefresh_Click);

            // 可用单词列表
            var lblAvailable = new Label { Text = "可用单词", Left = 10, Top = 100, AutoSize = true };
            lstAvailableWords = CreateListBox(10, 125, 160, 400);
            lstAvailableWords.DoubleClick += (s, e) => AddSelectedWord();

            // 按钮竖排区（中间）
            int btnX = 180;
            btnAddWord = CreateButton("→", btnX, 180, (s, e) => AddSelectedWord());
            btnRemoveWord = CreateButton("←", btnX, 220, (s, e) => RemoveSelectedWord());
            btnMoveUp = CreateButton("↑", btnX, 260, (s, e) => MoveSelectedSentenceWord(-1));
            btnMoveDown = CreateButton("↓", btnX, 300, (s, e) => MoveSelectedSentenceWord(1));
            btnClearSentence = CreateButton("清空", btnX, 340, (s, e) => lstSentenceWords.Items.Clear());

            // 句子顺序列表
            var lblSentence = new Label { Text = "句子顺序", Left = 270, Top = 100, AutoSize = true };
            lstSentenceWords = CreateListBox(270, 125, 160, 400);

            panelLeft.Controls.AddRange(new Control[]
            {
                lblFolder, txtFolder, btnBrowseFolder, btnRefresh,
                lblAvailable, lstAvailableWords,
                lblSentence, lstSentenceWords,
                btnAddWord, btnRemoveWord, btnMoveUp, btnMoveDown, btnClearSentence
            });

            // ====== 参数区（右侧）======

            // 间隔
            var lblGap = new Label { Text = "间隔(ms)", Left = 10, Top = 20, AutoSize = true };
            numGapMs = CreateNumericUpDown(120, 15, 0, 5000, 80);

            var lblOverlap = new Label { Text = "提前播放(ms)", Left = 10, Top = 60, AutoSize = true };
            numOverlapMs = CreateNumericUpDown(120, 55, 0, 5000, 0);

            var lblVoiceDelay = new Label { Text = "语音延迟(ms)", Left = 10, Top = 100, AutoSize = true };
            numVoiceDelayMs = CreateNumericUpDown(120, 95, 0, 20000, 0);

            var lblSpeed = new Label { Text = "语速(%)", Left = 10, Top = 140, AutoSize = true };
            numSpeed = CreateNumericUpDown(120, 135, 10, 400, 100);

            var lblPitch = new Label { Text = "音高(半音)", Left = 10, Top = 180, AutoSize = true };
            numPitch = CreateNumericUpDown(120, 175, -24, 24, 0);

            var lblReverb = new Label { Text = "尾音混响", Left = 10, Top = 220, AutoSize = true };
            numReverb = CreateNumericUpDown(120, 215, 0, 120, 0);

            var lblOutput = new Label { Text = "输出文件", Left = 10, Top = 260, AutoSize = true };
            txtOutput = new TextBox
            {
                Left = 10,
                Top = 285,
                Width = 400,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White
            };
            btnBrowseOutput = CreateButton("选择", 420, 283, BtnBrowseOutput_Click);

            var lblSentenceInput = new Label { Text = "文本句子", Left = 10, Top = 320, AutoSize = true };
            txtSentenceInput = new TextBox
            {
                Left = 10,
                Top = 345,
                Width = 500,
                Height = 80,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White
            };
            btnApplySentence = CreateButton("应用", 420, 430, BtnApplySentence_Click);

            // 操作按钮
            btnGenerate = CreateButton("生成", 10, 470, BtnGenerate_Click);
            btnPreGenerate = CreateButton("预生成", 100, 470, BtnPreGenerate_Click);
            btnPlay = CreateButton("播放", 190, 470, BtnPlay_Click);
            btnStop = CreateButton("停止", 280, 470, BtnStop_Click);

            lblStatus = new Label
            {
                Text = "状态：就绪",
                AutoSize = false,
                Left = 0,
                Top = 510,
                Width = 960,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.LightGreen
            };

            panelRight.Controls.AddRange(new Control[]
            {
                lblGap, numGapMs,
                lblOverlap, numOverlapMs,
                lblVoiceDelay, numVoiceDelayMs,
                lblSpeed, numSpeed,
                lblPitch, numPitch,
                lblReverb, numReverb,
                lblOutput, txtOutput, btnBrowseOutput,
                lblSentenceInput, txtSentenceInput, btnApplySentence,
                btnGenerate, btnPreGenerate, btnPlay, btnStop
            });

            Controls.Add(panelLeft);
            Controls.Add(panelRight);
            Controls.Add(lblStatus);
        }

        private Button CreateButton(string text, int x, int y, EventHandler click)
        {
            var button = new Button
            {
                Text = text,
                Left = x,
                Top = y,
                Width = 80,
                Height = 28,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            button.Click += click;
            return button;
        }

        private ListBox CreateListBox(int x, int y, int w, int h)
        {
            return new ListBox
            {
                Left = x,
                Top = y,
                Width = w,
                Height = h,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private NumericUpDown CreateNumericUpDown(int x, int y, int min, int max, int val)
        {
            return new NumericUpDown
            {
                Left = x,
                Top = y,
                Width = 80,
                Minimum = min,
                Maximum = max,
                Value = val,
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (Directory.Exists(txtFolder.Text))
                    fbd.SelectedPath = txtFolder.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtFolder.Text = fbd.SelectedPath;
                    LoadWordList();
                }
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e) => LoadWordList();

        private void LoadWordList()
        {
            lstAvailableWords.Items.Clear();
            if (!Directory.Exists(txtFolder.Text))
            {
                MessageBox.Show("文件夹不存在。");
                return;
            }
            foreach (var f in Directory.GetFiles(txtFolder.Text, "*.ogg"))
                lstAvailableWords.Items.Add(Path.GetFileNameWithoutExtension(f));
            lblStatus.Text = $"已加载 {lstAvailableWords.Items.Count} 个文件。";
        }

        private void AddSelectedWord()
        {
            if (lstAvailableWords.SelectedItem != null)
                lstSentenceWords.Items.Add(lstAvailableWords.SelectedItem);
        }

        private void RemoveSelectedWord()
        {
            if (lstSentenceWords.SelectedIndex >= 0)
                lstSentenceWords.Items.RemoveAt(lstSentenceWords.SelectedIndex);
        }

        private void MoveSelectedSentenceWord(int off)
        {
            int i = lstSentenceWords.SelectedIndex;
            if (i < 0) return;
            int ni = i + off;
            if (ni < 0 || ni >= lstSentenceWords.Items.Count) return;
            var w = lstSentenceWords.Items[i];
            lstSentenceWords.Items.RemoveAt(i);
            lstSentenceWords.Items.Insert(ni, w);
            lstSentenceWords.SelectedIndex = ni;
        }

        private void BtnBrowseOutput_Click(object s, EventArgs e)
        {
            using (var sfd = new SaveFileDialog() { Filter = "WAV 文件|*.wav", FileName = "output.wav" })
                if (sfd.ShowDialog() == DialogResult.OK)
                    txtOutput.Text = sfd.FileName;
        }

        private async void BtnGenerate_Click(object sender, EventArgs e)
        {
            if (lstSentenceWords.Items.Count == 0)
            {
                MessageBox.Show("句子为空。");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtOutput.Text))
            {
                MessageBox.Show("请选择输出路径。");
                return;
            }

            string folder = txtFolder.Text;
            string[] words = lstSentenceWords.Items.Cast<string>().ToArray();
            float gap = (float)numGapMs.Value;
            float overlap = (float)numOverlapMs.Value;
            float speed = (float)numSpeed.Value / 100f;
            float pitch = (float)numPitch.Value;
            float voiceDelay = (float)numVoiceDelayMs.Value;
            float reverbLevel = (float)numReverb.Value;

            lblStatus.Text = "生成中...";
            await System.Threading.Tasks.Task.Run(() =>
                OggSentenceBuilder.BuildSentence(
                    folder, words, txtOutput.Text,
                    gap, speed, pitch, overlap, voiceDelay, reverbLevel));
            lblStatus.Text = "生成完成，可播放或查看文件。";
        }

        /// 预生成（内存模式）
        private async void BtnPreGenerate_Click(object sender, EventArgs e)
        {
            if (lstSentenceWords.Items.Count == 0)
            {
                MessageBox.Show("句子为空。");
                return;
            }
            string folder = txtFolder.Text;
            string[] words = lstSentenceWords.Items.Cast<string>().ToArray();
            float gap = (float)numGapMs.Value;
            float overlap = (float)numOverlapMs.Value;
            float speed = (float)numSpeed.Value / 100f;
            float pitch = (float)numPitch.Value;
            float voiceDelay = (float)numVoiceDelayMs.Value;
            float reverbLevel = (float)numReverb.Value;

            lblStatus.Text = "预生成中...";
            btnPreGenerate.Enabled = false;

            try
            {
                _previewAudio = await System.Threading.Tasks.Task.Run(() =>
                {
                    using (var ms = new System.IO.MemoryStream())
                    {
                        OggSentenceBuilder.BuildSentence(
                            folder, words, ms,
                            gap, speed, pitch, overlap, voiceDelay, reverbLevel);
                        return new System.IO.MemoryStream(ms.ToArray());
                    }
                });
                lblStatus.Text = "预生成完成，可直接播放。";
            }
            catch (Exception ex)
            {
                MessageBox.Show("预生成失败：" + ex.Message);
            }
            finally
            {
                btnPreGenerate.Enabled = true;
            }
        }

        private void BtnPlay_Click(object s, EventArgs e)
        {
            if (_previewAudio != null)
            {
                _player.Play(_previewAudio);
                lblStatus.Text = "正在播放预生成音频。";
            }
            else if (File.Exists(txtOutput.Text))
            {
                _player.Play(txtOutput.Text);
                lblStatus.Text = "播放文件：" + Path.GetFileName(txtOutput.Text);
            }
            else
            {
                MessageBox.Show("没有预生成内容或输出文件。");
            }
        }

        private void BtnStop_Click(object s, EventArgs e)
        {
            _player.Stop();
            lblStatus.Text = "停止播放。";
        }

        private void BtnApplySentence_Click(object s, EventArgs e)
        {
            if (lstAvailableWords.Items.Count == 0)
            {
                MessageBox.Show("请先加载单词列表。");
                return;
            }

            var all = lstAvailableWords.Items.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tokens = txtSentenceInput.Text.Split(
                new[] { ' ', ',', '.', '!', '?', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            lstSentenceWords.Items.Clear();
            foreach (var t in tokens)
                if (all.Contains(t.ToLower()))
                    lstSentenceWords.Items.Add(t.ToLower());

            lblStatus.Text = $"已生成句子，共 {lstSentenceWords.Items.Count} 个单词。";
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(_configPath))
                    return;

                using (var fs = File.OpenRead(_configPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppConfig));
                    var cfg = ser.ReadObject(fs) as AppConfig;
                    if (cfg == null) return;

                    if (!string.IsNullOrEmpty(cfg.FolderPath))
                        txtFolder.Text = cfg.FolderPath;

                    if (!string.IsNullOrEmpty(cfg.OutputPath))
                        txtOutput.Text = cfg.OutputPath;

                    if (cfg.GapMs >= numGapMs.Minimum && cfg.GapMs <= numGapMs.Maximum)
                        numGapMs.Value = cfg.GapMs;

                    if (cfg.OverlapMs >= numOverlapMs.Minimum && cfg.OverlapMs <= numOverlapMs.Maximum)
                        numOverlapMs.Value = cfg.OverlapMs;

                    if (cfg.VoiceDelayMs >= numVoiceDelayMs.Minimum && cfg.VoiceDelayMs <= numVoiceDelayMs.Maximum)
                        numVoiceDelayMs.Value = cfg.VoiceDelayMs;

                    if (cfg.SpeedPercent >= numSpeed.Minimum && cfg.SpeedPercent <= numSpeed.Maximum)
                        numSpeed.Value = cfg.SpeedPercent;

                    if (cfg.PitchSemitones >= numPitch.Minimum && cfg.PitchSemitones <= numPitch.Maximum)
                        numPitch.Value = cfg.PitchSemitones;

                    if (!string.IsNullOrEmpty(cfg.SentenceInput))
                        txtSentenceInput.Text = cfg.SentenceInput;

                    if (cfg.ReverbLevel >= numReverb.Minimum && cfg.ReverbLevel <= numReverb.Maximum)
                        numReverb.Value = cfg.ReverbLevel;
                }

                if (!string.IsNullOrWhiteSpace(txtFolder.Text) && Directory.Exists(txtFolder.Text))
                {
                    LoadWordList();
                }
            }
            catch
            {
            }
        }

        private void SaveConfig()
        {
            try
            {
                var cfg = new AppConfig
                {
                    FolderPath = txtFolder.Text ?? string.Empty,
                    OutputPath = txtOutput.Text ?? string.Empty,
                    GapMs = numGapMs.Value,
                    OverlapMs = numOverlapMs.Value,
                    VoiceDelayMs = numVoiceDelayMs.Value,
                    SpeedPercent = numSpeed.Value,
                    PitchSemitones = numPitch.Value,
                    SentenceInput = txtSentenceInput.Text ?? string.Empty,
                    ReverbLevel = numReverb.Value
                };

                Directory.CreateDirectory(Path.GetDirectoryName(_configPath) ?? AppDomain.CurrentDomain.BaseDirectory);

                using (var fs = File.Create(_configPath))
                {
                    var ser = new DataContractJsonSerializer(typeof(AppConfig));
                    ser.WriteObject(fs, cfg);
                }
            }
            catch
            {
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig();
            _player.Dispose();
            base.OnFormClosing(e);
        }
    }
}
