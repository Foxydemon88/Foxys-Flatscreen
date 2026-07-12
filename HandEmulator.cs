using UnityEngine;

namespace FlatscreenATTMod
{
    internal sealed class HandEmulator
    {
        private const float SelectedRaiseOffset = 0.52f;
        private const float SelectedForwardOffset = 0.24f;
        private const float SelectedForwardPitch = -30f;
        private const float FaceRaiseOffset = 0.58f;
        private const float FaceForwardOffset = 0.08f;
        private const float FacePitch = -8f;
        private const float ArrowAdjustSpeed = 0.55f;

        private readonly HandState _left = new HandState(new Vector3(-0.22f, -0.60f, 0.10f), -14f, 18f);
        private readonly HandState _right = new HandState(new Vector3(0.22f, -0.60f, 0.10f), 14f, 18f);
        private readonly LookTargeter _targeter = new LookTargeter();
        private bool _wasLeftGrabbing;
        private bool _wasRightGrabbing;
        private object _leftInteractor;
        private object _rightInteractor;
        private object _leftHeldInteractable;
        private object _rightHeldInteractable;
        private object _lastLeftInput;
        private object _lastRightInput;
        private bool _leftPickupClickMode;
        private bool _rightPickupClickMode;
        private bool _leftSelectedToggle;
        private bool _rightSelectedToggle;

        public string TargetColliderName { get { return _targeter.CurrentColliderName; } }
        public string TargetInteractableName { get { return _targeter.CurrentInteractableName; } }
        public string TargetMenuName { get { return _targeter.CurrentMenuTargetName; } }
        public string TargetParentComponents { get { return _targeter.CurrentParentComponents; } }
        public bool HasTargetPickup { get { return _targeter.CurrentPickup != null; } }
        public bool HasTargetInteractable { get { return _targeter.CurrentInteractable != null; } }
        public float LeftHorizontalAngle { get { return _left.HorizontalAngle; } }
        public float LeftVerticalAngle { get { return _left.VerticalAngle; } }
        public float RightHorizontalAngle { get { return _right.HorizontalAngle; } }
        public float RightVerticalAngle { get { return _right.VerticalAngle; } }
        public float LeftDepth { get { return _left.Depth; } }
        public float RightDepth { get { return _right.Depth; } }

        public void ResetHands()
        {
            _left.Reset();
            _right.Reset();
            _leftInteractor = null;
            _rightInteractor = null;
            _leftHeldInteractable = null;
            _rightHeldInteractable = null;
            _wasLeftGrabbing = false;
            _wasRightGrabbing = false;
            _leftPickupClickMode = false;
            _rightPickupClickMode = false;
        }

        public void ResetLeftPose()
        {
            _left.ResetPose();
        }

        public void ResetRightPose()
        {
            _right.ResetPose();
        }

        public void SetLeftHorizontalAngle(float value)
        {
            _left.HorizontalAngle = value;
        }

        public void SetLeftVerticalAngle(float value)
        {
            _left.VerticalAngle = value;
        }

        public void SetRightHorizontalAngle(float value)
        {
            _right.HorizontalAngle = value;
        }

        public void SetRightVerticalAngle(float value)
        {
            _right.VerticalAngle = value;
        }

        public void SetLeftDepth(float value)
        {
            _left.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
        }

        public void SetRightDepth(float value)
        {
            _right.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
        }

        public void Update(object player, GameReflection game, DesktopInput input, Transform cameraTransform)
        {
            UpdateInputs(game.GetLeftInput(player), game.GetRightInput(player), game, input, cameraTransform);
        }

