namespace XrmToolBox.Extensibility.Interfaces;

public sealed class MessageBusEventArgs : EventArgs
{
    public string TargetArgument { get; init; } = string.Empty;
    public string SourcePlugin { get; init; } = string.Empty;
    public string TargetPlugin { get; init; } = string.Empty;
    public bool NewInstance { get; init; }
    public object? TargetParameter { get; init; }

    public MessageBusEventArgs(string targetPlugin) => TargetPlugin = targetPlugin;
}

public interface IMessageBusHost
{
    event EventHandler<MessageBusEventArgs>? OnOutgoingMessage;
    void OnIncomingMessage(MessageBusEventArgs message);
}
