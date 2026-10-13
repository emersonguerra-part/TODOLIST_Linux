using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TodoListApp.Services
{
    public static class SoundEffectHelper
    {
        private static string? _taskWavPath;
        private static string? _subtaskWavPath;

        public static void Initialize()
        {
            _taskWavPath = ResolveAudioPath("Task_completed.wav");
            _subtaskWavPath = ResolveAudioPath("Subtask_completed.wav");
        }

        private static string? ResolveAudioPath(string filename)
        {
            string p1 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            if (File.Exists(p1)) return p1;

            string p2 = Path.Combine(Directory.GetCurrentDirectory(), filename);
            if (File.Exists(p2)) return p2;

            string p3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", filename);
            if (File.Exists(p3)) return Path.GetFullPath(p3);

            return null;
        }

        public static void PlayTaskCompleted()
        {
            PlayFile(_taskWavPath);
        }

        public static void PlaySubtaskChime()
        {
            PlayFile(_subtaskWavPath);
        }

        private static void PlayFile(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            Task.Run(() =>
            {
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = $"-NoProfile -NonInteractive -Command \"(New-Object Media.SoundPlayer '{path.Replace("'", "''")}').PlaySync()\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        proc?.WaitForExit(3000);
                        return;
                    }

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        string[] players = { "pw-play", "paplay", "aplay" };
                        foreach (var cmd in players)
                        {
                            try
                            {
                                var psi = new ProcessStartInfo
                                {
                                    FileName = cmd,
                                    Arguments = $"\"{path}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using var proc = Process.Start(psi);
                                if (proc != null)
                                {
                                    proc.WaitForExit(3000);
                                    if (proc.ExitCode == 0)
                                        return;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                catch { }
            });
        }
    }
}
