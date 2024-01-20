using System;
using CoreHook.IPC.Messages;
using CoreHook.IPC.NamedPipes;

namespace Hook;

public sealed class Logger : IDisposable
{
    private readonly INamedPipe _pipe;

    public Logger()
    {
        _pipe = new NamedPipeClient("AutoGameBench");
        _pipe.Connect();
    }

    public bool Log(string message)
    {
        bool success = false;

        if (_pipe.Connection.IsConnected)
        {
            if (_pipe.MessageHandler.TryWrite(new Message("Log", message)))
            {
                success = String.Equals(_pipe.MessageHandler.Read().Body, "ok", StringComparison.OrdinalIgnoreCase);
            }
        }

        return success;
    }

    public void Dispose()
    {
        if (_pipe != null)
        {
            _pipe.Dispose();
        }
    }
}
