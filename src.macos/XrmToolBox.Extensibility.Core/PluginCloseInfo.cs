namespace XrmToolBox.Extensibility;

public enum ToolBoxCloseReason
{
    None,
    CloseAll,
    CloseAllExceptActive,
    CloseCurrent,
    CloseHotKey,
    PluginRequest,
    HostShutdown,
}

public sealed class PluginCloseInfo
{
    public PluginCloseInfo() { }

    public PluginCloseInfo(ToolBoxCloseReason reason)
    {
        if (reason == ToolBoxCloseReason.None)
        {
            throw new ArgumentException("None is not a valid ToolBoxCloseReason", nameof(reason));
        }
        ToolBoxReason = reason;
    }

    public bool Silent { get; set; }
    public bool Cancel { get; set; }
    public ToolBoxCloseReason ToolBoxReason { get; set; } = ToolBoxCloseReason.None;
}
