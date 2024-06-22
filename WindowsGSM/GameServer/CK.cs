using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System;
using WindowsGSM.Functions;
using Discord;
using System.Net.Sockets;
using System.Windows.Documents;
using System.Linq;

namespace WindowsGSM.GameServer
{
	class CK
	{

		// 存储服务器配置数据的对象
		private readonly ServerConfig _serverData;

		// 错误消息和通知消息
		public string Error;
		public string Notice;

		// 服务器全名、启动路径、是否允许嵌入控制台、端口号增量、查询方法等服务器信息
		public const string FullName = "地心护核者 专用服务器";
		public string StartPath = @"Launch.bat";
		public bool AllowsEmbedConsole = true;
		public int PortIncrements = 2;
		public dynamic QueryMethod = new Query.A2S();

		// 默认配置项：端口号、查询端口、默认地图、最大玩家数、额外参数及应用 ID
		public string Port = "9000";
		public string QueryPort = "9100";
		public string Defaultmap = "map";
		public string Maxplayers = "100";
		public string Additional = $""; // 额外的服务器启动参数
		public string AppId = "1963720";

		// 构造函数，需要传入服务器配置数据对象
		public CK(Functions.ServerConfig serverData)
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
			string param = $"-batchmode {_serverData.ServerParam}" + (!AllowsEmbedConsole ? " -log" : string.Empty);
			var p = new Process
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

			// Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
			if (AllowsEmbedConsole)
			{
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardInput = true;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				var serverConsole = new ServerConsole(_serverData.ServerID);
				p.OutputDataReceived += serverConsole.AddOutput;
				p.ErrorDataReceived += serverConsole.AddOutput;

				// Start Process
				try
				{
					p.Start();
				}
				catch (Exception e)
				{
					Error = e.Message;
					return null; // return null if fail to start
				}

				p.BeginOutputReadLine();
				p.BeginErrorReadLine();
				return p;
			}

			// Start Process
			try
			{
				p.Start();
				return p;
			}
			catch (Exception e)
			{
				Error = e.Message;
				return null; // return null if fail to start
			}
		}


		public async Task Stop(Process p)
		{
			Process coreKeeperProcess = Process.GetProcessesByName("CoreKeeperServer").FirstOrDefault();

			await Task.Run(() =>
			{
				Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
				Functions.ServerConsole.SendWaitToMainWindow("q");
				if (coreKeeperProcess != null&&!coreKeeperProcess.HasExited)
				{
					coreKeeperProcess.Kill();
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
			return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "CoreKeeperServer.exe"));
		}

		public bool IsImportValid(string path)
		{
			string exePath = Path.Combine(path, "CoreKeeperServer.exe");
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