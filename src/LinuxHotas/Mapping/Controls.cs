namespace LinuxHotas.Mapping
{
    /// <summary>
    /// What an axis can drive. One-sided outputs read 0..1 (a throttle lever, a pedal, a trigger); centred ones read -1..1
    /// (a stick). Written with ananace's GameAxis as the reference.
    /// </summary>
    public enum GameAxis
    {
        /// <summary>One-sided forward thrust; Reverse Throttle turns it into backward thrust.</summary>
        Throttle,
        Backward,
        ForwardBackward,
        /// <summary>Strafe, and steering on wheeled grids.</summary>
        Left,
        Right,
        LeftRight,
        Up,
        Down,
        UpDown,
        Pitch,
        Yaw,
        Roll,
        CameraPitch,
        CameraYaw,
        /// <summary>One-sided wheel brake force.</summary>
        WheelBrake,
    }

    /// <summary>What a button or hat direction can do. Written with ananace's GameAction as the reference.</summary>
    public enum GameAction
    {
        /// <summary>Switches the joysticks' hold on the current grid on and off.</summary>
        ToggleActive,
        /// <summary>A tap flips the Throttle's direction; held for longer than half a second, it flips back on release.</summary>
        ReverseThrottle,
        ReverseThrottleToggle,
        ReverseThrottleHold,

        FirePrimary,
        FireSecondary,
        LockTarget,
        ReleaseTarget,
        WheelJump,
        Brake,

        Lights,
        Dampeners,
        Handbrake,
        Power,
        Parking,

        NextToolbarSlot,
        PreviousToolbarSlot,
        ToolbarSlot1,
        ToolbarSlot2,
        ToolbarSlot3,
        ToolbarSlot4,
        ToolbarSlot5,
        ToolbarSlot6,
        ToolbarSlot7,
        ToolbarSlot8,
        ToolbarSlot9,
        EmptyHands,

        NextToolbar,
        PreviousToolbar,
        Toolbar1,
        Toolbar2,
        Toolbar3,
        Toolbar4,
        Toolbar5,
        Toolbar6,
        Toolbar7,
        Toolbar8,
        Toolbar9,
    }

    public static class Controls
    {
        /// <summary>A stick axis: deadzone in the middle, -1..1. Otherwise a lever: deadzone at both ends, 0..1.</summary>
        public static bool IsCentred(GameAxis axis)
        {
            switch (axis)
            {
                case GameAxis.ForwardBackward:
                case GameAxis.LeftRight:
                case GameAxis.UpDown:
                case GameAxis.Pitch:
                case GameAxis.Yaw:
                case GameAxis.Roll:
                case GameAxis.CameraPitch:
                case GameAxis.CameraYaw:
                    return true;
                default:
                    return false;
            }
        }
    }
}
