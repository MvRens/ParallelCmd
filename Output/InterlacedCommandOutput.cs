using System;

namespace ParallelCmd.Output
{
    public class InterlacedCommandOutputFactory : ICommandOutputFactory
    {
        private readonly object outputLock = new();


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }


        public ICommandOutput Create(string command, string? arguments, int commandIndex)
        {
            return new InterlacedCommandOutput(outputLock, command, arguments, commandIndex);
        }


        protected class InterlacedCommandOutput : ICommandOutput
        {
            private readonly object lockObject;
            private readonly int commandIndex;

            public InterlacedCommandOutput(object lockObject, string command, string? arguments, int commandIndex)
            {
                this.lockObject = lockObject;
                this.commandIndex = commandIndex;
                WriteLine(command + (arguments != null ? " " + arguments : ""));
            }


            public void WriteLine(string line)
            {
                lock (lockObject)
                {
                    Console.WriteLine($"[{commandIndex}] {line}");
                }
            }
        }
    }
}
