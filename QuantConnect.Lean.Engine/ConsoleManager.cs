using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuantConnect.Lean.Engine
{
	public class ConsoleManager
	{
		// 屏蔽只在windows下才能用的函数
		[DllImport("Kernel32")]
		private static extern bool SetConsoleCtrlHandler(ConsoleManager.EventHandler handler, bool add);

		public event EventHandler<EventArgs> OnExit;

		private int exited = 0;

		private void DoExit()
		{
			if (Interlocked.CompareExchange(ref exited, 1, 0) == 0)
			{
				OnExit?.Invoke(this, EventArgs.Empty);
			}
		}

		public ConsoleManager()
		{
			// 屏蔽只在windows下才能用的函数
			 ConsoleManager.handler = (ConsoleManager.EventHandler)Delegate.Combine(ConsoleManager.handler, new ConsoleManager.EventHandler(this.Handler));
			 ConsoleManager.SetConsoleCtrlHandler(ConsoleManager.handler, true);

			AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
			Console.CancelKeyPress += Console_CancelKeyPress;
            

			// 异常时记录日志
			var _logDir = System.AppDomain.CurrentDomain.BaseDirectory;

			if (!Directory.Exists(_logDir))
			{
				Directory.CreateDirectory(_logDir);
			}
			AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs eventArgs) {
				File.AppendAllLines(Path.Combine(_logDir, "exception.txt"), new string[1]
				{
					$"{DateTime.Now}::{eventArgs.ExceptionObject.ToString()}"
				});
                DoExit();
            };
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			DoExit();
		}

		private void CurrentDomain_ProcessExit(object sender, EventArgs e)
		{
			DoExit();
		}

		private void Handler(ConsoleManager.CtrlType sig)
		{
			DoExit();
		}

		public void ProcessMessage(string message)
		{
			if (message == null || message.Length != 0)
			{
				if (message == "data")
				{
					Console.WriteLine("--> data");
					return;
				}
				if (message == "info")
				{
					Console.WriteLine("--> info");
					return;
				}
				if (message == "help")
				{
					Console.WriteLine("--> help");
					Console.WriteLine("data");
					Console.WriteLine("info");
					Console.WriteLine("help");
					Console.WriteLine("exit");
					return;
				}
				Console.WriteLine("--> " + message);
				Console.WriteLine("Unknown console command. Please type help and press Enter to see the list of supported commands.");
			}
		}

		private static ConsoleManager.EventHandler handler;

		private delegate void EventHandler(ConsoleManager.CtrlType sig);

		public enum CtrlType
		{
			CTRL_C_EVENT,
			CTRL_BREAK_EVENT,
			CTRL_CLOSE_EVENT,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT
		}
	}
}
