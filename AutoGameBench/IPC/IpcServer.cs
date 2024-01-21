using System;
using CoreHook.IPC.Handlers;
using CoreHook.IPC.Messages;
using CoreHook.IPC.NamedPipes;
using CoreHook.IPC.Transport;

namespace AutoGameBench.IPC;

public sealed class IpcServer : IDisposable
{
    #region Fields

    private readonly INamedPipe _pipeServer;

    #endregion

    #region Events

    public event EventHandler<FrameTimeEventArgs> FrameTimeReceived;

    #endregion

    #region Constructor

    public IpcServer()
    {
        _pipeServer = NamedPipeServer.StartNewServer("AutoGameBench", new PipePlatformBase(), HandleMessage);
    }

    #endregion

    #region Public Methods

    public void SendMessage(string body)
    {
        SendMessage(_pipeServer.MessageHandler, "Message", body);
    }

    public void SendMessage(string header, string body)
    {
        SendMessage(_pipeServer.MessageHandler, header, body);
    }

    public void Dispose()
    {
        _pipeServer.Dispose();
    }

    #endregion

    #region Private Methods

    private void HandleMessage(IMessage message, ITransportChannel transport)
    {
        string response = "ok";

        if (String.Equals(message.Header, "Log", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{message.Header} - {message.Body}");
        }

        if (String.Equals(message.Header, "Dir", StringComparison.OrdinalIgnoreCase))
        {
            response = Environment.CurrentDirectory;
        }

        if (String.Equals(message.Header, "FT", StringComparison.OrdinalIgnoreCase))
        {
            if (Double.TryParse(message.Body, out double frameTime))
            {
                FrameTimeEventArgs args = new()
                {
                    FrameTime = frameTime
                };
                FrameTimeReceived?.Invoke(this, args);
            }
        }

        SendMessage(transport.MessageHandler, "Response", response);
    }

    private void SendMessage(IMessageHandler handler, string header, string body)
    {
        handler.TryWrite(new Message(header, body));
    }

    #endregion
}
