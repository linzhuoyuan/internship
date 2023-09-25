/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Util;
using PythonRuntime = Python.Runtime;

namespace QuantConnect.Lean.Launcher
{
    public class Program
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private static readonly bool IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        private static readonly char PathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? ':' : ';';
        private static string GetConfigName(string name) => IsLinux ? $"linux-{name}" : string.Empty + name;

        private const string _collapseMessage = "Unhandled exception breaking past controls and causing collapse of algorithm node. This is likely a memory leak of an external dependency or the underlying OS terminating the LEAN engine.";

        //zt 
        //todo 回测阶段暂不处理
        //static public IClientLogInterface logRecv = null;
        public static Process proc = null;

        //zt 改为成员变量
        private static LeanEngineSystemHandlers leanEngineSystemHandlers = null;
        private static LeanEngineAlgorithmHandlers leanEngineAlgorithmHandlers = null;
        private static AlgorithmNodePacket job;
        private static AlgorithmManager algorithmManager;
        private static int exitCode = 0;

        private static void PythonInit()
        {
            if (!Config.GetBool("python-enable") && Config.Get("algorithm-language") != "Python")
            {
                return;
            }

            if (PythonRuntime.PythonEngine.IsInitialized)
            {
                return;
            }

            var pythonHome = Config.Get(GetConfigName("python-home")).ReplaceByHomePath();
            var pythonDll = Config.Get(GetConfigName("python-dll")).ReplaceByHomePath();
            if (!File.Exists(pythonDll))
            {
                pythonDll = Path.Combine(pythonHome, pythonDll);
                if (!File.Exists(pythonDll))
                {
                    throw new FileNotFoundException($"python-dll:{pythonDll}, 路径错误!");
                }
            }

            PythonRuntime.Runtime.PythonDLL = pythonDll;
            Log.Trace($"PythonRuntime: PythonDLL {pythonDll}");

            PythonRuntime.PythonEngine.PythonHome = pythonHome;
            Log.Trace($"PythonRuntime: PythonHome {pythonHome}");

            var defaultPythonPath = PythonRuntime.PythonEngine.PythonPath
                .Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToArray();
            var userPythonPath = JsonConvert.DeserializeObject<string[]>(Config.Get(GetConfigName("python-path")))
                .Select(n =>
                {
                    n = n.ReplaceByHomePath();
                    return !Directory.Exists(n) ? Path.Combine(pythonHome, n) : n;
                })
                .Where(Directory.Exists)
                .ToArray();

            var pythonPath = defaultPythonPath.Union(userPythonPath).ToList();
            foreach (var path in pythonPath)
            {
                Log.Trace($"PythonRuntime: PythonPath {path}");
            }

            PythonRuntime.PythonEngine.PythonPath = string.Join(PathSeparator, pythonPath);
        }

