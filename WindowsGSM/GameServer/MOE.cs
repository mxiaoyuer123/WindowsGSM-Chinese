using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System;
using WindowsGSM.Functions;
using Discord;
using System.Net.Sockets;
using System.Windows.Documents;
using System.Net;
using Microsoft.VisualBasic.Logging;

namespace WindowsGSM.GameServer
{
     class MOE
    {       // 存储服务器配置数据的对象
        private readonly ServerConfig _serverData;

        // 错误消息和通知消息
        public string Error;
        public string Notice;
        // - Settings properties for SteamCMD installer
        public  bool loginAnonymous = false;
        // 服务器全名、启动路径、是否允许嵌入控制台、端口号增量、查询方法等服务器信息
        public const string FullName = "帝国神话 专用服务器";
        public string StartPath = @"MOE\Binaries\Win64\MOEServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();
        // 默认配置项：端口号、查询端口、默认地图、最大玩家数、额外参数及应用 ID
        public string Port = "7777";
        public string QueryPort = "7778";
        public string Defaultmap = "LargeTerrain_Central_Main";
        public string Maxplayers = "150";
        public string Additional = $"-GameServerPVPType=1 -MapDifficultyRate=1 -UseACE -EnableVACBan=1 -ServerId=100 -ClusterId=1 -bStartShutDownServiceInPrivateServer=true -ShutDownServiceIP=127.0.0.1 -ShutDownServicePort=13888 -ShutDownServiceKey=351703 -ServerAdminAccounts=76561198328820250"; // Additional server start parameter
        public string AppId = "1794810";
        // 构造函数，需要传入服务器配置数据对象
        public MOE(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        // - 在安装后为游戏服务器创建一个默认的 cfg
        public async void CreateServerCFG()
        {

        }
        // 启动服务器进程
        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {

            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} 未找到 ({shipExePath})";
                return null;
            }

            //Get WAN IP from net
            string externalIpString = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            var externalIp = IPAddress.Parse(externalIpString);

            // Prepare start parameter
            string param = $"{_serverData.ServerMap} -game -server -DataLocalFile -NotCheckServerSteamAuth -log log={_serverData.ServerName}  -LOCALLOGTIMES -PrivateServer -disable_qim -MultiHome={_serverData.ServerIP} -OutAddress={externalIp.ToString()} -SessionName={_serverData.ServerName} -MaxPlayers={_serverData.ServerMaxPlayer} -Port={_serverData.ServerPort} -QueryPort={_serverData.ServerQueryPort}";// Set basic parameters

            param += string.IsNullOrWhiteSpace(_serverData.ServerParam) ? string.Empty : $" {_serverData.ServerParam}";
         

            Process p; // 创建一个 Process 实例来运行服务器进程

            // 如果不允许嵌入控制台，则以最小化窗口样式启动进程
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID), // 设置工作目录为服务器文件所在目录
                        FileName = shipExePath, // 设置要运行的可执行文件路径
                        Arguments = param, // 设置传递给可执行文件的参数
                        WindowStyle = ProcessWindowStyle.Minimized, // 设置窗口样式为最小化
                        UseShellExecute = false // 设置不使用操作系统外壳程序启动进程
                    },
                    EnableRaisingEvents = true // 启用进程事件
                };
                p.Start(); // 启动进程
            }
            else
            {
                // 如果允许嵌入控制台，则以隐藏窗口样式启动进程，并重定向标准输出和标准错误流
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID), // 设置工作目录为服务器文件所在目录
                        FileName = shipExePath, // 设置要运行的可执行文件路径
                        Arguments = param, // 设置传递给可执行文件的参数
                        WindowStyle = ProcessWindowStyle.Hidden, // 设置窗口样式为隐藏
                        CreateNoWindow = true, // 设置不创建窗口
                        UseShellExecute = false, // 设置不使用操作系统外壳程序启动进程
                        RedirectStandardOutput = true, // 设置重定向标准输出流
                        RedirectStandardError = true // 设置重定向标准错误流
                    },
                    EnableRaisingEvents = true // 启用进程事件
                };

                // 创建一个服务器控制台实例
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);

                // 将进程的标准输出和标准错误流数据传递给服务器控制台实例的输出处理方法
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;

                p.Start(); // 启动进程

                // 开始异步读取进程的标准输出和标准错误流
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p; // 返回创建的进程实例

        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                if (p.StartInfo.CreateNoWindow)
                {
                    Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                    Functions.ServerConsole.SendWaitToMainWindow("^c");

                }
                else
                {
                    Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                    Functions.ServerConsole.SendWaitToMainWindow("^c");
                }
            });
            await Task.Delay(20000);
        }

        // 安装游戏服务端
        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            // 使用 SteamCMD 安装服务端
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
            Error = steamCMD.Error;

            return p;
        }

        // 升级游戏服务端
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            // 使用 SteamCMD 更新服务端
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            return p;
        }

        // 在服务器文件夹中检查游戏服务端是否已正确安装
        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, StartPath);
            Error = $"无效路径！找不到 {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        // 获取本地游戏服务端的版本号
        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        // 获取官方游戏服务端的版本号
        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
