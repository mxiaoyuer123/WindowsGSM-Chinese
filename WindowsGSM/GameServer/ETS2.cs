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
    class ETS2
    {

        // 存储服务器配置数据的对象
        private readonly ServerConfig _serverData;

        // 错误消息和通知消息
        public string Error;
        public string Notice;

        // 服务器全名、启动路径、是否允许嵌入控制台、端口号增量、查询方法等服务器信息
        public const string FullName = "欧洲卡车模拟2 专用服务器";
        public string StartPath = @"bin\win_x64\eurotrucks2_server.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();

        // 默认配置项：端口号、查询端口、默认地图、最大玩家数、额外参数及应用 ID
        public string Port = "27015";
        public string QueryPort = "27016";
        public string Defaultmap = "";
        public string Maxplayers = "8";
        public string Additional = $"-password \"123456\" -saveinterval 1800 -backupshort 7200 -backuplong 43200 -instanceid \"1\" -Public 1"; // 额外的服务器启动参数
        public string AppId = "1948160";

        // 构造函数，需要传入服务器配置数据对象
        public ETS2(Functions.ServerConfig serverData)
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
            string param = " -server -nosingle";
            param += $" {_serverData.ServerParam}";
            await Task.Delay(1000);
            modifyConfigFile();
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
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "valheim_server.exe"));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, "valheim_server.exe");
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

        public async void modifyConfigFile()
        {
            // Get the path to the My Documents folder for the current user
            string documentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // Specify the path to your server_config.sii file
            string ets = "Euro Truck Simulator 2";
            string configFile = "server_config.sii";

            string filePath = Path.Combine(documentsFolderPath, ets, configFile);

            string serverPath = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string sii = "server_packages.sii";
            string dat = "server_packages.dat";
            string savePath = Path.Combine(serverPath, "save");

            // Specify the new values
            string ServerName = _serverData.ServerName;
            string Port = _serverData.ServerQueryPort;
            string QueryPort = _serverData.ServerQueryPort;
            string GLST = _serverData.ServerGSLT;

            try
            {
                // Read all lines from the file
                string[] lines = File.ReadAllLines(filePath);

                // Use a regular expression to find and replace the values
                for (int i = 0; i < lines.Length; i++)
                {
                    // Modify lobby_name
                    if (lines[i].Contains("lobby_name"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"""(.*)""", $"\"{ServerName}\"");
                    }

                    // Modify connection_dedicated_port
                    if (lines[i].Contains("connection_dedicated_port"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"\d+", Port);
                    }

                    // Modify query_dedicated_port
                    if (lines[i].Contains("query_dedicated_port"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"\d+", QueryPort);
                    }

                    // Modify GSLT
                    if (lines[i].Contains("server_logon_token"))
                    {
                        lines[i] = Regex.Replace(lines[i], @"\d+", GLST);
                    }
                }

                // Write the modified lines back to the file
                File.WriteAllLines(filePath, lines);

                Notice = "File modified successfully!";
            }
            catch (Exception ex)
            {
                Error = $"Error: {ex.Message}";
            }

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            // Automatically copy server_packages.sii and server_packages.dat
            if (File.Exists(Path.Combine(documentsFolderPath, ets, sii)) && File.Exists(Path.Combine(documentsFolderPath, ets, dat)))
            {
                try
                {
                    File.Copy(Path.Combine(documentsFolderPath, ets, sii), Path.Combine(savePath, sii), true);
                    File.Copy(Path.Combine(documentsFolderPath, ets, dat), Path.Combine(savePath, dat), true);
                    Notice = $"{sii} and {dat} File has been copied to {savePath}";
                }
                catch (Exception ex)
                {
                    Error = $"Error: {ex.Message}";
                }
            }

            await Task.Delay(1000);
        }
    }
}
