using System;

namespace ParallelCmd.Output
{
    public class BoxedCommandOutputFactory : ICommandOutputFactory
    {
        private readonly int boxWidth;
        private readonly int boxHeight;
        private readonly int top;
        private readonly int bottom;

        private readonly object outputLock = new();


        public BoxedCommandOutputFactory(int? boxSize, int commandCount)
        {
            boxWidth = Console.WindowWidth;
            boxHeight = Math.Max(boxSize ?? Console.WindowHeight / commandCount, 1);
            top = Console.CursorTop;
            bottom = (top + boxHeight * commandCount) + 1;

            Console.CursorVisible = false;

            // Make sure we scroll all the way
            for (var i = 0; i < Console.WindowHeight - 1; i++)
                Console.WriteLine();
        }


        public ICommandOutput Create(string command, string? arguments, int commandIndex)
        {
            return new BoxedCommandOutput(outputLock, command, arguments, top + (commandIndex * boxHeight), boxWidth, boxHeight);
        }


        public void Dispose()
        {
            Console.CursorTop = bottom;
            Console.CursorVisible = true;

            GC.SuppressFinalize(this);
        }


        protected class BoxedCommandOutput : ICommandOutput
        {
            private readonly object lockObject;
            private readonly int boxTop;
            private readonly int boxWidth;

            private readonly string[] lines; 
            private int nextLineIndex;
            private bool linesWrapped = false;


            public BoxedCommandOutput(object lockObject, string command, string? arguments, int boxTop, int boxWidth, int boxHeight)
            {
                this.lockObject = lockObject;
                this.boxTop = boxTop;
                this.boxWidth = boxWidth;
                if (boxHeight > 1)
                {
                    lines = new string[boxHeight - 1];

                    Console.CursorTop = boxTop;

                    var currentBackground = Console.BackgroundColor;
                    var currentForeground = Console.ForegroundColor;
                    try
                    {
                        Console.BackgroundColor = ConsoleColor.DarkBlue;
                        Console.ForegroundColor = ConsoleColor.Gray;

                        WriteFullWidthLine(command + " " + arguments);
                    }
                    finally
                    {
                        Console.BackgroundColor = currentBackground;
                        Console.ForegroundColor = currentForeground;
                    }
                }
                else
                    lines = new string[boxHeight];
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

                    var top = boxTop + 1;


                    if (linesWrapped)
                    {
                        // lines is a ring buffer, so nextLineIndex is effectively the tail here
                        for (var i = nextLineIndex; i < lines.Length; i++)
                        {
                            Console.CursorTop = top;
                            WriteFullWidthLine(lines[i]);
                            top++;
                        }
                    }

                    for (var i = 0; i < nextLineIndex; i++)
                    {
                        Console.CursorTop = top;
                        WriteFullWidthLine(lines[i]);
                        top++;
                    }
                }
            }


            private void WriteFullWidthLine(string? line)
            {
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
