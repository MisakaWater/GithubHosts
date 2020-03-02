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
using System.Threading;
using System.Collections.Generic;

namespace GithubHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var Count = 1;
            var core = new Core();
            while (!core.Iteration())
            {
                Console.WriteLine($"第{Count}次测试");
                Count++;
                if (Count==10)
                {
                    Console.WriteLine("已尝试10次，请检查网络情况。");
                    break;
                }
            }
            core.ConsoleWait(3);
        }

    }
    class Core
    {
        private Dictionary<PlatformID, string> _hostPath;
        private List<string> _hostName;
        private OperatingSystem _OS;
        private IPAddress[] _addr;
        private Dictionary<PlatformID, Func<bool>> _funcArr;
        public Core()
        {
            _hostPath = new Dictionary<PlatformID, string>()
            {
                {PlatformID.Win32NT,@"C:\Windows\System32\drivers\etc\hosts"},
                {PlatformID.Unix,@"/etc/hosts"}
            };
            _hostName = new List<string>()
            {
                "github.com", 
                "github-cloud.s3.amazonaws.com", 
                "codeload.github.com",
                "raw.githubusercontent.com"
            };
            _OS = Environment.OSVersion;
            _addr = new IPAddress[0];
            _funcArr = new Dictionary<PlatformID, Func<bool>>()
            {
                {PlatformID.Win32NT,WinNtFlushDns},
                {PlatformID.Unix,LinuxFlushDns}
            };
        }
        public bool Iteration()
        {
            Array.Resize(ref _addr, _hostName.Count);
            for (int i = 0; i < _hostName.Count; i++)
            {
                _addr[i] = LookUpAsync(_hostName[i]);
            }

            SetHosts();
            return _funcArr.FirstOrDefault(f => f.Key == _OS.Platform).Value();
        }
        bool LinuxFlushDns()
        {//linux下一般没有dns缓存
            return true;
        }

        bool WinNtFlushDns()
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C ipconfig /flushdns";
            startInfo.RedirectStandardOutput = false;
            process.StartInfo = startInfo;
            process.Start();
            process.Close();

            Console.WriteLine("下载速度测试...");
            Console.WriteLine("Ctrl + C 停止测试");
            var speed = SpeedTest();
            Console.WriteLine("测试下载速度: " + speed + "kbps");
            if (speed < 300)
            {
                Console.WriteLine("测试下载速度 < 300kbps");
                for (int i = 0; i < 29; i++)
                {
                    Console.Write("/");
                }
                Console.Write(Environment.NewLine);
                return false;
            }
            Console.WriteLine("Hosts设置成功！");
            return true;
        }
        public void ConsoleWait(int s)
        {
            for (int i = s; i > 0; i--)
            {
                Console.CursorLeft = 0;
                Console.Write($"{i}秒后自动关闭");
                Thread.Sleep(1000);
            }
        }
        long SpeedTest()
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
        void SetHosts()
        {
            string Path = _hostPath[_OS.Platform];

            var FileStringArray = File.ReadLines(Path).ToArray();
            string FileString = string.Empty;

            for (int i = 0; i < FileStringArray.Length; i++)
            {
                if (FileStringArray[i] != "" && FileStringArray[i].Substring(0, 1) != "#")
                {
                    for (int j = 0; j < _hostName.Count; j++)
                    {
                        if (FileStringArray[i].ToLower().Contains(_hostName[j]))
                        {
                            FileStringArray[i] = _addr[j] + " " + _hostName[j] + "#GithubHost.exe";
                        }

                    }

                }
                FileString += FileStringArray[i] + Environment.NewLine;
            }

            File.WriteAllText(Path, FileString);
        }

        IPAddress LookUpAsync(string Host)
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
