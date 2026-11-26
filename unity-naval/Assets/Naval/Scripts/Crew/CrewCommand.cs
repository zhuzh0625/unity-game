using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Galleon
{
    public enum CrewWeapon { None, Pistol, Cutlass, Medicine }

    // A frame of the same controls used by the keyboard and mouse.
    public struct CrewCommand
    {
        public Vector2 Move;
        public Vector2 Look;
        public bool UsePressed;
        public bool FirePressed;
        public CrewWeapon WeaponKey;
        public bool AimPressed, AttackPressed, AttackHeld;
        public bool JumpPressed;
        public bool Sprint;
        public int SailChange;
        public float Zoom;

        internal static CrewCommand Read()
        {
            CrewCommand value = default;
#if ENABLE_INPUT_SYSTEM
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys != null)
            {
                value.Move = new Vector2(
                    Axis(keys.dKey.isPressed || keys.rightArrowKey.isPressed, keys.aKey.isPressed || keys.leftArrowKey.isPressed),
                    Axis(keys.wKey.isPressed || keys.upArrowKey.isPressed, keys.sKey.isPressed || keys.downArrowKey.isPressed));
                value.UsePressed = keys.eKey.wasPressedThisFrame;
                if (keys.digit1Key.wasPressedThisFrame || keys.numpad1Key.wasPressedThisFrame) value.WeaponKey = CrewWeapon.Pistol;
                else if (keys.digit2Key.wasPressedThisFrame || keys.numpad2Key.wasPressedThisFrame) value.WeaponKey = CrewWeapon.Cutlass;
                else if (keys.digit3Key.wasPressedThisFrame || keys.numpad3Key.wasPressedThisFrame) value.WeaponKey = CrewWeapon.Medicine;
                value.JumpPressed = keys.spaceKey.wasPressedThisFrame;
                value.Sprint = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
                value.SailChange = Axis(keys.wKey.wasPressedThisFrame, keys.sKey.wasPressedThisFrame);
            }
            if (mouse != null)
            {
                value.Look = mouse.delta.ReadValue();
                value.AimPressed = mouse.rightButton.wasPressedThisFrame;
                value.AttackPressed = mouse.leftButton.wasPressedThisFrame;
                value.AttackHeld = mouse.leftButton.isPressed;
                value.Zoom = System.Math.Sign(mouse.scroll.y.ReadValue());
            }
#else
            value.Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            value.Look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 15f;
            value.UsePressed = Input.GetKeyDown(KeyCode.E);
            value.JumpPressed = Input.GetKeyDown(KeyCode.Space);
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) value.WeaponKey = CrewWeapon.Pistol;
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) value.WeaponKey = CrewWeapon.Cutlass;
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) value.WeaponKey = CrewWeapon.Medicine;
            value.AimPressed = Input.GetMouseButtonDown(1);
            value.AttackPressed = Input.GetMouseButtonDown(0);
            value.AttackHeld = Input.GetMouseButton(0);
            value.Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            value.SailChange = Axis(Input.GetKeyDown(KeyCode.W), Input.GetKeyDown(KeyCode.S));
            value.Zoom = Input.mouseScrollDelta.y;
#endif
            value.FirePressed = value.AttackPressed || value.JumpPressed;
            return value;
        }

        private static int Axis(bool positive, bool negative)
        {
            return (positive ? 1 : 0) - (negative ? 1 : 0);
        }
    }
}
