using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using DnsClient;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static GithubHost.WinHelper;
using System.Threading;

namespace GithubHost
{
    class WinHelper
    {
        [ComImport, Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"), ClassInterface(ClassInterfaceType.None)]
        public class NetworkListManager : INetworkCostManager
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            public virtual extern void GetCost(out uint pCost, [In] IntPtr pDestIPAddr);
        }


        [ComImport, Guid("DCB00008-570F-4A9B-8D69-199FDBA5723B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface INetworkCostManager
        {
            void GetCost(out uint pCost, [In] IntPtr pDestIPAddr);
        }
    }
    class Program
    {
        readonly static string[] HostPath = new string[2] { @"C:\Windows\System32\drivers\etc\hosts", @"/etc/hosts" };
        readonly static string[] HostsName = new string[3] { "github.com", "github-cloud.s3.amazonaws.com", "codeload.github.com" };
        static IPAddress[] Addr = new IPAddress[0];
        static OperatingSystem OS;
        static int MainCount = 0;
        static void Main(string[] args)
        {
            Console.WriteLine($"第{MainCount}次测试");

            OS = Environment.OSVersion;
            Array.Resize(ref Addr, HostsName.Length);
            for (int i = 0; i < HostsName.Length; i++)
            {
                Addr[i] = LookUpAsync(HostsName[i]);
            }
            SetHosts();
            //cost=1不计费网络,cost=2计费网络
            //https://support.microsoft.com/zh-cn/help/4028458/windows-metered-connections-in-windows-10
            if (OS.Platform == PlatformID.Win32NT)
            {
                FlushDns();
                Console.WriteLine("下载速度测试...");
                Console.WriteLine("Ctrl + C 停止测试");
                new NetworkListManager().GetCost(out uint cost, IntPtr.Zero);
                if (cost == 1)
                {//不计费网络
                    var speed = SpeedTest();
                    Console.WriteLine("测试下载速度: " + speed + "kbps");
                    if (speed < 300)
                    {
                        Console.WriteLine("测试下载速度 < 300kbps");
                        string[] strArr = new string[] { };
                        for (int i = 0; i < 29; i++)
                        {
                            Console.Write("/");
                        }
                        Console.Write(Environment.NewLine);
                        MainCount++;
                        if (MainCount < 10)
                        {
                            Main(strArr);
                        }
                    }
                }
            }
            Console.WriteLine("Hosts设置成功！");
            for (int i = 3; i > 0; i--)
            {
                Console.CursorLeft = 0;
                Console.Write($"{i}秒后自动关闭");
                Thread.Sleep(1000);
            }
        }
        static void FlushDns()
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C ipconfig /flushdns";
            startInfo.RedirectStandardOutput = false;
            process.StartInfo = startInfo;
            process.Start();
            process.Close();
        }
        static long SpeedTest()
        {
            Stopwatch sw = new Stopwatch();
            WebClient webClient = new WebClient();
            long speed = -1;

            var task = Task.Run(() =>
            {
                sw.Start();
                var data = webClient.DownloadData(new Uri("https://github.com/twbs/bootstrap/archive/v4.4.1.zip"));
                sw.Stop();
                return data;
            });
            if (task.Wait(TimeSpan.FromSeconds(10)))
            {
                speed = task.Result.Length / 1024 / sw.Elapsed.Seconds;
                return speed;
            }
            else
            {
                return -1;
            }
        }
        static void SetHosts()
        {
            var Path = string.Empty;

            if (OS.Platform == PlatformID.Win32NT)
            {
                Path = HostPath[0];
            }
            else if (OS.Platform == PlatformID.Unix)
            {
                Path = HostPath[1];
            }

            var FileStringArray = File.ReadLines(Path).ToArray();
            string FileString = string.Empty;

            for (int i = 0; i < FileStringArray.Length; i++)
            {
                if (FileStringArray[i] != "" && FileStringArray[i].Substring(0, 1) != "#")
                {
                    for (int j = 0; j < HostsName.Length; j++)
                    {
                        if (FileStringArray[i].ToLower().Contains(HostsName[j]))
                        {
                            FileStringArray[i] = Addr[j] + " " + HostsName[j] + "#GithubHost.exe";
                        }

                    }

                }
                FileString += FileStringArray[i] + Environment.NewLine;
            }

            File.WriteAllText(Path, FileString);
        }

        static IPAddress LookUpAsync(string Host)
        {
            var lookup = new LookupClient();
            var result = lookup.Query(Host, QueryType.A);
            var record = result.Answers.ARecords().FirstOrDefault();
            var addr = record?.Address;
            if (addr == null)
            {
                throw new ArgumentNullException($"LookUp错误:没有返回{Host}的IP地址。");
            }
            return addr;
        }
    }
}
