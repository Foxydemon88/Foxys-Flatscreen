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
        private const float BagBackOffset = 0.28f;
        private const float BagSideOffset = 0.18f;
        private const float BagHeightOffset = 0.34f;
        private readonly HandState _left = new HandState(new Vector3(-0.22f, -0.60f, 0.10f), -14f, 18f);
        private readonly HandState _right = new HandState(new Vector3(0.22f, -0.60f, 0.10f), 14f, 18f);
        private readonly LookTargeter _targeter = new LookTargeter();
        private object _lastLeftInput;
        private object _lastRightInput;
        private bool _leftSelectedToggle;
        private bool _rightSelectedToggle;
        private bool _leftGrabToggle;
        private bool _rightGrabToggle;
        private bool _bagAssistActive;
        private Vector3 _bagAssistPosition;
        private Quaternion _bagAssistRotation;

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
            _bagAssistActive = false;
            _leftGrabToggle = false;
            _rightGrabToggle = false;
        }

        public void ReleaseAllGrabs()
        {
            _leftGrabToggle = false;
            _rightGrabToggle = false;
        }

        public void TriggerBagAssist(GameReflection game, Transform cameraTransform)
        {
            if (_bagAssistActive)
            {
                _bagAssistActive = false;
                _left.ResetRestPose();
                return;
            }

            var localPlayer = game.FindLocalPlayer();
            var playerRoot = game.GetPlayerRootTransform(localPlayer);
            var reference = playerRoot != null ? playerRoot : cameraTransform;
            if (reference == null)
            {
                return;
            }

            var forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = reference.forward;
            }
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = reference.right;
            }
            right.Normalize();

            var heightSource = cameraTransform != null ? cameraTransform : reference;
            _bagAssistPosition = new Vector3(
                reference.position.x - forward.x * BagBackOffset - right.x * BagSideOffset,
                heightSource.position.y - BagHeightOffset,
                reference.position.z - forward.z * BagBackOffset - right.z * BagSideOffset);
            _bagAssistRotation = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Euler(0f, -90f, 90f);
            _bagAssistActive = true;
            _leftSelectedToggle = false;
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

        public void Update(object player, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
        {
            UpdateInputs(game.GetLeftInput(player), game.GetRightInput(player), game, input, camera, cameraTransform, cursorLocked);
        }

        public void UpdateInputs(object leftInput, object rightInput, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
        {
            if (!object.ReferenceEquals(_lastLeftInput, leftInput))
            {
                _left.IsLocked = false;
                _left.ResetRestPose();
                _lastLeftInput = leftInput;
            }

            if (!object.ReferenceEquals(_lastRightInput, rightInput))
            {
                _right.IsLocked = false;
                _right.ResetRestPose();
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
                if (_leftSelectedToggle)
                {
                    _left.IsLocked = false;
                }
                else
                {
                    _left.ResetRestPose();
                }
            }

            if (input.IsRightSelectTogglePressed)
            {
                _rightSelectedToggle = !_rightSelectedToggle;
                if (_rightSelectedToggle)
                {
                    _right.IsLocked = false;
                }
                else
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

            var lookPoint = _targeter.Update(camera, cameraTransform, input.ReadMousePosition(), cursorLocked, Mathf.Max(_left.Depth, _right.Depth));

            if (input.IsLeftGrabTogglePressed)
            {
                _leftGrabToggle = !_leftGrabToggle;
            }

            if (input.IsRightGrabTogglePressed)
            {
                _rightGrabToggle = !_rightGrabToggle;
            }

            var canTeleport = input.IsTeleportPressed && !input.HasMoveInput && !input.IsRunPressed;
            ApplyHand(game, leftInput, _left, _leftSelectedToggle, input.IsLeftFacePressed, _bagAssistActive, _bagAssistPosition, _bagAssistRotation, _leftGrabToggle, canTeleport, lookPoint, input, cameraTransform, true);
            ApplyHand(game, rightInput, _right, _rightSelectedToggle, input.IsRightFacePressed, false, Vector3.zero, Quaternion.identity, _rightGrabToggle, canTeleport, lookPoint, input, cameraTransform, false);
        }

        private static void ApplyHand(GameReflection game, object playerInput, HandState state, bool selected, bool facePose, bool bagPose, Vector3 bagPosePosition, Quaternion bagPoseRotation, bool isGrabbing, bool isTeleporting, Vector3 lookPoint, DesktopInput input, Transform cameraTransform, bool isLeft)
        {
            if (playerInput == null)
            {
                return;
            }

            if (bagPose)
            {
                state.Position = bagPosePosition;
                state.Rotation = bagPoseRotation;
            }
            else if (facePose || selected || !state.IsLocked)
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
