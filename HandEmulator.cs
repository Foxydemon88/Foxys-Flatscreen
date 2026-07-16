using UnityEngine;

namespace FlatscreenATTMod
{
	internal sealed class HandEmulator
	{
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

			public float RollAngle;

			public Vector3 ClimbPoseOffset;

			public bool IsClimbingGrabActive;

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
				ResetClimb();
				ResetRestPose();
			}

			public void ResetPose()
			{
				HorizontalAngle = DefaultHorizontalAngle;
				VerticalAngle = DefaultVerticalAngle;
				RollAngle = 0f;
				OffsetX = 0f;
				OffsetY = 0f;
			}

			public void ResetRestPose()
			{
				Depth = BaseOffset.z;
				ResetPose();
			}

			public void ResetPositionOnly()
			{
				Depth = BaseOffset.z;
				OffsetX = 0f;
				OffsetY = 0f;
				ResetClimb();
			}

			public void Clamp()
			{
				Depth = Mathf.Clamp(Depth, -0.15f, 1.6f);
				OffsetX = Mathf.Clamp(OffsetX, -0.45f, 0.45f);
				OffsetY = Mathf.Clamp(OffsetY, -0.35f, 0.55f);
				HorizontalAngle = Mathf.Clamp(HorizontalAngle, -180f, 180f);
				VerticalAngle = Mathf.Clamp(VerticalAngle, -180f, 180f);
				RollAngle = Mathf.Clamp(RollAngle, -180f, 180f);
			}

			public void ResetClimb()
			{
				ClimbPoseOffset = Vector3.zero;
				IsClimbingGrabActive = false;
			}