        public static void Main(string[] args)
        {
            //Initialize:
            var mode = "RELEASE";
#if DEBUG
            mode = "DEBUG";
#endif

            if (OS.IsWindows)
            {
                //zt
                //todo 回测阶段暂不处理
                //if (logRecv == null)
                //
                //Console.OutputEncoding = System.Text.Encoding.UTF8;
                WinNative.DisableQuickEdit();
            }

            // expect first argument to be config file name
            if (args.Length > 0)
            {
                Config.MergeCommandLineArgumentsWithConfiguration(LeanArgumentParser.ParseArguments(args));
            }

            PythonInit();

            var environment = Config.Get("environment");
            var liveMode = Config.GetBool("live-mode");
            if (!liveMode)
            {
                if (Config.GetBool("sync-data"))
                {
                    SyncData();
                }
            }

            if (Config.GetBool("python-enabled"))
            {
                var pythonDll = Config.Get("python-dll");
                if (!File.Exists(pythonDll))
                {
                    throw new Exception($"python-dll:{pythonDll}, 路径错误!");
                }
                PythonRuntime.Runtime.PythonDLL = pythonDll;
                PythonRuntime.PythonEngine.PythonHome = Config.Get("python-home");
                //PythonRuntime.PythonEngine.PythonPath = Config.Get("python_path");
                //Environment.SetEnvironmentVariable("PYTHONHOME", Config.Get("python_home"));
                //Environment.SetEnvironmentVariable("PYTHONPATH", Config.Get("python_path"));
            }

            Log.DebuggingEnabled = Config.GetBool("debug-mode");
            Log.LogHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>(Config.Get("log-handler", "CompositeLogHandler"));
            //todo 回测阶段暂不处理
            //if (logRecv != null)
            //{
            //    Log.LogHandler = new CompositeLogHandler(new FunctionalLogHandler(logRecv.OnTraceLog, logRecv.OnTraceLog, logRecv.OnTraceLog), Log.LogHandler);
            //}
            //Name thread for the profiler:
            Thread.CurrentThread.Name = "Algorithm Analysis Thread";

            Log.Trace("Engine.Main(): LEAN ALGORITHMIC TRADING ENGINE v" + Globals.Version + " Mode: " + mode + " (" + (Environment.Is64BitProcess ? "64" : "32") + "bit)");
            Log.Trace("Engine.Main(): Started " + DateTime.Now.ToShortTimeString());

            //Import external libraries specific to physical server location (cloud/local)

            //zt  
            //LeanEngineSystemHandlers leanEngineSystemHandlers;
            //

            try
            {
                leanEngineSystemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            //Setup packaging, queue and controls system: These don't do much locally.
            leanEngineSystemHandlers.Initialize();

            //-> Pull job from QuantConnect job queue, or, pull local build:
            job = leanEngineSystemHandlers.JobQueue.NextJob(out var assemblyPath);

            if (job == null)
            {
                throw new Exception("Engine.Main(): Job was null.");
            }

            //zt
            //LeanEngineAlgorithmHandlers leanEngineAlgorithmHandlers;
            try
            {
                leanEngineAlgorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
            }
            catch (CompositionException compositionException)
            {
                Log.Error("Engine.Main(): Failed to load library: " + compositionException);
                throw;
            }

            if (environment.EndsWith("-desktop"))
            {
                if (!File.Exists(Config.Get("desktop-exe")))
                {
                    var message = $"desktop-exe path ({Config.Get("desktop-exe")}) does not exist. You may need to update this path with the build configuration (currently ${mode})";
                    Log.Error(message);
                    throw new FileNotFoundException(message);
                }
                var info = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    FileName = Config.Get("desktop-exe"),
                    Arguments = Config.Get("desktop-http-port")
                };
                //zt
                //Process.Start(info);
                proc = Process.Start(info);
                //
            }

            // if the job version doesn't match this instance version then we can't process it
            // we also don't want to reprocess redelivered jobs
            if (VersionHelper.IsNotEqualVersion(job.Version) || job.Redelivered)
            {
                Log.Error("Engine.Run(): Job Version: " + job.Version + "  Deployed Version: " + Globals.Version + " Redelivered: " + job.Redelivered);
                //Tiny chance there was an uncontrolled collapse of a server, resulting in an old user task circulating.
                //In this event kill the old algorithm and leave a message so the user can later review.
                leanEngineSystemHandlers.Api.SetAlgorithmStatus(job.AlgorithmId, AlgorithmStatus.RuntimeError, _collapseMessage);
                leanEngineSystemHandlers.Notify.SetAuthentication(job);
                leanEngineSystemHandlers.Notify.Send(new RuntimeErrorPacket(job.UserId, job.AlgorithmId, _collapseMessage));
                leanEngineSystemHandlers.JobQueue.AcknowledgeJob(job);
                return;
            }

            try
            {
                algorithmManager = new AlgorithmManager(liveMode);
                leanEngineSystemHandlers.LeanManager.Initialize(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, job, algorithmManager);

                var engine = new Engine.Engine(leanEngineSystemHandlers, leanEngineAlgorithmHandlers, liveMode);

                engine.Run(job, algorithmManager, assemblyPath);

            }
            finally
            {
                // Note: When algorithmManager exits abnormally, we deal with it first. Since the there is 
                //       a chance for algorithm exception to be sent to user's end, not so as the outter 
                //       loop exceptions.
                if (algorithmManager.ExitCode != 0)
                {
                    Exit(algorithmManager.ExitCode);
                }
                else
                {
                    var algorithmStatus = algorithmManager?.State ?? AlgorithmStatus.DeployError;
                    var exitCode = algorithmManager?.AlgorithmExitCode ?? 0;
                    Log.Trace($"When exiting, the algorithm's exit code is {exitCode}, algorithm status is {algorithmStatus.ToString()}");
                    // Bugfix: The algorithm status is not very reliable, some other leaf thread will assign the exit code but might not update the 
                    //         algorithm status. So the exit code is more reliable than the algorithm status.
                    //Exit(algorithmStatus != AlgorithmStatus.Completed ? exitCode : 0);
                    Exit(exitCode);
                }

            }
        }

