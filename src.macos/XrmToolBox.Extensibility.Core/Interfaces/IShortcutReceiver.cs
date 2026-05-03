namespace XrmToolBox.Extensibility.Interfaces;

public interface IShortcutReceiver
{
    void ReceiveKeyDownShortcut(string shortcut);
    void ReceiveKeyPressShortcut(string shortcut);
    void ReceiveKeyUpShortcut(string shortcut);
    void ReceivePreviewKeyDownShortcut(string shortcut);
}
