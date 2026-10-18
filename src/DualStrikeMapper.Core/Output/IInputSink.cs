using SharpHook.Data;
using DualStrikeMapper.Core.Mapping;

namespace DualStrikeMapper.Core.Output;

/// <summary>Where generated mouse and keyboard input goes.</summary>
public interface IInputSink : IDisposable
{
    void MoveRelative(int dx, int dy);
    /// <summary>Puts the cursor in the middle of the main screen (no-op where that's not possible, e.g. Wayland).</summary>
    void CenterCursor();
    void MouseDown(MouseButtonKind button);
    void MouseUp(MouseButtonKind button);
    void Scroll(ScrollDirection direction);
    void KeyDown(KeyCode key);
    void KeyUp(KeyCode key);
}