			public void ClampClimb()
			{
				ClimbPoseOffset.x = Mathf.Clamp(ClimbPoseOffset.x, -0.85f, 0.85f);
				ClimbPoseOffset.y = Mathf.Clamp(ClimbPoseOffset.y, -1.25f, 1.25f);
				ClimbPoseOffset.z = Mathf.Clamp(ClimbPoseOffset.z, -0.35f, 0.35f);
			}
		}

		private const float SelectedRaiseOffset = 0.52f;

		private const float SelectedForwardOffset = 0.24f;

		private const float SelectedForwardPitch = -30f;

		private const float FaceRaiseOffset = 0.58f;

		private const float FaceForwardOffset = 0.08f;

		private const float FacePitch = -8f;

		private const float ArrowAdjustSpeed = 0.55f;

		private const float ClimbHandSpeed = 1.55f;

		private const float ClimbBodyMultiplier = 1f;

		private const float BagBackOffset = 0.28f;

		private const float BagSideOffset = 0.18f;

		private const float BagHeightOffset = 0.34f;

		private readonly HandState _left = new HandState(new Vector3(-0.22f, -0.6f, 0.1f), -14f, 18f);

		private readonly HandState _right = new HandState(new Vector3(0.22f, -0.6f, 0.1f), 14f, 18f);

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

		private Vector3 _climbBodyTranslation;

		private float _climbSensitivity = 1f;

		public string TargetColliderName
		{
			get
			{
				return _targeter.CurrentColliderName;
			}
		}

		public string TargetInteractableName
		{
			get
			{
				return _targeter.CurrentInteractableName;
			}
		}

		public string TargetMenuName
		{
			get
			{
				return _targeter.CurrentMenuTargetName;
			}
		}

		public string TargetParentComponents
		{
			get
			{
				return _targeter.CurrentParentComponents;
			}
		}

		public bool HasTargetPickup
		{
			get
			{
				return _targeter.CurrentPickup != null;
			}
		}

		public bool HasTargetInteractable
		{
			get
			{
				return _targeter.CurrentInteractable != null;
			}
		}

		public float LeftHorizontalAngle
		{
			get
			{
				return _left.HorizontalAngle;
			}
		}

		public float LeftVerticalAngle
		{
			get
			{
				return _left.VerticalAngle;
			}
		}

		public float LeftRollAngle
		{
			get
			{
				return _left.RollAngle;
			}
		}

		public float RightHorizontalAngle
		{
			get
			{
				return _right.HorizontalAngle;
			}
		}

		public float RightVerticalAngle
		{
			get
			{
				return _right.VerticalAngle;
			}
		}

		public float RightRollAngle
		{
			get
			{
				return _right.RollAngle;
			}
		}

		public float LeftDepth
		{
			get
			{
				return _left.Depth;
			}
		}

		public float RightDepth
		{
			get
			{
				return _right.Depth;
			}
		}

		public bool ClimbingModeEnabled { get; set; }

		public bool IsClimbingGrabActive
		{
			get
			{
				return ClimbingModeEnabled && (IsLeftClimbingGrabActive || IsRightClimbingGrabActive);
			}
		}

		public bool IsLeftClimbingGrabActive
		{
			get
			{
				return _left.IsClimbingGrabActive;
			}
		}

		public bool IsRightClimbingGrabActive
		{
			get
			{
				return _right.IsClimbingGrabActive;
			}
		}

		public float ClimbSensitivity
		{
			get
			{
				return _climbSensitivity;
			}
			set
			{
				_climbSensitivity = Mathf.Clamp(value, 0.25f, 3f);
			}
		}

		public void ResetHands()
		{
			_left.Reset();
			_right.Reset();
			_bagAssistActive = false;
			_leftGrabToggle = false;
			_rightGrabToggle = false;
			_climbBodyTranslation = Vector3.zero;
		}

		public void ReleaseAllGrabs()
		{
			_leftGrabToggle = false;
			_rightGrabToggle = false;
			_left.ResetClimb();
			_right.ResetClimb();
		}

		public void TriggerBagAssist(GameReflection game, Transform cameraTransform)
		{
			if (_bagAssistActive)
			{
				_bagAssistActive = false;
				_left.ResetRestPose();
				return;
			}
			object player = game.FindLocalPlayer();
			Transform playerRootTransform = game.GetPlayerRootTransform(player);
			Transform transform = ((playerRootTransform != null) ? playerRootTransform : cameraTransform);
			if (!(transform == null))
			{
				Vector3 vector = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = transform.forward;
				}
				vector.Normalize();
				Vector3 vector2 = Vector3.Cross(Vector3.up, vector);
				if (vector2.sqrMagnitude < 0.0001f)
				{
					vector2 = transform.right;
				}
				vector2.Normalize();
				Transform transform2 = ((cameraTransform != null) ? cameraTransform : transform);
				_bagAssistPosition = new Vector3(transform.position.x - vector.x * 0.28f - vector2.x * 0.18f, transform2.position.y - 0.34f, transform.position.z - vector.z * 0.28f - vector2.z * 0.18f);
				_bagAssistRotation = Quaternion.LookRotation(-vector, Vector3.up) * Quaternion.Euler(0f, -90f, 90f);
				_bagAssistActive = true;
				_leftSelectedToggle = false;
			}
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
			_left.HorizontalAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetLeftVerticalAngle(float value)
		{
			_left.VerticalAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetLeftRollAngle(float value)
		{
			_left.RollAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetRightHorizontalAngle(float value)
		{
			_right.HorizontalAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetRightVerticalAngle(float value)
		{
			_right.VerticalAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetRightRollAngle(float value)
		{
			_right.RollAngle = Mathf.Clamp(value, -180f, 180f);
		}

		public void SetLeftDepth(float value)
		{
			_left.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
		}

		public void SetRightDepth(float value)
		{
			_right.Depth = Mathf.Clamp(value, -0.15f, 1.6f);
		}

		public void SetLeftPalmDownPose()
		{
			SetPalmDownPose(_left, true);
		}

		public void SetRightPalmDownPose()
		{
			SetPalmDownPose(_right, false);
		}

		public void SetBothPalmDownPose()
		{
			SetPalmDownPose(_left, true);
			SetPalmDownPose(_right, false);
		}

		public void SetBothHandshakePose()
		{
			SetHandshakePose(_left, true);
			SetHandshakePose(_right, false);
		}

		public void SetBothFingertipsDownInPose()
		{
			SetFingertipsDownInPose(_left, true);
			SetFingertipsDownInPose(_right, false);
		}

		private static void SetPalmDownPose(HandState state, bool isLeft)
		{
			state.HorizontalAngle = isLeft ? -105f : 105f;
			state.VerticalAngle = -110f;
			state.RollAngle = isLeft ? 122f : -122f;
			state.Clamp();
		}

		private static void SetHandshakePose(HandState state, bool isLeft)
		{
			state.HorizontalAngle = isLeft ? -14f : 14f;
			state.VerticalAngle = -44f;
			state.RollAngle = 0f;
			state.Depth = 0.26f;
			state.Clamp();
		}

		private static void SetFingertipsDownInPose(HandState state, bool isLeft)
		{
			state.HorizontalAngle = isLeft ? -16f : 16f;
			state.VerticalAngle = 57f;
			state.RollAngle = isLeft ? -5f : 5f;
			state.Depth = 0.26f;
			state.Clamp();
		}

		public void Update(object player, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
		{
			UpdateInputs(game.GetLeftInput(player), game.GetRightInput(player), game, input, camera, cameraTransform, cursorLocked);
		}

		public void UpdateInputs(object leftInput, object rightInput, GameReflection game, DesktopInput input, Camera camera, Transform cameraTransform, bool cursorLocked)
		{
			_climbBodyTranslation = Vector3.zero;
			if (!object.ReferenceEquals(_lastLeftInput, leftInput))
			{
				_left.IsLocked = false;
				_left.ResetRestPose();
				_left.ResetClimb();
				_lastLeftInput = leftInput;
			}
			if (!object.ReferenceEquals(_lastRightInput, rightInput))
			{
				_right.IsLocked = false;
				_right.ResetRestPose();
				_right.ResetClimb();
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
				bool isLocked = !_left.IsLocked || !_right.IsLocked;
				_left.IsLocked = isLocked;
				_right.IsLocked = isLocked;
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
					_left.ResetPositionOnly();
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
					_right.ResetPositionOnly();
				}
			}
			float num = input.ReadScroll() * 0.0012f;
			if (Mathf.Abs(num) > 0.0001f)
			{
				if (_leftSelectedToggle)
				{
					_left.Depth += num;
				}
				if (_rightSelectedToggle)
				{
					_right.Depth += num;
				}
				_left.Clamp();
				_right.Clamp();
			}
			float num2 = 0f;
			float num3 = 0f;
			if (input.IsHandAdjustLeftPressed)
			{
				num2 -= 0.55f * Time.deltaTime;
			}
			if (input.IsHandAdjustRightPressed)
			{
				num2 += 0.55f * Time.deltaTime;
			}
			if (input.IsHandAdjustUpPressed)
			{
				num3 += 0.55f * Time.deltaTime;
			}
			if (input.IsHandAdjustDownPressed)
			{
				num3 -= 0.55f * Time.deltaTime;
			}
			if (Mathf.Abs(num2) > 0.0001f || Mathf.Abs(num3) > 0.0001f)
			{
				if (_leftSelectedToggle)
				{
					_left.OffsetX += num2;
					_left.OffsetY += num3;
					_left.Clamp();
				}
				if (_rightSelectedToggle)
				{
					_right.OffsetX += num2;
					_right.OffsetY += num3;
					_right.Clamp();
				}
			}
			Vector3 lookPoint = _targeter.Update(camera, cameraTransform, input.ReadMousePosition(), cursorLocked, Mathf.Max(_left.Depth, _right.Depth));
			if (!ClimbingModeEnabled && input.IsLeftGrabTogglePressed)
			{
				_leftGrabToggle = !_leftGrabToggle;
			}
			if (!ClimbingModeEnabled && input.IsRightGrabTogglePressed)
			{
				_rightGrabToggle = !_rightGrabToggle;
			}
			UpdateClimbing(input, cameraTransform);
			bool isTeleporting = input.IsTeleportPressed && !input.HasMoveInput && !input.IsRunPressed;
			bool leftGrab = ClimbingModeEnabled ? input.IsLeftGrabPressed : _leftGrabToggle;
			bool rightGrab = ClimbingModeEnabled ? input.IsRightGrabPressed : _rightGrabToggle;
			ApplyHand(game, leftInput, _left, _leftSelectedToggle, input.IsLeftFacePressed, _bagAssistActive, _bagAssistPosition, _bagAssistRotation, leftGrab, isTeleporting, lookPoint, input, cameraTransform, true);
			ApplyHand(game, rightInput, _right, _rightSelectedToggle, input.IsRightFacePressed, false, Vector3.zero, Quaternion.identity, rightGrab, isTeleporting, lookPoint, input, cameraTransform, false);
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
			else if (facePose || selected || !state.IsLocked || state.IsClimbingGrabActive)
			{
				Vector3 baseOffset = state.BaseOffset;
				baseOffset.z = state.Depth;
				baseOffset += state.ClimbPoseOffset;
				Quaternion quaternion = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
				float pitchOffset = state.VerticalAngle - state.DefaultVerticalAngle;
				if (facePose)
				{
					baseOffset.y += 0.58f;
					baseOffset.z += 0.08f;
					baseOffset.x += (isLeft ? 0.01f : (-0.01f));
					baseOffset.x += state.OffsetX;
					baseOffset.y += state.OffsetY;
					state.Position = cameraTransform.TransformPoint(baseOffset);
					state.Rotation = cameraTransform.rotation * Quaternion.Euler(-8f + pitchOffset, state.HorizontalAngle, state.RollAngle);
				}
				else if (selected)
				{
					baseOffset.y += 0.52f;
					baseOffset.z += 0.24f;
					baseOffset.x += state.OffsetX;
					baseOffset.y += state.OffsetY;
					state.Position = cameraTransform.TransformPoint(baseOffset);
					state.Rotation = cameraTransform.rotation * Quaternion.Euler(-30f + pitchOffset, state.HorizontalAngle, state.RollAngle);
				}
				else
				{
					state.Position = cameraTransform.position + quaternion * baseOffset;
					state.Rotation = quaternion * Quaternion.Euler(state.VerticalAngle, state.HorizontalAngle, state.RollAngle);
				}
			}
			Transform inputTargetTransform = game.GetInputTargetTransform(playerInput);
			if (inputTargetTransform != null)
			{
				Vector3 position = inputTargetTransform.position;
				inputTargetTransform.position = state.Position;
				inputTargetTransform.rotation = state.Rotation;
				object rawInput = game.GetRawInput(playerInput);
				game.SetRawInputVector3(rawInput, "Velocity", (inputTargetTransform.position - position) / Mathf.Max(Time.deltaTime, 0.0001f));
				game.SetRawInputVector3(rawInput, "AngularVelocity", Vector3.zero);
				game.SetRawInputFloat(rawInput, "GrabAxis", isGrabbing ? 1f : 0f);
				game.SetRawInputFloat(rawInput, "GrabStrength", isGrabbing ? 1f : 0f);
				game.SetRawInputFloat(rawInput, "SecondaryAxis", 0f);
				game.SetButton(playerInput, rawInput, "Grab", isGrabbing);
				game.SetButton(playerInput, rawInput, "HardGrab", isGrabbing);
				game.SetButton(playerInput, rawInput, "Teleport", isTeleporting);
				game.SetButton(playerInput, rawInput, "MetaMenu", input.IsMetaMenuPressed);
				game.SetButton(playerInput, rawInput, "ToggleMetaMenu", input.IsMetaMenuPressed);
			}
		}

		public Vector3 ConsumeClimbBodyTranslation()
		{
			Vector3 climbBodyTranslation = _climbBodyTranslation;
			_climbBodyTranslation = Vector3.zero;
			return climbBodyTranslation;
		}

		private void UpdateClimbing(DesktopInput input, Transform cameraTransform)
		{
			if (!ClimbingModeEnabled || cameraTransform == null)
			{
				_left.ResetClimb();
				_right.ResetClimb();
				return;
			}
			Vector3 vector = input.ReadClimbHandVector();
			Vector3 handDelta = vector * ClimbHandSpeed * _climbSensitivity * Time.deltaTime;
			int activeCount = 0;
			Vector3 mirroredBodyDelta = Vector3.zero;
			if (input.IsLeftGrabPressed)
			{
				activeCount++;
				mirroredBodyDelta -= TransformClimbDelta(cameraTransform, UpdateClimbHand(_left, handDelta));
			}
			else
			{
				_left.ResetClimb();
			}
			if (input.IsRightGrabPressed)
			{
				activeCount++;
				mirroredBodyDelta -= TransformClimbDelta(cameraTransform, UpdateClimbHand(_right, handDelta));
			}
			else
			{
				_right.ResetClimb();
			}
			if (activeCount > 1)
			{
				mirroredBodyDelta /= (float)activeCount;
			}
			_climbBodyTranslation = mirroredBodyDelta * ClimbBodyMultiplier;
		}

		private static Vector3 UpdateClimbHand(HandState state, Vector3 handDelta)
		{
			state.IsClimbingGrabActive = true;
			if (handDelta.sqrMagnitude <= 1E-06f)
			{
				return Vector3.zero;
			}
			Vector3 climbPoseOffset = state.ClimbPoseOffset;
			state.ClimbPoseOffset += handDelta;
			state.ClampClimb();
			return state.ClimbPoseOffset - climbPoseOffset;
		}

		private static Vector3 TransformClimbDelta(Transform cameraTransform, Vector3 localDelta)
		{
			Quaternion quaternion = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
			return quaternion * localDelta;
		}
	}
}
