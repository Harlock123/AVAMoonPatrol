using Avalonia.Input;

namespace AVAMoonPatrol.Engine;

/// <summary>Keyboard, mouse and gamepad boiled down to the controls: faster or slower, jump, and fire.</summary>
public sealed class InputHandler : IDisposable
{
    private readonly HashSet<Key> _pressed = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager _pad = new(0);
    private bool _mouseFire, _mouseJump;

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _pressed.Remove(was);
            _pressedAs[physical] = key;
        }
        _pressed.Add(key);
    }
    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _pressed.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _pressed.Remove(was);
    }

    public bool Has(Key key) => _pressed.Contains(key);

    /// <summary>The left button fires, the right button jumps.</summary>
    public void OnMouse(bool left, bool right)
    {
        _mouseFire = left;
        _mouseJump = right;
    }

    public void Update() => _pad.Update();

    public Controls Controls
    {
        get
        {
            int dx = (Has(Key.Right) || Has(Key.D) || _pad.DPadRight || _pad.LeftStickX > 0.35f ? 1 : 0)
                   - (Has(Key.Left) || Has(Key.A) || _pad.DPadLeft || _pad.LeftStickX < -0.35f ? 1 : 0);
            bool jump = Has(Key.Up) || Has(Key.W) || Has(Key.LeftShift) || Has(Key.RightShift) || Has(Key.X) || _mouseJump
                        || _pad.ButtonA || _pad.DPadUp || _pad.LeftStickY < -0.5f;
            bool fire = Has(Key.Space) || Has(Key.LeftCtrl) || Has(Key.RightCtrl) || Has(Key.Z) || _mouseFire
                        || _pad.ButtonB || _pad.ButtonX || _pad.RightTrigger > 0.3f;
            return new Controls(dx, jump, fire);
        }
    }

    public bool Start => Has(Key.Enter) || Has(Key.D1) || _pad.ButtonStart;

    public bool PadUp => _pad.DPadUp || _pad.LeftStickY < -0.5f;
    public bool PadDown => _pad.DPadDown || _pad.LeftStickY > 0.5f;
    public bool PadLeft => _pad.DPadLeft || _pad.LeftStickX < -0.5f;
    public bool PadRight => _pad.DPadRight || _pad.LeftStickX > 0.5f;
    public bool PadAccept => _pad.ButtonA || _pad.ButtonX || _pad.ButtonB;
    public bool PadDone => _pad.ButtonStart;
    public bool PadPause => _pad.ButtonStart;
    public bool PadBack => _pad.ButtonBack;

    public void Dispose() => _pad.Dispose();
}
