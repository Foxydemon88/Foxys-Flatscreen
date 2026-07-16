using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace FlatscreenATTMod
{
	public sealed class FlatscreenMod : MelonMod
	{
		internal static bool SuppressOpenXRInputUpdates;

		private readonly DesktopInput _input = new DesktopInput();

		private readonly GameReflection _game = new GameReflection();

		private readonly HandEmulator _hands = new HandEmulator();

		private readonly ServerBrowserOverlay _serverBrowser = new ServerBrowserOverlay();

		private Camera _camera;

		private Camera _menuCamera;

		private Camera _thirdPersonCamera;

		private bool _cursorLocked = true;

		private bool _showDebug;

		private bool _showServerBrowser = true;

		private bool _menuCameraInitialized;

		private bool _customizationCameraInitialized;

		private bool _wasCustomizationMode;

		private bool _loggedMissingInput;

		private bool _pendingReturnToMenu;

		private string _lastStatus = "starting";

		private string _lastMode = "none";

		private object _lastPlayer;

		private Transform _lastPlayerTransform;

		private object _lastLeftInput;

		private object _lastRightInput;

		private bool _wasMovementBlocked;

		private bool _serverSessionActive;

		private Vector3 _menuCameraPosition;

		private float _heightOffset = 1.45f;

		private float _targetFieldOfView = 90f;

		private bool _fieldOfViewInitialized;

		private bool _thirdPersonEnabled;

		private float _thirdPersonDistance = 2.35f;

		private float _thirdPersonHeight = 0.55f;

		private Vector3 _terrainBasePosition;

		private bool _terrainBasePositionSet;

		private bool _terrainDown;

		internal static FlatscreenMod Instance { get; private set; }

		internal float HeightOffset
		{
			get
			{
				return _heightOffset;
			}
		}

		internal float CameraFieldOfView
		{
			get
			{
				Camera camera = ((_camera != null) ? _camera : Camera.main);
				return (camera != null) ? camera.fieldOfView : _targetFieldOfView;
			}
		}

		internal float LookSensitivity
		{
			get
			{
				return _input.LookSensitivityMultiplier;
			}
		}

		internal bool ThirdPersonEnabled
		{
			get
			{
				return _thirdPersonEnabled;
			}
		}

		internal float ThirdPersonDistance
		{
			get
			{
				return _thirdPersonDistance;
			}
		}

		public override void OnInitializeMelon()
		{
			Instance = this;
			new HarmonyLib.Harmony("FlatscreenATTMod").PatchAll(typeof(OpenXRInputPatches).Assembly);
			base.LoggerInstance.Msg("Loaded. F7 opens the control menu, Caps Lock toggles control lock, F10 toggles debug overlay.");
		}

		public override void OnUpdate()
		{
			if (!_input.IsAvailable)
			{
				_lastStatus = "waiting for keyboard/mouse from Unity InputSystem";
				if (!_loggedMissingInput)
				{
					_loggedMissingInput = true;
					base.LoggerInstance.Warning("Unity InputSystem keyboard/mouse devices are not available yet.");
				}
				return;
			}
			if (_input.IsControlTogglePressed)
			{
				_cursorLocked = !_cursorLocked;
				base.LoggerInstance.Msg("Control lock " + (_cursorLocked ? "enabled" : "disabled") + ".");
			}
			if (_input.IsDebugTogglePressed)
			{
				_showDebug = !_showDebug;
				base.LoggerInstance.Msg("Debug overlay " + (_showDebug ? "enabled" : "disabled") + ".");
			}
			if (_input.IsServerBrowserTogglePressed)
			{
				_showServerBrowser = !_showServerBrowser;
				base.LoggerInstance.Msg("Server browser " + (_showServerBrowser ? "enabled" : "disabled") + ".");
			}
			if (_input.IsBagTogglePressed)
			{
				_hands.TriggerBagAssist(_game, GetViewTransform());
				base.LoggerInstance.Msg("Bag assist triggered.");
			}
			if (_input.IsThirdPersonTogglePressed)
			{
				_thirdPersonEnabled = !_thirdPersonEnabled;
				base.LoggerInstance.Msg("Third person " + (_thirdPersonEnabled ? "enabled" : "disabled") + ".");
			}
			ApplyCursorState();
		}

		public override void OnLateUpdate()
		{
			SuppressOpenXRInputUpdates = true;
			if (!_input.IsAvailable)
			{
				SuppressOpenXRInputUpdates = false;
			}
			else if (_pendingReturnToMenu)
			{
				_lastStatus = "returning to menu";
				_lastMode = "server";
				_camera = null;
				_game.TryReturnToMainMenu();
				object obj = _game.FindLocalPlayer();
				if (obj == null)
				{
					_pendingReturnToMenu = false;
					_serverSessionActive = false;
					_lastPlayerTransform = null;
				}
			}
			else
			{
				object obj2 = _game.FindLocalPlayer();
				if (obj2 != null)
				{
					_lastPlayer = obj2;
					_serverSessionActive = true;
					UpdateServerPlayer(obj2);
				}
				else if (HoldMissingServerPlayerCamera())
				{
				}
				else
				{
					UpdateMainMenu();
				}
			}
		}

		public override void OnGUI()
		{
			if (_showServerBrowser)
			{
				_serverBrowser.Draw(_game, _hands);
			}
			if (_showDebug)
			{
				GUILayout.BeginArea(new Rect(12f, 12f, 560f, 520f), GUI.skin.box);
				GUILayout.Label("Flatscreen ATT Mod 1.4.0");
				GUILayout.Label("SteamVR/Alta launcher note: menu uses free-fly, server mode stays horizontal.");
				GUILayout.Label("Status: " + _lastStatus);
				GUILayout.Label("Mode: " + _lastMode);
				GUILayout.Label("Input available: " + _input.IsAvailable);
				GUILayout.Label("Cursor locked: " + _cursorLocked);
				GUILayout.Label("Player type: " + _game.PlayerTypeName);
				GUILayout.Label("Controller type: " + _game.PlayerControllerTypeName);
				GUILayout.Label("Has Player.Current: " + _game.HasCurrentPlayerProperty);
				GUILayout.Label("Has PlayerController.Current: " + _game.HasCurrentPlayerControllerProperty);
				GUILayout.Label("Player object: " + ((_lastPlayer == null) ? "null" : _lastPlayer.GetType().FullName));
				GUILayout.Label("Player transform: " + ((_lastPlayerTransform == null) ? "null" : _lastPlayerTransform.name));
				GUILayout.Label("Left input: " + ((_lastLeftInput == null) ? "null" : _lastLeftInput.GetType().FullName));
				GUILayout.Label("Right input: " + ((_lastRightInput == null) ? "null" : _lastRightInput.GetType().FullName));
				GUILayout.Label("Locomotion found: " + _game.HasLocomotionController);
				GUILayout.Label("Height offset: " + _heightOffset.ToString("0.00"));
				GUILayout.Label("Climbing mode: " + (_hands.ClimbingModeEnabled ? "enabled" : "disabled") + " / active: " + _hands.IsClimbingGrabActive);
				GUILayout.Label("Target collider: " + _hands.TargetColliderName);
				GUILayout.Label("Target pickup: " + _hands.HasTargetPickup);
				GUILayout.Label("Target interactable: " + _hands.TargetInteractableName + " / " + _hands.HasTargetInteractable);
				GUILayout.Label("Target menu: " + _hands.TargetMenuName);
				GUILayout.Label("Target components: " + _hands.TargetParentComponents);
				GUILayout.Label("Interactor: " + _game.LastInteractorName);
				GUILayout.Label("Interact result: " + _game.LastInteractResult);
				GUILayout.Label("Control menu: " + _showServerBrowser + " / " + _serverBrowser.Count + " / " + _serverBrowser.Status);
				GUILayout.Label("Third person: " + _thirdPersonEnabled + " Distance: " + _thirdPersonDistance.ToString("0.00"));
				GUILayout.Label("F7 menu, Caps Lock control lock, I bag, V third person, F10 debug");
				GUILayout.EndArea();
			}
		}

		private void UpdateServerPlayer(object player)
		{
			object controller = _game.FindLocalController();
			Transform controllerTransform = _game.GetControllerTransform(controller);
			Camera controllerCamera = _game.GetControllerCamera(controller);
			Transform playerTransform = _game.GetPlayerTransform(player);
			Transform transform = ((controllerTransform != null) ? controllerTransform : _game.GetPlayerRootTransform(player));
			Camera camera = ((controllerCamera != null) ? controllerCamera : _game.GetPlayerCamera(player));
			_lastPlayerTransform = ((transform != null) ? transform : playerTransform);
			if (transform == null)
			{
				_lastStatus = "server player found, but no root transform";
				_lastMode = "server";
				return;
			}
			if (camera != null)
			{
				_camera = camera;
				_camera.enabled = true;
				_camera.tag = "MainCamera";
				ApplyDefaultFieldOfView(_camera);
				UpdateServerCamera(transform, _camera.transform);
			}
			else
			{
				EnsureCamera();
				if (_camera != null)
				{
					UpdateServerCamera(transform, _camera.transform);
				}
			}
			Camera viewCamera = GetViewCamera();
			if (_thirdPersonEnabled)
			{
				UpdateThirdPersonCamera(transform, _camera);
				viewCamera = ((_thirdPersonCamera != null) ? _thirdPersonCamera : _camera);
			}
			else
			{
				DisableThirdPersonCamera();
				viewCamera = _camera;
			}
			AdjustHeight();
			_input.UpdateLook(_cursorLocked);
			bool flag = _game.IsMovementBlocked(player, controller);
			bool flag2 = _game.IsCustomizationObject(player) || _game.IsCustomizationObject(controller) || _game.IsCustomizationTransform(transform) || (flag && _game.IsCustomizationActive());
			if (flag2)
			{
				_wasCustomizationMode = true;
				UpdateCustomizationArea(player, transform, camera);
				return;
			}
			if (_wasCustomizationMode)
			{
				RestorePlayerCameraAfterCustomization(camera);
			}
			bool flag3 = !flag || flag2;
			_customizationCameraInitialized = false;
			_wasCustomizationMode = false;
			if (flag)
			{
				if (!ShouldPreserveBlockedHandInteraction())
				{
					_game.TryReleaseHeldHands(player);
				}
				if (!_wasMovementBlocked)
				{
					_hands.ResetHands();
				}
			}
			else if (_wasMovementBlocked)
			{
				_game.TryReleaseHeldHands(player);
			}
			_wasMovementBlocked = flag;
			if (_cursorLocked && flag3)
			{
				MovePlayer(player, controller, transform);
			}
			_lastLeftInput = _game.GetLeftInput(player);
			_lastRightInput = _game.GetRightInput(player);
			if (_lastLeftInput == null || _lastRightInput == null)
			{
				GameReflection.PlayerInputPair playerInputPair = _game.FindLoosePlayerInputs();
				if (_lastLeftInput == null)
				{
					_lastLeftInput = playerInputPair.Left;
				}
				if (_lastRightInput == null)
				{
					_lastRightInput = playerInputPair.Right;
				}
			}
			Camera camera2 = ((_camera != null) ? _camera : viewCamera);
			Transform transform2 = ((camera2 != null) ? camera2.transform : null);
			if (camera2 != null && transform2 != null)
			{
				_hands.UpdateInputs(_lastLeftInput, _lastRightInput, _game, _input, camera2, transform2, _cursorLocked);
			}
			if (_cursorLocked && flag3)
			{
				ApplyClimbTranslation(player, controller, transform);
			}
			_lastStatus = (flag2 ? "controlling customization area" : (flag ? "server movement blocked" : (_hands.IsClimbingGrabActive ? "climbing server player" : (_cursorLocked ? "controlling server player" : "server controls unlocked"))));
			_lastMode = flag2 ? "customization" : "server";
		}

		private bool HoldMissingServerPlayerCamera()
		{
			if (!_serverSessionActive)
			{
				return false;
			}
			if (_lastPlayerTransform != null)
			{
				AdjustHeight();
				_input.UpdateLook(_cursorLocked);
				EnsureCamera();
				if (_camera != null)
				{
					UpdateServerCamera(_lastPlayerTransform, _camera.transform);
				}
			}
			_hands.ReleaseAllGrabs();
			_wasMovementBlocked = true;
			_lastStatus = "server player unavailable, movement blocked";
			_lastMode = "server";
			return true;
		}

		private void RestorePlayerCameraAfterCustomization(Camera playerCamera)
		{
			DisableThirdPersonCamera();
			if (_camera != null && _camera.name == "Flatscreen Desktop Camera")
			{
				_camera.enabled = false;
				_camera.tag = "Untagged";
				_camera = null;
			}
			if (playerCamera != null)
			{
				playerCamera.enabled = true;
				playerCamera.tag = "MainCamera";
				_camera = playerCamera;
				ApplyDefaultFieldOfView(_camera);
				_input.SetInitialRotation(_camera.transform.rotation);
			}
			_customizationCameraInitialized = false;
			_menuCameraInitialized = false;
		}

		private void UpdateCustomizationArea(object player, Transform playerRoot, Camera sourceCamera)
		{
			_wasMovementBlocked = false;
			DisableThirdPersonCamera();
			_lastLeftInput = _game.GetLeftInput(player);
			_lastRightInput = _game.GetRightInput(player);
			if (_lastLeftInput == null || _lastRightInput == null)
			{
				GameReflection.PlayerInputPair playerInputPair = _game.FindLoosePlayerInputs();
				if (_lastLeftInput == null)
				{
					_lastLeftInput = playerInputPair.Left;
				}
				if (_lastRightInput == null)
				{
					_lastRightInput = playerInputPair.Right;
				}
			}
			EnsureCamera(true);
			if (_camera == null)
			{
				_lastStatus = "customization: waiting for camera";
				_lastMode = "customization";
				return;
			}
			if (!_customizationCameraInitialized)
			{
				Transform playerHeadTransform = _game.GetPlayerHeadTransform(player);
				if (playerHeadTransform != null)
				{
					_menuCameraPosition = playerHeadTransform.position;
					_input.SetInitialRotation(playerHeadTransform.rotation);
				}
				else if (playerRoot != null)
				{
					_menuCameraPosition = playerRoot.position + Vector3.up * _heightOffset;
					_input.SetInitialRotation(Quaternion.Euler(0f, playerRoot.rotation.eulerAngles.y, 0f));
				}
				else if (sourceCamera != null)
				{
					_menuCameraPosition = sourceCamera.transform.position;
					_input.SetInitialRotation(sourceCamera.transform.rotation);
				}
				else
				{
					_menuCameraPosition = _camera.transform.position;
				}
				_customizationCameraInitialized = true;
			}
			if (_cursorLocked)
			{
				MoveMenuCamera();
			}
			_camera.transform.position = _menuCameraPosition;
			_camera.transform.rotation = _input.CameraRotation;
			_hands.UpdateInputs(_lastLeftInput, _lastRightInput, _game, _input, _camera, _camera.transform, _cursorLocked);
			_lastStatus = (_cursorLocked ? "controlling customization camera" : "customization controls unlocked");
			_lastMode = "customization";
		}

		private void UpdateMainMenu()
		{
			_lastPlayerTransform = null;
			GameReflection.PlayerInputPair playerInputPair = _game.FindLoosePlayerInputs();
			_lastLeftInput = playerInputPair.Left;
			_lastRightInput = playerInputPair.Right;
			_wasMovementBlocked = false;
			if (!playerInputPair.HasAny)
			{
				_lastStatus = "main menu: waiting for PlayerInput hands";
				_lastMode = "menu";
				return;
			}
			EnsureMenuCamera();
			AdjustHeight();
			_input.UpdateLook(_cursorLocked);
			if (_cursorLocked)
			{
				MoveMenuCamera();
			}
			_camera.transform.position = _menuCameraPosition;
			_camera.transform.rotation = _input.CameraRotation;
			_hands.UpdateInputs(playerInputPair.Left, playerInputPair.Right, _game, _input, _camera, _camera.transform, _cursorLocked);
			_lastStatus = (_cursorLocked ? "controlling main menu hands" : "menu controls unlocked");
			_lastMode = "menu";
		}

		private void EnsureCamera(bool forceDedicatedCamera = false)
		{
			if (_camera != null && (!forceDedicatedCamera || _camera.name == "Flatscreen Desktop Camera"))
			{
				return;
			}
			Camera camera = null;
			if (!forceDedicatedCamera)
			{
				camera = Camera.main;
				if (camera != null)
				{
					_camera = camera;
					_camera.enabled = true;
					_camera.tag = "MainCamera";
					ApplyDefaultFieldOfView(_camera);
					return;
				}
			}
			if (forceDedicatedCamera && _camera != null && _camera.name != "Flatscreen Desktop Camera")
			{
				_camera.enabled = false;
				camera = _camera;
				_camera = null;
			}
			if (_camera == null)
			{
				if (camera == null)
				{
					camera = Camera.main;
				}
				GameObject gameObject = new GameObject("Flatscreen Desktop Camera");
				Object.DontDestroyOnLoad(gameObject);
				_camera = gameObject.AddComponent<Camera>();
				_camera.nearClipPlane = 0.03f;
				_camera.farClipPlane = 2000f;
				_camera.fieldOfView = _targetFieldOfView;
				if (camera != null)
				{
					_camera.clearFlags = camera.clearFlags;
					_camera.backgroundColor = camera.backgroundColor;
					_camera.cullingMask = camera.cullingMask;
					_camera.depth = camera.depth + 1f;
				}
				if (forceDedicatedCamera)
				{
					Camera[] array = Object.FindObjectsOfType<Camera>();
					foreach (Camera camera2 in array)
					{
						if (camera2 != null && camera2 != _camera)
						{
							camera2.enabled = false;
						}
					}
				}
			}
			_camera.enabled = true;
			_camera.tag = "MainCamera";
			ApplyDefaultFieldOfView(_camera);
		}

		private void EnsureMenuCamera()
		{
			EnsureCamera();
			_menuCamera = _camera;
			if (!_menuCameraInitialized)
			{
				_menuCameraPosition = _camera.transform.position;
				_input.SetInitialRotation(_camera.transform.rotation);
				_menuCameraInitialized = true;
			}
		}

		private void UpdatePlayerCamera(Transform playerTransform)
		{
			if (_camera != null)
			{
				UpdateServerCamera(playerTransform, _camera.transform);
			}
		}

		internal void RequestReturnToMenu()
		{
			_pendingReturnToMenu = true;
		}

		internal bool TryToggleBag()
		{
			_hands.TriggerBagAssist(_game, GetViewTransform());
			return true;
		}

		internal bool DropAllHeldItems()
		{
			object obj = _game.FindLocalPlayer();
			_hands.ReleaseAllGrabs();
			if (obj == null)
			{
				_lastStatus = "drop all: no local player";
				return false;
			}
			bool flag = _game.TryReleaseHeldHands(obj);
			_lastStatus = (flag ? "dropped held items" : "no held items to drop");
			return flag;
		}

		internal bool ToggleTerrainRefresh()
		{
			GameObject gameObject = GameObject.Find("MasterTerrain");
			if (gameObject == null)
			{
				_lastStatus = "MasterTerrain not found";
				return false;
			}
			if (!_terrainBasePositionSet)
			{
				_terrainBasePosition = gameObject.transform.position;
				_terrainBasePositionSet = true;
			}
			if (_terrainDown)
			{
				gameObject.transform.position = _terrainBasePosition;
				_terrainDown = false;
				_lastStatus = "terrain restored";
			}
			else
			{
				gameObject.transform.position = _terrainBasePosition + Vector3.down * 50f;
				_terrainDown = true;
				_lastStatus = "terrain lowered";
			}
			return true;
		}

		private void UpdateServerCamera(Transform playerRoot, Transform cameraTransform)
		{
			if (!(cameraTransform == null))
			{
				cameraTransform.localPosition = new Vector3(0f, _heightOffset, 0f);
				Vector3 eulerAngles = _input.CameraRotation.eulerAngles;
				float x = ((eulerAngles.x > 180f) ? (eulerAngles.x - 360f) : eulerAngles.x);
				float y = eulerAngles.y;
				if (playerRoot != null)
				{
					playerRoot.rotation = Quaternion.Euler(0f, y, 0f);
					cameraTransform.localRotation = Quaternion.Euler(x, 0f, 0f);
				}
				else
				{
					cameraTransform.rotation = _input.CameraRotation;
				}
			}
		}

		private void UpdateThirdPersonCamera(Transform playerRoot, Camera sourceCamera)
		{
			if (playerRoot == null || sourceCamera == null)
			{
				DisableThirdPersonCamera();
				return;
			}
			EnsureThirdPersonCamera(sourceCamera);
			if (!(_thirdPersonCamera == null))
			{
				SyncThirdPersonCameraSettings(sourceCamera, _thirdPersonCamera);
				Vector3 vector = playerRoot.position + Vector3.up * (_heightOffset + 0.1f);
				Quaternion cameraRotation = _input.CameraRotation;
				Vector3 vector2 = cameraRotation * new Vector3(0f, _thirdPersonHeight, 0f - _thirdPersonDistance);
				_thirdPersonCamera.transform.position = vector + vector2;
				_thirdPersonCamera.transform.rotation = Quaternion.LookRotation(vector - _thirdPersonCamera.transform.position, Vector3.up);
				_thirdPersonCamera.enabled = true;
				_thirdPersonCamera.tag = "MainCamera";
			}
		}

		private void EnsureThirdPersonCamera(Camera sourceCamera)
		{
			if (!(_thirdPersonCamera != null))
			{
				GameObject gameObject = new GameObject("Flatscreen Third Person Camera");
				Object.DontDestroyOnLoad(gameObject);
				_thirdPersonCamera = gameObject.AddComponent<Camera>();
				SyncThirdPersonCameraSettings(sourceCamera, _thirdPersonCamera);
				_thirdPersonCamera.depth = sourceCamera.depth + 10f;
				_thirdPersonCamera.enabled = true;
			}
		}

		private static void SyncThirdPersonCameraSettings(Camera sourceCamera, Camera targetCamera)
		{
			if (!(sourceCamera == null) && !(targetCamera == null))
			{
				targetCamera.clearFlags = sourceCamera.clearFlags;
				targetCamera.backgroundColor = sourceCamera.backgroundColor;
				targetCamera.cullingMask = sourceCamera.cullingMask;
				targetCamera.nearClipPlane = sourceCamera.nearClipPlane;
				targetCamera.farClipPlane = sourceCamera.farClipPlane;
				targetCamera.fieldOfView = sourceCamera.fieldOfView;
				targetCamera.orthographic = sourceCamera.orthographic;
				targetCamera.orthographicSize = sourceCamera.orthographicSize;
				targetCamera.allowHDR = sourceCamera.allowHDR;
				targetCamera.allowMSAA = sourceCamera.allowMSAA;
			}
		}

		private void DisableThirdPersonCamera()
		{
			if (_thirdPersonCamera != null)
			{
				_thirdPersonCamera.enabled = false;
			}
		}

		private void MovePlayer(object player, object controller, Transform playerTransform)
		{
			Vector3 vector = _input.ReadMoveVector();
			if (!(vector.sqrMagnitude <= 0.0001f))
			{
				float num = (_input.IsRunPressed ? 6f : 3.2f);
				Vector3 vector2 = vector * num * Time.deltaTime;
				vector2.y = 0f;
				if (playerTransform != null)
				{
					Vector3 position = playerTransform.position + vector2;
					position.y = playerTransform.position.y;
					playerTransform.position = position;
					_lastStatus = "server movement direct";
				}
				else if (!_game.TryTranslateWithLocomotion(player, vector2) && !_game.TryTranslateWithLocomotion(controller, vector2) && !_game.TryTranslateWithController(controller, vector2) && !_game.TryTranslateWithController(player, vector2))
				{
					_lastStatus = "server movement unavailable";
				}
			}
		}

		private void ApplyClimbTranslation(object player, object controller, Transform playerTransform)
		{
			Vector3 vector = _hands.ConsumeClimbBodyTranslation();
			if (vector.sqrMagnitude <= 1E-06f)
			{
				return;
			}
			vector = Vector3.ClampMagnitude(vector, 0.2f);
			if (playerTransform != null)
			{
				playerTransform.position += vector;
			}
			else if (!_game.TryTranslateWithLocomotion(player, vector) && !_game.TryTranslateWithLocomotion(controller, vector) && !_game.TryTranslateWithController(controller, vector) && !_game.TryTranslateWithController(player, vector))
			{
				_lastStatus = "climb movement unavailable";
			}
		}

		private void MoveMenuCamera()
		{
			Vector3 vector = _input.ReadMoveVector();
			if (!(vector.sqrMagnitude <= 0.0001f))
			{
				float num = (_input.IsRunPressed ? 6f : 3.2f);
				if (_input.IsMenuFlyUpPressed)
				{
					vector += Vector3.up;
				}
				if (_input.IsMenuFlyDownPressed)
				{
					vector -= Vector3.up;
				}
				_menuCameraPosition += vector * num * Time.deltaTime;
			}
		}

		private void AdjustHeight()
		{
			float num = 0f;
			if (_input.IsHeightUpPressed)
			{
				num += 1f;
			}
			if (_input.IsHeightDownPressed)
			{
				num -= 1f;
			}
			if (Mathf.Abs(num) > 0.001f)
			{
				_heightOffset = Mathf.Clamp(_heightOffset + num * Time.deltaTime * 0.8f, 0f, 2f);
			}
		}

		internal void SetHeightOffset(float value)
		{
			_heightOffset = Mathf.Clamp(value, 0f, 2f);
		}

		internal void ResetHeightOffset()
		{
			_heightOffset = 1.45f;
		}

		internal void SetCameraFieldOfView(float value)
		{
			float fieldOfView = Mathf.Clamp(value, 45f, 110f);
			_targetFieldOfView = fieldOfView;
			_fieldOfViewInitialized = true;
			if (_camera != null)
			{
				_camera.fieldOfView = fieldOfView;
			}
			if (Camera.main != null && Camera.main != _camera)
			{
				Camera.main.fieldOfView = fieldOfView;
			}
		}

		private void ApplyDefaultFieldOfView(Camera camera)
		{
			if (_fieldOfViewInitialized || camera == null)
			{
				return;
			}
			camera.fieldOfView = _targetFieldOfView;
			if (Camera.main != null && Camera.main != camera)
			{
				Camera.main.fieldOfView = _targetFieldOfView;
			}
			_fieldOfViewInitialized = true;
		}

		internal void SetLookSensitivity(float value)
		{
			_input.LookSensitivityMultiplier = value;
		}

		internal void SetThirdPersonEnabled(bool value)
		{
			_thirdPersonEnabled = value;
		}

		internal void ToggleThirdPerson()
		{
			_thirdPersonEnabled = !_thirdPersonEnabled;
			if (!_thirdPersonEnabled)
			{
				DisableThirdPersonCamera();
			}
		}

		internal void SetThirdPersonDistance(float value)
		{
			_thirdPersonDistance = Mathf.Clamp(value, 0.8f, 4.5f);
		}

		private void ApplyCursorState()
		{
			bool showServerBrowser = _showServerBrowser;
			Cursor.lockState = ((_cursorLocked && !showServerBrowser) ? CursorLockMode.Locked : CursorLockMode.None);
			Cursor.visible = !_cursorLocked || showServerBrowser;
		}

		private Camera GetViewCamera()
		{
			if (_thirdPersonEnabled && _thirdPersonCamera != null)
			{
				return _thirdPersonCamera;
			}
			return _camera;
		}

		private Transform GetViewTransform()
		{
			Camera viewCamera = GetViewCamera();
			return (viewCamera != null) ? viewCamera.transform : null;
		}

		private bool ShouldPreserveBlockedHandInteraction()
		{
			string targetInteractableName = _hands.TargetInteractableName;
			if (!string.IsNullOrEmpty(targetInteractableName))
			{
				string text = targetInteractableName.ToLowerInvariant();
				if (text.Contains("revive") || text.Contains("respawn") || text.Contains("death orb"))
				{
					return true;
				}
			}
			string targetParentComponents = _hands.TargetParentComponents;
			if (!string.IsNullOrEmpty(targetParentComponents))
			{
				string text = targetParentComponents.ToLowerInvariant();
				if (text.Contains("reviveorb") || text.Contains("respawnorb") || text.Contains("deathorb"))
				{
					return true;
				}
			}
			return false;
		}
	}
}