        public void UpdateInputs(object leftInput, object rightInput, GameReflection game, DesktopInput input, Transform cameraTransform)
        {
            if (!object.ReferenceEquals(_lastLeftInput, leftInput))
            {
                _leftInteractor = null;
                _leftHeldInteractable = null;
                _wasLeftGrabbing = false;
                _leftPickupClickMode = false;
                _lastLeftInput = leftInput;
            }

            if (!object.ReferenceEquals(_lastRightInput, rightInput))
            {
                _rightInteractor = null;
                _rightHeldInteractable = null;
                _wasRightGrabbing = false;
                _rightPickupClickMode = false;
                _lastRightInput = rightInput;
            }

            if (input.IsResetHandsPressed)
            {
                ResetHands();
            }

            if (input.IsLockLeftPressed)
            {
                _left.IsLocked = !_left.IsLocked;
            }

            if (input.IsLockRightPressed)
            {
                _right.IsLocked = !_right.IsLocked;
            }

            if (input.IsLockBothPressed)
            {
                var shouldLock = !_left.IsLocked || !_right.IsLocked;
                _left.IsLocked = shouldLock;
                _right.IsLocked = shouldLock;
            }

            if (input.IsUnlockHandsPressed)
            {
                _left.IsLocked = false;
                _right.IsLocked = false;
            }

            if (input.IsLeftSelectTogglePressed)
            {
                _leftSelectedToggle = !_leftSelectedToggle;
                if (!_leftSelectedToggle)
                {
                    _left.ResetRestPose();
                }
            }

            if (input.IsRightSelectTogglePressed)
            {
                _rightSelectedToggle = !_rightSelectedToggle;
                if (!_rightSelectedToggle)
                {
                    _right.ResetRestPose();
                }
            }

            var scroll = input.ReadScroll() * 0.0012f;
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                if (_leftSelectedToggle) _left.Depth += scroll;
                if (_rightSelectedToggle) _right.Depth += scroll;
                _left.Clamp();
                _right.Clamp();
            }

            var offsetX = 0f;
            var offsetY = 0f;
            if (input.IsHandAdjustLeftPressed)
            {
                offsetX -= ArrowAdjustSpeed * Time.deltaTime;
            }

            if (input.IsHandAdjustRightPressed)
            {
                offsetX += ArrowAdjustSpeed * Time.deltaTime;
            }

            if (input.IsHandAdjustUpPressed)
            {
                offsetY += ArrowAdjustSpeed * Time.deltaTime;
            }

            if (input.IsHandAdjustDownPressed)
            {
                offsetY -= ArrowAdjustSpeed * Time.deltaTime;
            }

            if (Mathf.Abs(offsetX) > 0.0001f || Mathf.Abs(offsetY) > 0.0001f)
            {
                if (_leftSelectedToggle)
                {
                    _left.OffsetX += offsetX;
                    _left.OffsetY += offsetY;
                    _left.Clamp();
                }

                if (_rightSelectedToggle)
                {
                    _right.OffsetX += offsetX;
                    _right.OffsetY += offsetY;
                    _right.Clamp();
                }
            }

            var lookPoint = _targeter.Update(cameraTransform, Mathf.Max(_left.Depth, _right.Depth));
            HandleGrabInteraction(game, leftInput, ref _leftInteractor, ref _leftHeldInteractable, ref _leftPickupClickMode, _targeter.CurrentPickup, _targeter.CurrentInteractable, _targeter.CurrentMenuTarget, input.IsLeftGrabPressed, ref _wasLeftGrabbing, true);
            HandleGrabInteraction(game, rightInput, ref _rightInteractor, ref _rightHeldInteractable, ref _rightPickupClickMode, _targeter.CurrentPickup, _targeter.CurrentInteractable, _targeter.CurrentMenuTarget, input.IsRightGrabPressed, ref _wasRightGrabbing, false);

            ApplyHand(game, leftInput, _left, _leftSelectedToggle, input.IsLeftFacePressed, input.IsLeftGrabPressed, input.IsTeleportPressed, lookPoint, input, cameraTransform, true);
            ApplyHand(game, rightInput, _right, _rightSelectedToggle, input.IsRightFacePressed, input.IsRightGrabPressed, input.IsTeleportPressed, lookPoint, input, cameraTransform, false);
        }

        private static void ApplyHand(GameReflection game, object playerInput, HandState state, bool selected, bool facePose, bool isGrabbing, bool isTeleporting, Vector3 lookPoint, DesktopInput input, Transform cameraTransform, bool isLeft)
        {
            if (playerInput == null)
            {
                return;
            }

            if (facePose || selected || !state.IsLocked)
            {
                var local = state.BaseOffset;
                local.z = state.Depth;
                var flatRotation = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);

                if (facePose)
                {
                    local.y += FaceRaiseOffset;
                    local.z += FaceForwardOffset;
                    local.x += isLeft ? 0.01f : -0.01f;
                    local.x += state.OffsetX;
                    local.y += state.OffsetY;
                    state.Position = cameraTransform.TransformPoint(local);
                    state.Rotation = cameraTransform.rotation * Quaternion.Euler(FacePitch, state.HorizontalAngle, 0f);
                }
                else if (selected)
                {
                    local.y += SelectedRaiseOffset;
                    local.z += SelectedForwardOffset;
                    local.x += state.OffsetX;
                    local.y += state.OffsetY;
                    state.Position = cameraTransform.TransformPoint(local);
                    state.Rotation = cameraTransform.rotation * Quaternion.Euler(SelectedForwardPitch, state.HorizontalAngle, 0f);
                }
                else
                {
                    state.Position = cameraTransform.position + (flatRotation * local);
                    state.Rotation = flatRotation * Quaternion.Euler(state.VerticalAngle, state.HorizontalAngle, 0f);
                }
            }

