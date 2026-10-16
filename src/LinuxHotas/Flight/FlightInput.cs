using System;
using System.Collections.Generic;
using LinuxHotas.Devices;
using LinuxHotas.Mapping;
using VRageMath;

namespace LinuxHotas.Flight
{
    /// <summary>
    /// The binds of every connected device, read once a frame into the game's units: the move, rotation and roll that
    /// MyShipController.MoveAndRotate takes, and which actions were pressed or released. Written with ananace's
    /// InputAggregate as the reference.
    /// </summary>
    public sealed class FlightInput
    {
        /// <summary>MoveAndRotate divides rotation by 20 and multiplies roll by 0.2 before the gyros (full torque at 1),
        /// so full deflection is full torque on every axis.</summary>
        public const float RotationScale = 20f, RollScale = 5f, CameraScale = 20f;
        private static readonly TimeSpan ReverseHoldTime = TimeSpan.FromMilliseconds(500);

        /// <summary>X right, Y up, Z backward, -1..1: the ship's strafe and thrust (and the wheels' steering and drive).</summary>
        public Vector3 Move;
        /// <summary>X pitch (positive noses down), Y yaw (positive turns right), in MoveAndRotate's units.</summary>
        public Vector2 Rotation;
        /// <summary>Positive rolls right, in MoveAndRotate's units.</summary>
        public float Roll;
        public Vector2 Camera;
        public float WheelBrake;

        /// <summary>Whether the joysticks fly the grid at all; Toggle Active flips it.</summary>
        public bool Active = true;
        /// <summary>-1: Throttle pushes forward; 1: backward.</summary>
        public float ThrottleDirection { get; private set; } = -1;

        private HashSet<GameAction> held = new HashSet<GameAction>(), wasHeld = new HashSet<GameAction>();
        private DateTime reversedAt;

        public event Action<GameAction> Pressed, Released;

        public bool IsHeld(GameAction action) => held.Contains(action);

        public void Update(IReadOnlyList<JoystickDevice> devices, Config config)
        {
            Move = Vector3.Zero;
            Rotation = Vector2.Zero;
            Roll = 0;
            Camera = Vector2.Zero;
            WheelBrake = 0;
            (held, wasHeld) = (wasHeld, held);
            held.Clear();

            foreach (var device in devices)
            {
                var binds = config.For(device.Key, false);
                if (binds == null || !device.Connected)
                    continue;
                foreach (var bind in binds.Binds)
                {
                    if (!bind.IsValid)
                        continue;
                    float value = bind.Read(device);
                    if (bind.OutputAxis is GameAxis axis)
                        Apply(axis, value);
                    else if (bind.OutputAction is GameAction action && value >= 0.75f)
                        held.Add(action);
                }
            }

            foreach (var action in wasHeld)
                if (!held.Contains(action))
                    Ended(action);
            foreach (var action in held)
                if (!wasHeld.Contains(action))
                    Began(action);
        }

        /// <summary>Forgets what is held, so nothing fires again on the next frame (a new session, say).</summary>
        public void Reset()
        {
            held.Clear();
            wasHeld.Clear();
            Move = Vector3.Zero;
            Rotation = Vector2.Zero;
            Roll = 0;
            Camera = Vector2.Zero;
            WheelBrake = 0;
        }

        private void Apply(GameAxis axis, float v)
        {
            switch (axis)
            {
                case GameAxis.Throttle: Max(ref Move.Z, v * ThrottleDirection); break;
                case GameAxis.Backward: Max(ref Move.Z, v); break;
                case GameAxis.ForwardBackward: Max(ref Move.Z, v); break;
                case GameAxis.Left: Max(ref Move.X, -v); break;
                case GameAxis.Right: Max(ref Move.X, v); break;
                case GameAxis.LeftRight: Max(ref Move.X, v); break;
                case GameAxis.Up: Max(ref Move.Y, v); break;
                case GameAxis.Down: Max(ref Move.Y, -v); break;
                case GameAxis.UpDown: Max(ref Move.Y, v); break;
                // A stick pushed forward reads -1 and should put the nose down, which MoveAndRotate calls positive.
                case GameAxis.Pitch: Max(ref Rotation.X, -v * RotationScale); break;
                case GameAxis.Yaw: Max(ref Rotation.Y, v * RotationScale); break;
                case GameAxis.Roll: Max(ref Roll, v * RollScale); break;
                case GameAxis.CameraPitch: Max(ref Camera.X, -v * CameraScale); break;
                case GameAxis.CameraYaw: Max(ref Camera.Y, v * CameraScale); break;
                case GameAxis.WheelBrake: Max(ref WheelBrake, v); break;
            }
        }

        /// <summary>Several binds on one output: the one pushed furthest wins.</summary>
        private static void Max(ref float into, float value)
        {
            if (Math.Abs(value) > Math.Abs(into))
                into = value;
        }

        private void Began(GameAction action)
        {
            switch (action)
            {
                case GameAction.ToggleActive:
                    Active = !Active;
                    break;
                case GameAction.ReverseThrottleToggle:
                    ThrottleDirection = -ThrottleDirection;
                    break;
                case GameAction.ReverseThrottle:
                case GameAction.ReverseThrottleHold:
                    ThrottleDirection = -ThrottleDirection;
                    reversedAt = DateTime.UtcNow;
                    break;
            }
            Pressed?.Invoke(action);
        }

        private void Ended(GameAction action)
        {
            if (action == GameAction.ReverseThrottleHold || (action == GameAction.ReverseThrottle && DateTime.UtcNow - reversedAt > ReverseHoldTime))
                ThrottleDirection = -ThrottleDirection;
            Released?.Invoke(action);
        }
    }
}
