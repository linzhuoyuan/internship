using System;
using Serilog;
using Serilog.Events;

namespace QuantConnect.Logging
{
    public class SerilogFileLogHandler : ILogHandler
    {
        /// <summary>
        /// GetFileLogger
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="minLevel"></param>
        /// <param name="rollingInterval"></param>
        /// <param name="flushToDiskInterval">单位(秒)</param>
        /// <param name="fileSizeLimitBytes">日志文件的大小限制</param>
        /// <param name="retainedFileCountLimit">日志文件的个数</param>
        /// <returns></returns>
        public static ILogger GetFileLogger(
            string filename,
            LogEventLevel minLevel = LogEventLevel.Verbose,
            RollingInterval rollingInterval = RollingInterval.Infinite,
            int flushToDiskInterval = 5,
            long? fileSizeLimitBytes = null,
            int? retainedFileCountLimit = null)
        {
            return new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo
                .Async(a => a.File(
                    filename,
                    minLevel,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    rollOnFileSizeLimit: fileSizeLimitBytes.HasValue,
                    retainedFileCountLimit: retainedFileCountLimit ?? 31,
                    rollingInterval: rollingInterval,
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(flushToDiskInterval)))
                .CreateLogger();
        }

        private readonly ILogger _logger;

        public SerilogFileLogHandler(string logFile)
        {
            _logger = GetFileLogger(logFile, flushToDiskInterval: 1);
        }

        public void Dispose()
        {
        }

        public void Error(string text)
        {
            _logger.Error(text);
        }

        public void Debug(string text)
        {
            _logger.Debug(text);
        }

        public void Trace(string text)
        {
            _logger.Verbose(text);
        }
    }
}