            var target = game.GetInputTargetTransform(playerInput);
            if (target != null)
            {
                var previousPosition = target.position;
                target.position = state.Position;
                target.rotation = state.Rotation;

                var raw = game.GetRawInput(playerInput);
                game.SetRawInputVector3(raw, "Velocity", (target.position - previousPosition) / Mathf.Max(Time.deltaTime, 0.0001f));
                game.SetRawInputVector3(raw, "AngularVelocity", Vector3.zero);
                game.SetRawInputFloat(raw, "GrabAxis", isGrabbing ? 1f : 0f);
                game.SetRawInputFloat(raw, "GrabStrength", isGrabbing ? 1f : 0f);
                game.SetRawInputFloat(raw, "SecondaryAxis", 0f);
                game.SetButton(playerInput, raw, "Grab", isGrabbing);
                game.SetButton(playerInput, raw, "HardGrab", isGrabbing);
                game.SetButton(playerInput, raw, "Teleport", isTeleporting);
                game.SetButton(playerInput, raw, "MetaMenu", input.IsMetaMenuPressed);
                game.SetButton(playerInput, raw, "ToggleMetaMenu", input.IsMetaMenuPressed);
            }
        }

        private static void HandleGrabInteraction(GameReflection game, object playerInput, ref object interactor, ref object heldInteractable, ref bool pickupClickMode, object pickup, object interactable, object menuTarget, bool isPressed, ref bool wasPressed, bool isLeft)
        {
            if (playerInput == null)
            {
                return;
            }

            if (interactor == null)
            {
                interactor = game.FindInteractorForInput(playerInput);
            }

            if (isPressed && !wasPressed)
            {
                if (pickup != null && game.TryTestGrab(pickup, isLeft))
                {
                    heldInteractable = pickup;
                    pickupClickMode = true;
                    wasPressed = isPressed;
                    return;
                }

                if (pickup != null && game.TryPickupGrab(interactor, pickup))
                {
                    heldInteractable = pickup;
                    pickupClickMode = true;
                    wasPressed = isPressed;
                    return;
                }

                if (interactable != null)
                {
                    heldInteractable = interactable;
                    if (!game.TryStartInteract(interactor, interactable))
                    {
                        game.TryTestGrab(heldInteractable, isLeft);
                    }
                    wasPressed = isPressed;
                    return;
                }

                if (interactable == null && menuTarget != null && game.TryAdjustMenuWheel(menuTarget, isLeft ? -1f : 1f))
                {
                    wasPressed = isPressed;
                    return;
                }

                wasPressed = isPressed;
            }
            else if (!isPressed && wasPressed)
            {
                if (pickupClickMode)
                {
                    pickupClickMode = false;
                    wasPressed = false;
                    return;
                }

                if (!game.TryTestGrabEnd(heldInteractable, isLeft))
                {
                    game.TryStopInteract(interactor);
                }

                heldInteractable = null;
            }

            wasPressed = isPressed;
        }

        private sealed class HandState
        {
            public readonly Vector3 BaseOffset;
            public readonly float DefaultHorizontalAngle;
            public readonly float DefaultVerticalAngle;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool IsLocked;
            public float Depth;
            public float HorizontalAngle;
            public float OffsetX;
            public float OffsetY;
            public float VerticalAngle;

            public HandState(Vector3 baseOffset, float horizontalAngle, float verticalAngle)
            {
                BaseOffset = baseOffset;
                DefaultHorizontalAngle = horizontalAngle;
                DefaultVerticalAngle = verticalAngle;
                Reset();
            }

            public void Reset()
            {
                IsLocked = false;
                ResetRestPose();
            }

            public void ResetPose()
            {
                HorizontalAngle = DefaultHorizontalAngle;
                VerticalAngle = DefaultVerticalAngle;
                OffsetX = 0f;
                OffsetY = 0f;
            }

            public void ResetRestPose()
            {
                Depth = BaseOffset.z;
                ResetPose();
            }

            public void Clamp()
            {
                Depth = Mathf.Clamp(Depth, -0.15f, 1.6f);
                OffsetX = Mathf.Clamp(OffsetX, -0.45f, 0.45f);
                OffsetY = Mathf.Clamp(OffsetY, -0.35f, 0.55f);
            }
        }
    }
}
