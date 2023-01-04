using System;
using System.Collections.Generic;
using System.Threading;

namespace ParallelCmd.Output
{
    public class BoxedCommandOutputFactory : ICommandOutputFactory
    {
        private readonly int? boxSize;
        private readonly int commandCount;
        private readonly ConsoleColor headerBackground;
        private readonly ConsoleColor headerForeground;
        private int boxWidth;
        private int boxHeight;
        private int top;
        private int bottom;

        private readonly object outputLock = new();

        private readonly Timer resizeTimer;
        private readonly List<BoxedCommandOutput> outputs = new();


        private const int ResizeCheckInterval = 500;


        public BoxedCommandOutputFactory(int? boxSize, int commandCount, ConsoleColor headerBackground, ConsoleColor headerForeground)
        {
            this.boxSize = boxSize;
            this.commandCount = commandCount;
            this.headerBackground = headerBackground;
            this.headerForeground = headerForeground;

            CalculateBoxSize(out boxWidth, out boxHeight);
            ResetConsole();

            resizeTimer = new Timer(CheckResized, null, ResizeCheckInterval, ResizeCheckInterval);
        }


        private void CalculateBoxSize(out int width, out int height)
        {
            width = Console.WindowWidth;
            height = Math.Max(boxSize ?? Console.WindowHeight / commandCount, 1);
        }


        private void ResetConsole()
        {
            Console.Clear();
            Console.CursorVisible = false;

            top = Console.CursorTop;
            bottom = (top + boxHeight * commandCount) + 1;
        }


        private void CheckResized(object? state)
        {
            CalculateBoxSize(out var newWidth, out var newHeight);
            if (newWidth == boxWidth && newHeight == boxHeight)
                return;

            lock (outputLock)
            {
                boxWidth = newWidth;
                boxHeight = newHeight;

                ResetConsole();

                for (var outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
                    outputs[outputIndex].Resize(top + outputIndex * boxHeight,  boxWidth, boxHeight);
            }
        }


        public ICommandOutput Create(string command, string? arguments, int commandIndex)
        {
            var output = new BoxedCommandOutput(outputLock, command, arguments, top + (commandIndex * boxHeight), boxWidth, boxHeight, headerBackground, headerForeground);
            outputs.Add(output);

            return output;
        }


        public void Dispose()
        {
            resizeTimer.Dispose();

            Console.CursorTop = bottom;
            Console.CursorVisible = true;

            GC.SuppressFinalize(this);
        }


        protected class BoxedCommandOutput : ICommandOutput
        {
            private readonly object lockObject;
            private readonly string command;
            private readonly string? arguments;
            private readonly ConsoleColor headerBackground;
            private readonly ConsoleColor headerForeground;
            private int firstLineTop;
            private int boxWidth;

            private string[] lines = Array.Empty<string>();
            private int nextLineIndex;
            private bool linesWrapped;


            public BoxedCommandOutput(object lockObject, string command, string? arguments, int boxTop, int boxWidth, int boxHeight, ConsoleColor headerBackground, ConsoleColor headerForeground)
            {
                this.lockObject = lockObject;
                this.command = command;
                this.arguments = arguments;
                this.headerBackground = headerBackground;
                this.headerForeground = headerForeground;

                Resize(boxTop, boxWidth, boxHeight);
            }


            public void Resize(int newTop, int newWidth, int newHeight)
            {
                boxWidth = newWidth;
                var hadLines = lines.Length > 0;

                if (newHeight > 1)
                {
                    Array.Resize(ref lines, newHeight - 1);
                    firstLineTop = newTop + 1;

                    var currentBackground = Console.BackgroundColor;
                    var currentForeground = Console.ForegroundColor;
                    try
                    {
                        Console.BackgroundColor = headerBackground;
                        Console.ForegroundColor = headerForeground;

                        WriteFullWidthLine(newTop, command + " " + arguments);
                    }
                    finally
                    {
                        Console.BackgroundColor = currentBackground;
                        Console.ForegroundColor = currentForeground;
                    }
                }
                else
                {
                    Array.Resize(ref lines, newHeight);
                    firstLineTop = newTop;
                }


                if (!hadLines)
                    return;

                var top = firstLineTop;
                foreach (var line in lines)
                {
                    WriteFullWidthLine(top, line);
                    top++;
                }
            }


            public void WriteLine(string line)
            {
                lock (lockObject)
                {
                    lines[nextLineIndex] = line;
                    nextLineIndex++;

                    if (nextLineIndex >= lines.Length)
                    {
                        nextLineIndex = 0;
                        linesWrapped = true;
                    }

                    var top = firstLineTop;

                    if (linesWrapped)
                    {
                        // lines is a ring buffer, so nextLineIndex is effectively the tail here
                        for (var i = nextLineIndex; i < lines.Length; i++)
                        {
                            WriteFullWidthLine(top, lines[i]);
                            top++;
                        }
                    }

                    for (var i = 0; i < nextLineIndex; i++)
                    {
                        WriteFullWidthLine(top, lines[i]);
                        top++;
                    }
                }
            }


            private void WriteFullWidthLine(int top, string? line)
            {
                Console.CursorTop = top;
                Console.CursorLeft = 0;
                line ??= "";

                // No wrapping support yet
                if (line.Length > boxWidth)
                    line = line[..(boxWidth - 3)] + "...";
                else if (line.Length < boxWidth)
                    line += new string(' ', boxWidth - line.Length);
                
                // No WriteLine, the caller must set the CursorTop, so there is no empty line at the bottom if
                // the commands boxes exactly fit in the console window
                Console.Write(line);
            }
        }
    }
}
