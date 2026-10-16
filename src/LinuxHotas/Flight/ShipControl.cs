using System;
using HarmonyLib;
using LinuxHotas.Mapping;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace LinuxHotas.Flight
{
    /// <summary>
    /// Puts <see cref="FlightInput"/> into the seat the local player is flying. Axes are added to whatever the game, and
    /// SEVR's hands, ask of the ship in the same frame (<see cref="MoveAndRotatePatch"/>), so none of them overrides another.
    /// Buttons work the seat's own switches and toolbar. Game thread only. Written with ananace's session component as
    /// the reference.
    /// </summary>
    internal static class ShipControl
    {
        public static FlightInput Input { get; } = new FlightInput();
        public static MyShipController Seat { get; private set; }

        public static void Init()
        {
            Input.Pressed += OnPressed;
            Input.Released += OnReleased;
        }

        /// <summary>Once a frame, after <see cref="FlightInput.Update"/>.</summary>
        public static void Update(Config config)
        {
            var seat = MySession.Static?.ControlledEntity as MyShipController;
            if (seat != Seat)
            {
                SetBrakes(false);
                SetJump(false);
                if (seat != null && Seat == null)
                    Input.Active = config.ActiveByDefault;
                Seat = seat;
            }

            brakeFrame++;
            bool wheels = CanControl(Seat) && Seat.ControlWheels && Seat.GridWheels != null;
            SetBrakes(wheels && BrakeWanted());
            SetJump(wheels && Input.IsHeld(GameAction.WheelJump));
            if (!CanControl(Seat))
                return;

            if (Input.Camera != Vector2.Zero)
                MyAPIGateway.Session?.CameraController?.Rotate(Input.Camera, 0f);
        }

        /// <summary>A brake lever below this is off; above <c>1 - BrakeDead</c> it is fully on.</summary>
        private const float BrakeDead = 0.05f;
        /// <summary>Frames in one on/off cycle of a part-pulled brake.</summary>
        private const int BrakePeriod = 10;

        /// <summary>The joysticks hold the seat's wheel brakes; the game's own brake call is ORed with this
        /// (<see cref="TryEnableBrakesPatch"/>), so its every-frame check against the space key does not undo it.</summary>
        internal static bool Braking { get; private set; }
        private static int brakeFrame;

        /// <summary>Wheel brakes are on or off in the game, so a part-pulled lever pulses them: on for that fraction of
        /// every <see cref="BrakePeriod"/> frames, as ananace's plugin does where its analog brakes are unavailable.</summary>
        private static bool BrakeWanted()
        {
            float pull = Input.IsHeld(GameAction.Brake) ? 1f : Input.WheelBrake;
            if (pull < BrakeDead)
                return false;
            if (pull > 1f - BrakeDead)
                return true;
            return brakeFrame % BrakePeriod < pull * BrakePeriod;
        }

        private static void SetBrakes(bool on)
        {
            if (on == Braking)
                return;
            Braking = on;
            if (Seat != null && !Seat.Closed)
                Seat.TryEnableBrakes(on);
        }

        /// <summary>The joysticks hold the wheel jump; the game's own every-frame call with its jump key is ORed with
        /// this (<see cref="WheelJumpPatch"/>), so neither releases the other's jump mid-charge.</summary>
        internal static bool Jumping { get; private set; }

        private static void SetJump(bool on)
        {
            if (on == Jumping)
                return;
            Jumping = on;
            if (Seat != null && !Seat.Closed)
                Seat.WheelJump(on);
        }

        /// <summary>The local player flies <paramref name="seat"/>, the joysticks are switched on, and no gameplay
        /// screen (terminal, cockpit text editor) is taking the input.</summary>
        public static bool CanControl(MyShipController seat)
        {
            if (seat == null || seat != Seat || !Input.Active || seat.Closed)
                return false;
            if (MyGuiScreenGamePlay.ActiveGameplayScreen != null || MyGuiScreenGamePlay.DisableInput)
                return false;
            return seat.ControllerInfo.IsLocallyHumanControlled();
        }

        /// <summary>Called as the game hands the seat this frame's keyboard, mouse and pad request (SEVR's hands
        /// included): the joysticks' request is added to it.</summary>
        internal static void AddTo(MyShipController seat, ref Vector3 move, ref Vector2 rotation, ref float roll)
        {
            if (!CanControl(seat))
                return;
            move += Input.Move;
            rotation += Input.Rotation;
            roll += Input.Roll;
        }

        private static void OnPressed(GameAction action)
        {
            var seat = Seat;
            if (!CanControl(seat))
                return;

            var toolbar = seat.Toolbar;
            switch (action)
            {
                case GameAction.Lights: seat.SwitchLights(); break;
                case GameAction.Dampeners: seat.SwitchDamping(); break;
                case GameAction.Handbrake: seat.SwitchHandbrake(); break;
                case GameAction.Power: seat.SwitchReactorsLocal(); break;
                case GameAction.Parking: seat.SwitchLandingGears(); break;

                case GameAction.FirePrimary: seat.BeginShoot(MyShootActionEnum.PrimaryAction); break;
                case GameAction.FireSecondary: seat.BeginShoot(MyShootActionEnum.SecondaryAction); break;

                case GameAction.LockTarget:
                    if (seat.IsTargetLockingEnabled())
                        MySession.Static.LocalCharacter?.TargetFocusComp?.OnLockRequest();
                    break;
                case GameAction.ReleaseTarget:
                    if (seat.IsTargetLockingEnabled())
                        MySession.Static.LocalCharacter?.TargetLockingComp?.ReleaseTargetLockRequest();
                    break;

                case GameAction.NextToolbarSlot: toolbar?.SelectNextSlot(); break;
                case GameAction.PreviousToolbarSlot: toolbar?.SelectPreviousSlot(); break;
                case GameAction.EmptyHands: toolbar?.ActivateItemAtSlot(toolbar.SlotCount); break;
                case GameAction.NextToolbar: toolbar?.PageUp(); break;
                case GameAction.PreviousToolbar: toolbar?.PageDown(); break;

                default:
                    if (action >= GameAction.ToolbarSlot1 && action <= GameAction.ToolbarSlot9)
                        toolbar?.ActivateItemAtSlot(action - GameAction.ToolbarSlot1);
                    else if (action >= GameAction.Toolbar1 && action <= GameAction.Toolbar9)
                        toolbar?.SwitchToPage(action - GameAction.Toolbar1);
                    break;
            }
        }

        private static void OnReleased(GameAction action)
        {
            var seat = Seat;
            if (seat == null || seat.Closed)
                return;

            // Released even when control has lapsed meanwhile, so a gun never keeps firing.
            switch (action)
            {
                case GameAction.FirePrimary: seat.EndShoot(MyShootActionEnum.PrimaryAction); break;
                case GameAction.FireSecondary: seat.EndShoot(MyShootActionEnum.SecondaryAction); break;
            }
        }
    }

    /// <summary>
    /// The game calls this every frame its gameplay screen handles input, with fresh values, and the ship acts on the
    /// stored result later (MoveAndRotate()). Adding here, rather than to the stored values as ananace's plugin does,
    /// cannot pile up over frames in which the game does not call it (a menu has the focus).
    /// </summary>
    [HarmonyPatch(typeof(MyShipController), nameof(MyShipController.MoveAndRotate), new[] { typeof(Vector3), typeof(Vector2), typeof(float) })]
    internal static class MoveAndRotatePatch
    {
        private static void Prefix(MyShipController __instance, ref Vector3 moveIndicator, ref Vector2 rotationIndicator, ref float rollIndicator) =>
            ShipControl.AddTo(__instance, ref moveIndicator, ref rotationIndicator, ref rollIndicator);
    }
}

