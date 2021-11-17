using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using ParallelCmd.Output;

namespace ParallelCmd
{
    public static class Program
    {
        // ReSharper disable InconsistentNaming
        public enum OutputFormat
        {
            interlaced,
            boxed
        }
        // ReSharper restore InconsistentNaming


        public class Options
        {
            [Option('o', "output", HelpText = "Determines how command output is displayed. Default is 'Interlaced', which outputs each line as it is received prefixed by " +
                                              "the index of the command. Alternatively you can specify 'Boxed' to output each command in it's own separate box.")]
            public OutputFormat OutputFormat { get; set; }

            [Option('b', "boxsize", Default = null, HelpText = "The height of each command's output box when using the Boxed output. Defaults to an even distribution of the console's height.")]
            public int? BoxSize { get; set; }

            [Option('w', "workingdir", HelpText = "Specifies the default working directory to use for commands. If not specified, the current working directory will be used.")]
            public string? DefaultWorkingDirectory { get; set; }

            [Value(0, Required = true, HelpText = "One or more commands to run, including any arguments. Each command may begin with <[path to working directory]>" +
                                                  "to specify the working directory for that command. If not specified, the current working directory will be used.")]
            public IEnumerable<string> Commands { get; set; } = null!;
        }


        public static async Task<int> Main(string[] args)
        {
            try
            {
                await Parser.Default.ParseArguments<Options>(args)
                    .WithParsedAsync(RunCommands);

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
            finally
            {
                if (Debugger.IsAttached)
                {
                    Console.WriteLine("Press any Enter key to continue...");
                    Console.ReadLine();
                }
            }
        }


        private static async Task RunCommands(Options options)
        {
            var commandsArray = options.Commands.ToArray();

            using ICommandOutputFactory commandOutputFactory =
                options.OutputFormat switch
                {
                    OutputFormat.interlaced => new InterlacedCommandOutputFactory(),
                    OutputFormat.boxed => new BoxedCommandOutputFactory(options.BoxSize, commandsArray.Length),
                    _ => new InterlacedCommandOutputFactory()
                };


            var commandRunners = commandsArray
                .Select(ParseCommand)
                .Select((parsedCommand, index) => new CommandRunner(
                    commandOutputFactory.Create(parsedCommand.Command, parsedCommand.Arguments, index), 
                    parsedCommand.Command, 
                    parsedCommand.Arguments,
                    parsedCommand.WorkingDirectory ?? options.DefaultWorkingDirectory ?? Environment.CurrentDirectory))
                .ToArray();

            if (commandRunners.Length == 0)
                throw new ArgumentException("No commands to run");

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, args) =>
            {
                cancellationTokenSource.Cancel();
                args.Cancel = true;
            };

            var commandRunnerTasks = commandRunners.Select(c => c.Run(cancellationTokenSource.Token));
            await Task.WhenAll(commandRunnerTasks);
        }


        private static ParsedCommand ParseCommand(string command)
        {
            string? workingDirectory = null;
            string? arguments = null;
            int commandEnd;

            if (command.StartsWith('<'))
            {
                var workingDirEnd = command.IndexOf('>');
                if (workingDirEnd == -1)
                    throw new ArgumentException("Command parameters containing a working directory by starting with a < must end with a >");

                if (workingDirEnd == command.Length)
                    throw new ArgumentException("Command parameter must also include an actual command, not only a working directory");

                workingDirectory = command.Substring(1, workingDirEnd - 1);
                command = command[(workingDirEnd + 1)..];
            }

            // ReSharper disable once InvertIf
            if (command.StartsWith('"'))
            {
                commandEnd = command.IndexOf('"', 1);
                if (commandEnd == -1)
                    throw new ArgumentException("Command parameters starting with a quote must end with a quote");

                arguments = command[(commandEnd + 2)..];
                command = command[1..commandEnd];
            }
            else if ((commandEnd = command.IndexOf(' ')) >= 0)
            {
                arguments = command[(commandEnd + 1)..];
                command = command[..commandEnd];
            }

            return new ParsedCommand(command, arguments, workingDirectory);
        }


        private readonly struct ParsedCommand
        {
            public string Command { get; }
            public string? Arguments { get; }
            public string? WorkingDirectory { get; }


            public ParsedCommand(string command, string? arguments, string? workingDirectory)
            {
                Command = command;
                Arguments = arguments;
                WorkingDirectory = workingDirectory;
            }
        }
    }
}
