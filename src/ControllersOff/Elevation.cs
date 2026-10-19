using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace ControllersOff;

public static class Elevation
{
    private static readonly WindowsPrincipal CurrentUser = new(WindowsIdentity.GetCurrent());

    public static bool IsAdministrator => CurrentUser.IsInRole(WindowsBuiltInRole.Administrator);

    public static bool RunElevated(params string[] arguments)
    {
        try
        {
            // ArgumentList is ignored when UseShellExecute is on, so the command line is built by hand.
            var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unknown."))
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(' ', arguments.Select(Quote))
            };

            Trace.Write($"runas exe='{startInfo.FileName}' args='{startInfo.Arguments}'");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                Trace.Write("runas process is null");
                return false;
            }

            process.WaitForExit();
            Trace.Write($"runas exit={process.ExitCode}");

            return process.ExitCode == 0;
        }
        catch (Win32Exception exception)
        {
            Trace.Write($"runas failed win32={exception.NativeErrorCode} {exception.Message}");
            return false;
        }
    }

    private static string Quote(string argument) => argument.Contains(' ') ? $"\"{argument}\"" : argument;
}