namespace LinuxHotas.Flight
{
    /// <summary>The game sets the wheel brakes each frame from the space key alone; the joysticks' brake is added to it.</summary>
    [HarmonyPatch(typeof(MyShipController), nameof(MyShipController.TryEnableBrakes))]
    internal static class TryEnableBrakesPatch
    {
        private static void Prefix(MyShipController __instance, ref bool enable)
        {
            if (ShipControl.Braking && __instance == ShipControl.Seat)
                enable = true;
        }
    }

    /// <summary>The game sets the wheel jump each frame from its own key alone; the joysticks' jump is added to it.</summary>
    [HarmonyPatch(typeof(MyShipController), nameof(MyShipController.WheelJump))]
    internal static class WheelJumpPatch
    {
        private static void Prefix(MyShipController __instance, ref bool controlPressed)
        {
            if (ShipControl.Jumping && __instance == ShipControl.Seat)
                controlPressed = true;
        }
    }

    /// <summary>
    /// Wheels drive at full force whenever the forward request is not zero; this scales the force by how far it is
    /// pushed. The request is the seat's move indicator, which keeps the joystick's fraction (a key gives 1, unchanged),
    /// and reaches a server with this plugin too, so a host or dedicated server running it drives a client's wheels the
    /// same way. Without it on the server, wheels stay on/off. Written with ananace's suspension patch as the reference.
    /// </summary>
    [HarmonyPatch(typeof(Sandbox.Game.Entities.Cube.MyMotorSuspension), "Accelerate")]
    internal static class WheelPropulsionPatch
    {
        private static void Prefix(Sandbox.Game.Entities.Cube.MyMotorSuspension __instance, ref float force)
        {
            if (__instance.PropulsionOverride != 0f)
                return;
            var wheels = __instance.CubeGrid?.GridSystems?.WheelSystem;
            if (wheels == null)
                return;
            float pushed = Math.Abs(wheels.AngularVelocity.Z);
            if (pushed > 0f && pushed < 1f)
                force *= pushed;
        }
    }
}
