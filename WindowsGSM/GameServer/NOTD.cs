using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsGSM.Functions;

namespace WindowsGSM.GameServer
{
     class NOTD
    {
        // 存储服务器配置数据的对象
        private readonly ServerConfig _serverData;

        // 错误消息和通知消息
        public string Error;
        public string Notice;

        // 服务器全名、启动路径、是否允许嵌入控制台、端口号增量、查询方法等服务器信息
        public const string FullName = "死亡之夜 专用服务器";
        public string StartPath = @"LFServer.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();

        // 默认配置项：端口号、查询端口、默认地图、最大玩家数、额外参数及应用 ID
        public string Port = "5755";
        public string QueryPort = "5756";
        public string Defaultmap = "Dedicated";
        public string Maxplayers = "64";
        public string Additional = $""; // 额外的服务器启动参数
        public string AppId = "1420710";

        // 构造函数，需要传入服务器配置数据对象
        public NOTD(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        // - 在安装后为游戏服务器创建一个默认的 cfg
        public async void CreateServerCFG()
        {
            //string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"ServerSettings.ini");
            //if (await Functions.Github.DownloadGameServerConfig(configPath, "Night of the Dead Dedicated Server"))
            //{
            //    string configText = File.ReadAllText(configPath);
            //    configText = configText.Replace("{{ServerName}}", _serverData.ServerName);
            //    configText = configText.Replace("{{ServerPassword}}", _serverData.GetRCONPassword());
            //    configText = configText.Replace("{{ServerMaxplayers}}", _serverData.ServerMaxPlayer);
            //    configText = configText.Replace("{{ServerName}}", _serverData.ServerMap);
            //    File.WriteAllText(configPath, configText);
            //}
        }
        public void UpdateConfig()
        {
            // 读取配置文件
            string configFile = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "ServerSettings.ini");
            string fileContent = File.ReadAllText(configFile);

            // 使用正则表达式进行替换
            fileContent = Regex.Replace(fileContent, @"ServerName=.*", "ServerName=" + _serverData.ServerName);
            fileContent = Regex.Replace(fileContent, @"MaxPlayers=.*", "MaxPlayers=" + _serverData.ServerMaxPlayer);
            fileContent = Regex.Replace(fileContent, @"SaveName=.*", "SaveName=" + _serverData.ServerMap);

            // 将更新后的内容写回文件
            File.WriteAllText(configFile, fileContent);


            string configFile1 = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "LF\\Saved\\Config\\ServerSettings.ini");

            // 获取目标目录的路径
            string destinationDirectory = Path.GetDirectoryName(configFile1);

            // 如果目标目录不存在，则创建目录
            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // 复制文件
            File.Copy(configFile, configFile1, true);
        }
        // 启动服务器进程
        public async Task<Process> Start()
        {

            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} 未找到 ({shipExePath})";
                return null;
            }
            string param = $"?listen -Port {_serverData.ServerPort} -QueryPort={_serverData.ServerQueryPort} {_serverData.ServerParam} -CRASHREPORTS" + (!AllowsEmbedConsole ? " -log" : string.Empty);
            UpdateConfig();
            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = shipExePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = shipExePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            return p;
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
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
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "LFServer.exe"));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "LFServer.exe");
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

