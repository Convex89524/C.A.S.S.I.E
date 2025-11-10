using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace C.A.S.S.I.E
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        
        public static Version Version = new Version("1.0.1");

        [STAThread]
        static void Main()
        {
            IntPtr handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var message =
                "免责声明 / Disclaimer\n\n" +
                "1. 您需要拥有合法的本地《SCP: Secret Laboratory》客户端并体验游戏后，方可使用此工具。\n" +
                "2. 您必须自行承担使用本工具所产生的一切后果。\n" +
                "3. 北木工作室（Northwood Studios）拥有相关游戏及音频资源的全部权利。\n\n" +
                "By clicking \"同意 / Accept\", you acknowledge and agree that:\n" +
                "- You legally own a copy of SCP: Secret Laboratory and its assets.\n" +
                "- All C.A.S.S.I.E. voice assets are the exclusive property of Northwood Studios.\n" +
                "- You are solely responsible for any consequences resulting from the use of this tool.\n\n" +
                "是否已阅读并同意以上免责声明？";

            var result = MessageBox.Show(
                message,
                "C.A.S.S.I.E Sentence Builder - 免责声明",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return;
            }

            Application.Run(new MainForm());
        }
    }
}