using System;

namespace ParallelCmd.Output
{
    public interface ICommandOutput
    {
        void WriteLine(string line);
    }


    public interface ICommandOutputFactory : IDisposable
    {
        ICommandOutput Create(string command, string? arguments, int commandIndex);
    }
}
