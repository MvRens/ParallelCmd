using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ParallelCmd.Output;

namespace ParallelCmd
{
    public class CommandRunner
    {
        private readonly ICommandOutput commandOutput;
        private readonly string command;
        private readonly string? arguments;
        private readonly string? workingDirectory;


        public CommandRunner(ICommandOutput commandOutput, string command, string? arguments, string? workingDirectory)
        {
            this.commandOutput = commandOutput;
            this.command = command;
            this.arguments = arguments;
            this.workingDirectory = workingDirectory;
        }


        public Task Run(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory ?? ""
                };

                if (arguments != null)
                    processStartInfo.Arguments = arguments;

                var process = new Process
                {
                    StartInfo = processStartInfo
                };

                process.OutputDataReceived += ProcessDataReceived;
                process.ErrorDataReceived += ProcessDataReceived;

                process.Start();
                try
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync(cancellationToken);

                    commandOutput.WriteLine($"Process exited with code {process.ExitCode}");
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                    {
                        // Despite many examples (like https://stackoverflow.com/questions/813086/can-i-send-a-ctrl-c-sigint-to-an-application-on-windows)
                        // I could not get it working properly with multiple runners, so for now we'll just kill it ¯\_(ツ)_/¯
                        process.Kill(true);
                        commandOutput.WriteLine("Process killed");
                    }
                }
            }, CancellationToken.None);
        }


        private void ProcessDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (args.Data == null)
                return;

            commandOutput.WriteLine(args.Data);
        }
    }
}
