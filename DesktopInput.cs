using System;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
    internal sealed class DesktopInput
    {
        private const float LookSensitivity = 0.12f;

        private readonly InputSystemReflection _inputSystem = new InputSystemReflection();
        private float _yaw;
        private float _pitch;

        public bool IsAvailable { get { return _inputSystem.IsAvailable; } }
        public bool IsControlTogglePressed { get { return _inputSystem.WasPressed("capsLockKey"); } }
        public bool IsDebugTogglePressed { get { return _inputSystem.WasPressed("f10Key"); } }
        public bool IsServerBrowserTogglePressed { get { return _inputSystem.WasPressed("f7Key"); } }
        public bool IsLeftSelectTogglePressed { get { return _inputSystem.WasPressed("qKey"); } }
        public bool IsRightSelectTogglePressed { get { return _inputSystem.WasPressed("eKey"); } }
        public bool IsLeftGrabTogglePressed { get { return _inputSystem.WasMousePressed("leftButton"); } }
        public bool IsRightGrabTogglePressed { get { return _inputSystem.WasMousePressed("rightButton"); } }
        public bool IsLeftGrabPressed { get { return _inputSystem.IsMousePressed("leftButton"); } }
        public bool IsRightGrabPressed { get { return _inputSystem.IsMousePressed("rightButton"); } }
        public bool IsTeleportPressed { get { return _inputSystem.IsPressed("tKey"); } }
        public bool IsBagTogglePressed { get { return _inputSystem.WasPressed("iKey"); } }
        public bool IsThirdPersonTogglePressed { get { return _inputSystem.WasPressed("vKey"); } }
        public bool IsRunPressed { get { return _inputSystem.IsPressed("leftShiftKey") || _inputSystem.IsPressed("rightShiftKey"); } }
        public bool IsHeightUpPressed { get { return _inputSystem.IsPressed("rKey"); } }
        public bool IsHeightDownPressed { get { return _inputSystem.IsPressed("fKey"); } }
        public bool IsMenuFlyUpPressed { get { return _inputSystem.IsPressed("spaceKey"); } }
        public bool IsMenuFlyDownPressed { get { return _inputSystem.IsPressed("leftCtrlKey") || _inputSystem.IsPressed("rightCtrlKey"); } }
        public bool IsResetHandsPressed { get { return _inputSystem.WasPressed("homeKey"); } }
        public bool IsMetaMenuPressed { get { return _inputSystem.WasPressed("tabKey") || _inputSystem.WasPressed("escapeKey"); } }
        public bool IsLockLeftPressed { get { return _inputSystem.WasPressed("digit1Key"); } }
        public bool IsLockRightPressed { get { return _inputSystem.WasPressed("digit2Key"); } }
        public bool IsLockBothPressed { get { return _inputSystem.WasPressed("digit3Key"); } }
        public bool IsUnlockHandsPressed { get { return _inputSystem.WasPressed("digit4Key"); } }
        public bool IsLeftFacePressed { get { return _inputSystem.IsPressed("digit6Key"); } }
        public bool IsRightFacePressed { get { return _inputSystem.IsPressed("digit7Key"); } }
        public bool IsHandAdjustLeftPressed { get { return _inputSystem.IsPressed("leftArrowKey"); } }
        public bool IsHandAdjustRightPressed { get { return _inputSystem.IsPressed("rightArrowKey"); } }
        public bool IsHandAdjustUpPressed { get { return _inputSystem.IsPressed("upArrowKey"); } }
        public bool IsHandAdjustDownPressed { get { return _inputSystem.IsPressed("downArrowKey"); } }
        public bool HasMoveInput
        {
            get
            {
                return _inputSystem.IsPressed("wKey") ||
                       _inputSystem.IsPressed("aKey") ||
                       _inputSystem.IsPressed("sKey") ||
                       _inputSystem.IsPressed("dKey");
            }
        }

        public Quaternion CameraRotation { get { return Quaternion.Euler(_pitch, _yaw, 0f); } }
        public Vector3 ForwardOnPlane { get { return Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward; } }
        public Vector3 RightOnPlane { get { return Quaternion.Euler(0f, _yaw, 0f) * Vector3.right; } }

        public void SetInitialRotation(Quaternion rotation)
        {
            var euler = rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
        }

        public void UpdateLook(bool cursorLocked)
        {
            if (!IsAvailable || !cursorLocked)
            {
                return;
            }

            var delta = _inputSystem.ReadMouseVector2("delta");
            _yaw += delta.x * LookSensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * LookSensitivity, -85f, 85f);
        }

        public Vector3 ReadMoveVector()
        {
            var move = Vector3.zero;

            if (_inputSystem.IsPressed("wKey")) move += ForwardOnPlane;
            if (_inputSystem.IsPressed("sKey")) move -= ForwardOnPlane;
            if (_inputSystem.IsPressed("dKey")) move += RightOnPlane;
            if (_inputSystem.IsPressed("aKey")) move -= RightOnPlane;
            move.y = 0f;
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }

        public float ReadScroll()
        {
            return IsAvailable ? _inputSystem.ReadMouseVector2("scroll").y : 0f;
        }

        public Vector2 ReadMousePosition()
        {
            return IsAvailable ? _inputSystem.ReadMouseVector2("position") : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
        }

        private sealed class InputSystemReflection
        {
            private Type _keyboardType;
            private Type _mouseType;
            private PropertyInfo _keyboardCurrentProperty;
            private PropertyInfo _mouseCurrentProperty;
            private MethodInfo _readVector2Method;

            public bool IsAvailable
            {
                get
                {
                    EnsureTypes();
                    return Keyboard != null && Mouse != null;
                }
            }

            private object Keyboard
            {
                get { return _keyboardCurrentProperty == null ? null : _keyboardCurrentProperty.GetValue(null, null); }
            }

            private object Mouse
            {
                get { return _mouseCurrentProperty == null ? null : _mouseCurrentProperty.GetValue(null, null); }
            }

            public bool IsPressed(string keyProperty)
            {
                return ReadBooleanControl(Keyboard, keyProperty, "isPressed");
            }

            public bool WasPressed(string keyProperty)
            {
                return ReadBooleanControl(Keyboard, keyProperty, "wasPressedThisFrame");
            }

            public bool IsMousePressed(string mouseProperty)
            {
                return ReadBooleanControl(Mouse, mouseProperty, "isPressed");
            }

            public bool WasMousePressed(string mouseProperty)
            {
                return ReadBooleanControl(Mouse, mouseProperty, "wasPressedThisFrame");
            }

            public Vector2 ReadMouseVector2(string mouseProperty)
            {
                var mouse = Mouse;
                if (mouse == null)
                {
                    return Vector2.zero;
                }

                var control = GetPropertyValue(mouse, mouseProperty);
                if (control == null)
                {
                    return Vector2.zero;
                }

                if (_readVector2Method == null || _readVector2Method.DeclaringType != control.GetType())
                {
                    _readVector2Method = control.GetType().GetMethod("ReadValue", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                }

                if (_readVector2Method == null)
                {
                    return Vector2.zero;
                }

                var value = _readVector2Method.Invoke(control, null);
                return value is Vector2 ? (Vector2)value : Vector2.zero;
            }

            private bool ReadBooleanControl(object device, string controlProperty, string valueProperty)
            {
                var control = GetPropertyValue(device, controlProperty);
                if (control == null)
                {
                    return false;
                }

                var property = control.GetType().GetProperty(valueProperty, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null && (bool)property.GetValue(control, null);
            }

            private static object GetPropertyValue(object instance, string propertyName)
            {
                if (instance == null)
                {
                    return null;
                }

                var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property == null ? null : property.GetValue(instance, null);
            }

            private void EnsureTypes()
            {
                if (_keyboardType != null && _mouseType != null)
                {
                    return;
                }

                TryFindTypes();

                if (_keyboardType == null || _mouseType == null)
                {
                    try
                    {
                        Assembly.Load("Unity.InputSystem");
                    }
                    catch
                    {
                    }

                    TryFindTypes();
                }

                _keyboardCurrentProperty = _keyboardType == null ? null : _keyboardType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
                _mouseCurrentProperty = _mouseType == null ? null : _mouseType.GetProperty("current", BindingFlags.Static | BindingFlags.Public);
            }

            private void TryFindTypes()
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (_keyboardType == null)
                    {
                        _keyboardType = assembly.GetType("UnityEngine.InputSystem.Keyboard");
                    }

                    if (_mouseType == null)
                    {
                        _mouseType = assembly.GetType("UnityEngine.InputSystem.Mouse");
                    }
                }
            }
        }
    }
}