        private static void SyncData()
        {
            var exe = Config.Get("python_exe");
            if (!File.Exists(exe))
            {
                throw new Exception($"python.exe:{exe},路径错误!");
            }

            var script = Config.Get("sync_script");
            if (!File.Exists(script))
            {
                throw new Exception($"同步脚本:{script},路径错误!");
            }

            var args = script;
            if (Config.GetBool("sync_force_update"))
            {
                args = $"{args} -f";
            }

            var info = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };
            var p = Process.Start(info);
            if (p == null)
            {
                throw new Exception($"启动同步脚本失败!");
            }

            if (!p.WaitForExit(1000))
            {
                MoveWindow(p.MainWindowHandle, 50, 50, 1200, 600, false);
            }
            p.WaitForExit();
        }

        public static void ExitKeyPress(object sender, ConsoleCancelEventArgs args)
        {
            // Allow our process to resume after this event
            args.Cancel = true;

            // Stop the algorithm
            algorithmManager.SetStatus(AlgorithmStatus.Stopped);
            Log.Trace("Program.ExitKeyPress(): Lean instance has been cancelled, shutting down safely now");
        }

        public static void Exit(int exitCode)
        {
            //TODO: This logic is accompanied with the introduction of "exit-time". Need to move it into a Config implementation.
            if (Config.TryGetValue("exit-time", out int exitTime) && exitTime > -1)
            {
                Config.Set("close-automatically", "true");
            }

            //Delete the message from the job queue:
            leanEngineSystemHandlers.JobQueue.AcknowledgeJob(job);
            Log.Trace("Engine.Main(): Packet removed from queue: " + job.AlgorithmId);

            // clean up resources
            leanEngineSystemHandlers.DisposeSafely();
            leanEngineAlgorithmHandlers.DisposeSafely();
            Log.LogHandler.DisposeSafely();
            //OS.CpuPerformanceCounter.DisposeSafely();

            Log.Trace($"Program.Main(): Exiting Lean with exit code {exitCode}...");
            Environment.Exit(exitCode > 0 ? -1 : exitCode);
        }

        public static void ExitProc()
        {
            try
            {
                // clean up resources
                if (leanEngineSystemHandlers != null)
                {
                    leanEngineSystemHandlers.Dispose();
                    leanEngineSystemHandlers = null;
                }

                if (leanEngineAlgorithmHandlers != null)
                {
                    leanEngineAlgorithmHandlers.Dispose();
                    leanEngineAlgorithmHandlers = null;
                }

                if (proc == null)
                    return;

                proc.WaitForExit(1);
                if (!proc.HasExited)
                {
                    proc.Kill();
                }

            }
            catch (System.InvalidOperationException)
            {

            }

        }

        public static void PauseAlgorithm(bool pause)
        {
            try
            {
                // clean up resources
                leanEngineSystemHandlers?.LeanManager.SetPauseAlgorithm(pause);
            }
            catch (System.InvalidOperationException)
            {

            }
        }
    }
}
