using UnityEngine;
using UnityEngine.InputSystem;

namespace Voidborne.Vehicles
{
    /// <summary>
    /// Reads per-frame input for a vehicle. Enabled only while the player is driving.
    /// Uses UnityEngine.InputSystem keyboard polling to avoid requiring a Vehicle action map asset.
    /// </summary>
    public class VehicleInput : MonoBehaviour
    {
        // Inputs sampled this frame
        public float Throttle { get; private set; }   // -1 to 1 (W=forward, S=reverse)
        public float Steer   { get; private set; }   // -1 to 1 (A=left, D=right)
        public float Brake   { get; private set; }   // 0 or 1 (Space)
        public bool  Boost   { get; private set; }   // Shift
        public bool  ExitPressed   { get; private set; }   // E
        public bool  ActionPressed { get; private set; }   // F (horn/drill/rotor)
        public bool  InventoryPressed { get; private set; } // Tab

        private void Update()
        {
            if (Keyboard.current == null) return;

            float fwd = Keyboard.current.wKey.isPressed ? 1f : 0f;
            float rev = Keyboard.current.sKey.isPressed ? -1f : 0f;
            Throttle = fwd + rev;

            float left  = Keyboard.current.aKey.isPressed ? -1f : 0f;
            float right = Keyboard.current.dKey.isPressed ?  1f : 0f;
            Steer = left + right;

            Brake = Keyboard.current.spaceKey.isPressed ? 1f : 0f;
            Boost = Keyboard.current.leftShiftKey.isPressed;
            ExitPressed       = Keyboard.current.eKey.wasPressedThisFrame;
            ActionPressed     = Keyboard.current.fKey.wasPressedThisFrame;
            InventoryPressed  = Keyboard.current.tabKey.wasPressedThisFrame;
        }
    }
}
