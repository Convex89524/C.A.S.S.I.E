using System;
using System.IO;

namespace OggDataPacker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "OggDataPacker - 打包/解包 OGG 到 .data";

            if (args.Length == 0)
            {
                ShowMenu();
                return;
            }
            
            try
            {
                var command = args[0].ToLowerInvariant();
                switch (command)
                {
                    case "pack":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: OggDataPacker pack <ogg_folder> <output.data>");
                            return;
                        }
                        PackFromCommand(args[1], args[2]);
                        break;

                    case "unpack":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("用法: OggDataPacker unpack <input.data> <output_folder>");
                            return;
                        }
                        UnpackFromCommand(args[1], args[2]);
                        break;

                    default:
                        Console.WriteLine("未知命令: " + command);
                        ShowMenu();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生错误: " + ex.Message);
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine("==== OggDataPacker ====");
            Console.WriteLine("1. 打包 .ogg 文件夹 -> .data");
            Console.WriteLine("2. 解包 .data -> 文件夹");
            Console.WriteLine("Q. 退出");
            Console.WriteLine("=======================");
            Console.Write("请选择: ");

            var key = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(key))
                return;

            switch (key.Trim().ToLowerInvariant())
            {
                case "1":
                    PackInteractive();
                    break;
                case "2":
                    UnpackInteractive();
                    break;
                case "q":
                    return;
                default:
                    Console.WriteLine("无效选项。");
                    break;
            }
        }

        private static void PackInteractive()
        {
            Console.Write("请输入 OGG 文件所在文件夹路径: ");
            var folder = Console.ReadLine() ?? string.Empty;
            folder = folder.Trim('"');

            if (!Directory.Exists(folder))
            {
                Console.WriteLine("文件夹不存在。");
                return;
            }

            Console.Write("请输入输出 .data 文件路径(例如 output.data): ");
            var output = Console.ReadLine() ?? string.Empty;
            output = output.Trim('"');

            try
            {
                OggDataArchive.CreateArchive(folder, output);
                Console.WriteLine("打包完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine("打包失败: " + ex.Message);
            }
        }

        private static void UnpackInteractive()
        {
            Console.Write("请输入 .data 资源包路径: ");
            var archivePath = Console.ReadLine() ?? string.Empty;
            archivePath = archivePath.Trim('"');

            if (!File.Exists(archivePath))
            {
                Console.WriteLine(".data 文件不存在。");
                return;
            }

            Console.Write("请输入解包输出文件夹路径: ");
            var outputFolder = Console.ReadLine() ?? string.Empty;
            outputFolder = outputFolder.Trim('"');

            try
            {
                OggDataArchive.ExtractArchive(archivePath, outputFolder);
                Console.WriteLine("解包完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine("解包失败: " + ex.Message);
            }
        }

        private static void PackFromCommand(string folder, string output)
        {
            folder = folder.Trim('"');
            output = output.Trim('"');

            if (!Directory.Exists(folder))
            {
                Console.WriteLine("文件夹不存在: " + folder);
                return;
            }

            Console.WriteLine("正在打包...");
            OggDataArchive.CreateArchive(folder, output);
            Console.WriteLine("打包完成: " + output);
        }

        private static void UnpackFromCommand(string archivePath, string outputFolder)
        {
            archivePath = archivePath.Trim('"');
            outputFolder = outputFolder.Trim('"');

            if (!File.Exists(archivePath))
            {
                Console.WriteLine(".data 文件不存在: " + archivePath);
                return;
            }

            Console.WriteLine("正在解包...");
            OggDataArchive.ExtractArchive(archivePath, outputFolder);
            Console.WriteLine("解包完成: " + outputFolder);
        }
    }
}
