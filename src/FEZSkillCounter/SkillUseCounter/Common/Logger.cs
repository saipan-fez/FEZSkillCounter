﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO;
using System.Reflection;
using System.Text;

namespace SkillUseCounter
{
    public static class Logger
    {
        private static StreamWriter _logWriter      = null;
        private static StreamWriter _errorLogWriter = null;

        public static void WriteException(Exception ex)
        {
            if (ex is FileNotFoundException && ex.Message.IndexOf("ControlzEx.XmlSerializers") != -1)
            {
                // 起動時に毎回スローされるが、特に害はない例外のため記録しない
                return;
            }

#if DEBUG
            if (_errorLogWriter == null)
            {
                _errorLogWriter = CreateErrorLogWriter();
            }
#endif

            var msg = GenerateLogMessage(ex.ToString());

#if DEBUG
            _errorLogWriter.WriteLine(msg);
            _errorLogWriter.Flush();
#endif
            Debug.WriteLine(msg);
        }

        public static void WriteLine(string message, [CallerMemberName] string memberName = "")
        {
#if DEBUG
            if (_logWriter == null)
            {
                _logWriter = CreateLogWriter();
            }
#endif

            var msg = GenerateLogMessage($"{message} ({memberName})");

#if DEBUG
            _logWriter.WriteLine(msg);
            _logWriter.Flush();
#endif
            Debug.WriteLine(msg);
        }

        private static string GenerateLogMessage(string message)
        {
            var d = DateTime.Now;
            return $"[{d.Hour.ToString("D2")}:{d.Minute.ToString("D2")}:{d.Second.ToString("D2")}.{d.Millisecond.ToString("D3")}]{message}";
        }

        private static StreamWriter CreateLogWriter()
        {
            var assembly     = Assembly.GetEntryAssembly();
            var exeDirectory = Path.GetDirectoryName(assembly.Location);
            var logDirectory = Path.Combine(exeDirectory, "log");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logPath = Path.Combine(logDirectory, $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log");

            return new StreamWriter(logPath, false, Encoding.UTF8);
        }

        private static StreamWriter CreateErrorLogWriter()
        {
            var assembly     = Assembly.GetEntryAssembly();
            var exeDirectory = Path.GetDirectoryName(assembly.Location);
            var logDirectory = Path.Combine(exeDirectory, "error");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            var logPath = Path.Combine(logDirectory, $"{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log");

            return new StreamWriter(logPath, false, Encoding.UTF8);
        }
    }
}
