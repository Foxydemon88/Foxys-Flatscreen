using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(FlatscreenATTMod.FlatscreenMod), "Flatscreen ATT Mod", "1.4.0", "Foxydemon88")]
[assembly: MelonGame(null, "A Township Tale")]

namespace FlatscreenATTMod
{
    public sealed class FlatscreenMod : MelonMod
    {
        internal static FlatscreenMod Instance { get; private set; }
        internal static bool SuppressOpenXRInputUpdates;
        private const float DefaultHeightOffset = 0.85f;

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
        private bool _followLoadedAreaAnchor;
        private bool _loggedMissingInput;
        private bool _pendingReturnToMenu;
        private string _lastStatus = "starting";
        private string _lastMode = "none";
        private object _lastPlayer;
        private Transform _lastPlayerTransform;
        private Transform _lastControllerTransform;
        private string _lastPlayerCameraName = "null";
        private string _lastControllerCameraName = "null";
        private object _lastLeftInput;
        private object _lastRightInput;
        private bool _wasMovementBlocked;
        private Vector3 _menuCameraPosition;
        private float _heightOffset = DefaultHeightOffset;
        private bool _thirdPersonEnabled;
        private float _thirdPersonDistance = 2.35f;
        private float _thirdPersonHeight = 0.55f;
        private Vector3 _terrainBasePosition;
        private bool _terrainBasePositionSet;
        private bool _terrainDown;

        public override void OnInitializeMelon()
        {
            Instance = this;
            new HarmonyLib.Harmony("FlatscreenATTMod").PatchAll(typeof(OpenXRInputPatches).Assembly);
            LoggerInstance.Msg("Loaded. F7 opens the control menu, Caps Lock toggles control lock, F10 toggles debug overlay.");
        }

        public override void OnUpdate()
        {
            if (!_input.IsAvailable)
            {
                _lastStatus = "waiting for keyboard/mouse from Unity InputSystem";
                if (!_loggedMissingInput)
                {
                    _loggedMissingInput = true;
                    LoggerInstance.Warning("Unity InputSystem keyboard/mouse devices are not available yet.");
                }

                return;
            }

            if (_input.IsControlTogglePressed)
            {
                _cursorLocked = !_cursorLocked;
                LoggerInstance.Msg("Control lock " + (_cursorLocked ? "enabled" : "disabled") + ".");
            }

            if (_input.IsDebugTogglePressed)
            {
                _showDebug = !_showDebug;
                LoggerInstance.Msg("Debug overlay " + (_showDebug ? "enabled" : "disabled") + ".");
            }

            if (_input.IsServerBrowserTogglePressed)
            {
                _showServerBrowser = !_showServerBrowser;
                LoggerInstance.Msg("Server browser " + (_showServerBrowser ? "enabled" : "disabled") + ".");
            }

            if (_input.IsBagTogglePressed)
            {
                _hands.TriggerBagAssist(_game, GetViewTransform());
                LoggerInstance.Msg("Bag assist triggered.");
            }

            if (_input.IsThirdPersonTogglePressed)
            {
                _thirdPersonEnabled = !_thirdPersonEnabled;
                LoggerInstance.Msg("Third person " + (_thirdPersonEnabled ? "enabled" : "disabled") + ".");
            }

            ApplyCursorState();
        }

        public override void OnLateUpdate()
        {
            if (!_input.IsAvailable)
            {
                SuppressOpenXRInputUpdates = false;
                return;
            }

            if (_pendingReturnToMenu)
            {
                SuppressOpenXRInputUpdates = false;
                _lastStatus = "returning to menu";
                _lastMode = "server";
                _camera = null;

                _game.TryReturnToMainMenu();

                var stillPresent = _game.FindLocalPlayer();
                if (stillPresent == null)
                {
                    _pendingReturnToMenu = false;
                }

                return;
            }

            SuppressOpenXRInputUpdates = false;
            var player = _game.FindLocalPlayer();
            _lastPlayer = player;

            if (player != null)
            {
                UpdateServerPlayer(player);
                return;
            }

            var menuScene = IsMenuLikeScene();
            if (menuScene || LooksLikeCustomizationPlayer())
            {
                UpdateMainMenu();
                return;
            }

            UpdateMainMenu();
        }

        public override void OnGUI()
        {
            if (_showServerBrowser)
            {
                _serverBrowser.Draw(_game, _hands);
            }

            if (!_showDebug)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 560f, 520f), GUI.skin.box);
            GUILayout.Label("Flatscreen ATT Mod 1.4.0");
            GUILayout.Label("By Foxydemon88.");
            GUILayout.Label("Status: " + _lastStatus);
            GUILayout.Label("Mode: " + _lastMode);
            GUILayout.Label("Input available: " + _input.IsAvailable);
            GUILayout.Label("Cursor locked: " + _cursorLocked);
            GUILayout.Label("Player type: " + _game.PlayerTypeName);
            GUILayout.Label("Controller type: " + _game.PlayerControllerTypeName);
            GUILayout.Label("Has Player.Current: " + _game.HasCurrentPlayerProperty);
            GUILayout.Label("Has PlayerController.Current: " + _game.HasCurrentPlayerControllerProperty);
            GUILayout.Label("Player object: " + (_lastPlayer == null ? "null" : _lastPlayer.GetType().FullName));
            GUILayout.Label("Player transform: " + (_lastPlayerTransform == null ? "null" : _lastPlayerTransform.name));
            GUILayout.Label("Controller transform: " + (_lastControllerTransform == null ? "null" : _lastControllerTransform.name));
            GUILayout.Label("Player camera: " + _lastPlayerCameraName);
            GUILayout.Label("Controller camera: " + _lastControllerCameraName);
            GUILayout.Label("Scene scan: " + _game.BuildSceneScanSummary());
            GUILayout.Label("Left input: " + (_lastLeftInput == null ? "null" : _lastLeftInput.GetType().FullName));
            GUILayout.Label("Right input: " + (_lastRightInput == null ? "null" : _lastRightInput.GetType().FullName));
            GUILayout.Label("Hand targets: L " + _game.DescribeInputTarget(_lastLeftInput) + " / R " + _game.DescribeInputTarget(_lastRightInput));
            GUILayout.Label("Locomotion found: " + _game.HasLocomotionController);
            GUILayout.Label("Height offset: " + _heightOffset.ToString("0.00"));
            GUILayout.Label("Target collider: " + _hands.TargetColliderName);
            GUILayout.Label("Target pickup: " + _hands.HasTargetPickup);
            GUILayout.Label("Target interactable: " + _hands.TargetInteractableName + " / " + _hands.HasTargetInteractable);
            GUILayout.Label("Target menu: " + _hands.TargetMenuName);
            GUILayout.Label("Target components: " + _hands.TargetParentComponents);
            GUILayout.Label("Interactor: " + _game.LastInteractorName);
            GUILayout.Label("Interact result: " + _game.LastInteractResult);
            GUILayout.Label("Control menu: " + _showServerBrowser + " / " + _serverBrowser.Count + " / " + _serverBrowser.Status);
            GUILayout.Label("Third person: " + _thirdPersonEnabled + " Distance: " + _thirdPersonDistance.ToString("0.00"));
            GUILayout.Label("Follow loaded area: " + _followLoadedAreaAnchor);
            GUILayout.Label("F7 menu, Caps Lock control lock, I bag, V third person, F10 debug");
            GUILayout.EndArea();
        }

        private void UpdateServerPlayer(object player)
        {
            var controller = _game.FindLocalController();
            var controllerTransform = _game.GetControllerTransform(controller);
            var controllerCamera = _game.GetControllerCamera(controller);
            var playerTransform = _game.GetPlayerTransform(player);
            _lastControllerTransform = controllerTransform;
            _lastControllerCameraName = controllerCamera == null ? "null" : controllerCamera.name;
            var leftInput = _game.GetLeftInput(player);
            var rightInput = _game.GetRightInput(player);
            if (leftInput == null || rightInput == null)
            {
                var looseInputs = _game.FindLoosePlayerInputs();
                if (leftInput == null)
                {
                    leftInput = looseInputs.Left;
                }
                if (rightInput == null)
                {
                    rightInput = looseInputs.Right;
                }
            }

            if (leftInput == null || rightInput == null)
            {
                _lastPlayerTransform = playerTransform;
                _lastLeftInput = leftInput;
                _lastRightInput = rightInput;
                _lastStatus = "server player not ready: waiting for live hand inputs";
                _lastMode = "server";
                _wasMovementBlocked = false;
                DisableThirdPersonCamera();
                return;
            }

            var playerRoot = _game.GetPlayerRootTransform(player);
            if (playerRoot != null && (_game.LooksCosmeticTransform(playerRoot) || LooksLikeScenePlaceholder(playerRoot)))
            {
                playerRoot = null;
            }

            if (controllerTransform != null &&
                !_game.LooksCosmeticTransform(controllerTransform) &&
                !LooksLikeScenePlaceholder(controllerTransform) &&
                LooksLikeLocalPlaceholder(playerRoot))
            {
                playerRoot = controllerTransform;
            }

            if (playerRoot == null && playerTransform != null && !_game.LooksCosmeticTransform(playerTransform) && !LooksLikeScenePlaceholder(playerTransform))
            {
                playerRoot = playerTransform;
            }

            if (playerRoot == null && controllerTransform != null && !_game.LooksCosmeticTransform(controllerTransform) && !LooksLikeScenePlaceholder(controllerTransform))
            {
                playerRoot = controllerTransform;
            }

            var playerCamera = _game.GetPlayerCamera(player);
            _lastPlayerCameraName = playerCamera == null ? "null" : playerCamera.name;
            if ((playerCamera == null || _game.LooksCosmeticTransform(playerCamera.transform) || LooksLikeScenePlaceholder(playerCamera.transform)) &&
                controllerCamera != null &&
                !_game.LooksCosmeticTransform(controllerCamera.transform) &&
                !LooksLikeScenePlaceholder(controllerCamera.transform))
            {
                playerCamera = controllerCamera;
            }

            _lastPlayerTransform = playerRoot != null ? playerRoot : playerTransform;

            SuppressOpenXRInputUpdates = true;
            _followLoadedAreaAnchor = false;

            if (playerRoot == null)
            {
                _lastStatus = "server player using camera/controller fallback";
            }

            AdjustHeight();
            EnsureCamera(true);
            if (_camera != null)
            {
                ConfigureFallbackCamera(_camera);
                if (playerRoot != null)
                {
                    AttachCameraToPlayerRoot(playerRoot, _camera.transform);
                }
                UpdateServerCamera(playerRoot, _camera.transform);
            }

            var viewCamera = GetViewCamera();
            if (_thirdPersonEnabled && playerRoot != null)
            {
                UpdateThirdPersonCamera(playerRoot, _camera);
                viewCamera = _thirdPersonCamera != null ? _thirdPersonCamera : _camera;
                if (_camera != null && _camera != _thirdPersonCamera)
                {
                    _camera.enabled = false;
                }
            }
            else
            {
                DisableThirdPersonCamera();
                if (_camera != null)
                {
                    _camera.enabled = true;
                }
                viewCamera = _camera;
            }

            _input.UpdateLook(_cursorLocked);
            var movementBlocked = _game.IsMovementBlocked(player, controller);
            if (movementBlocked)
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
            _wasMovementBlocked = movementBlocked;
            if (_cursorLocked && !movementBlocked)
            {
                MovePlayer(player, controller, playerRoot, leftInput, rightInput);
            }

            _lastLeftInput = leftInput;
            _lastRightInput = rightInput;
            var handCamera = _camera != null ? _camera : viewCamera;
            var handTransform = handCamera != null ? handCamera.transform : null;
            if (handCamera != null && handTransform != null)
            {
                _hands.UpdateInputs(_lastLeftInput, _lastRightInput, _game, _input, handCamera, handTransform, _cursorLocked);
            }
            if (movementBlocked)
            {
                _lastStatus = "server movement blocked";
            }
            else if (playerRoot == null)
            {
                _lastStatus = _thirdPersonEnabled
                    ? "server fallback: third person needs body root"
                    : (_cursorLocked ? "server fallback: camera and controller" : "server fallback: controls unlocked");
            }
            else
            {
                _lastStatus = _cursorLocked ? "controlling server player" : "server controls unlocked";
            }
            _lastMode = "server";
        }

        private static void ConfigureFallbackCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = -1;
            camera.depth = 100f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 2000f;
            camera.enabled = true;
        }

        private static bool LooksLikeScenePlaceholder(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            var text = transform.name == null ? string.Empty : transform.name.ToLowerInvariant();
            return text == "scene a" ||
                   text == "scene 'a'" ||
                   text.StartsWith("scene '") ||
                   text.StartsWith("scene a");
        }

        private static bool LooksLikeRemoteProxy(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            var text = transform.name == null ? string.Empty : transform.name.ToLowerInvariant();
            return text.Contains("remote") ||
                   text.Contains("proxy") ||
                   text.Contains("ghost");
        }

        private static bool LooksLikeLocalPlaceholder(Transform transform)
        {
            if (transform == null)
            {
                return false;
            }

            var text = transform.name == null ? string.Empty : transform.name.ToLowerInvariant();
            return text.Contains("local") && transform.position.sqrMagnitude < 0.25f;
        }

        private void UpdateMainMenu()
        {
            _lastPlayerTransform = null;

            var inputs = _game.FindLoosePlayerInputs();
            _lastLeftInput = inputs.Left;
            _lastRightInput = inputs.Right;
            _wasMovementBlocked = false;

            if (!inputs.HasAny)
            {
                _lastStatus = "main menu: waiting for PlayerInput hands";
                _lastMode = "menu";
                return;
            }

            if (_camera == null || _camera.name != "Flatscreen Desktop Camera")
            {
                EnsureCamera(true);
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
            _hands.UpdateInputs(inputs.Left, inputs.Right, _game, _input, _camera, _camera.transform, _cursorLocked);
            _lastStatus = _cursorLocked ? "controlling main menu hands" : "menu controls unlocked";
            _lastMode = "menu";
        }

        private void EnsureCamera(bool forceDedicatedCamera = false)
        {
            if (_camera != null && (!forceDedicatedCamera || _camera.name == "Flatscreen Desktop Camera"))
            {
                return;
            }

            Camera source = null;
            if (!forceDedicatedCamera)
            {
                source = Camera.main;
                if (source != null)
                {
                    _camera = source;
                    _camera.enabled = true;
                    _camera.tag = "MainCamera";
                    return;
                }
            }

            if (forceDedicatedCamera && _camera != null && _camera.name != "Flatscreen Desktop Camera")
            {
                _camera.enabled = false;
                source = _camera;
                _camera = null;
            }

            if (_camera == null)
            {
                if (source == null)
                {
                    source = Camera.main;
                }

                var cameraObject = new GameObject("Flatscreen Desktop Camera");
                Object.DontDestroyOnLoad(cameraObject);
                _camera = cameraObject.AddComponent<Camera>();
                _camera.nearClipPlane = 0.03f;
                _camera.farClipPlane = 2000f;
                _camera.fieldOfView = 75f;

                if (source != null)
                {
                    _camera.clearFlags = source.clearFlags;
                    _camera.backgroundColor = source.backgroundColor;
                    _camera.cullingMask = source.cullingMask;
                    _camera.depth = source.depth + 1f;
                }

            }

            _camera.enabled = true;
            _camera.tag = "MainCamera";
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

        private static void AttachCameraToPlayerRoot(Transform playerRoot, Transform cameraTransform)
        {
            if (playerRoot == null || cameraTransform == null)
            {
                return;
            }

            if (cameraTransform.parent != playerRoot)
            {
                cameraTransform.SetParent(playerRoot, false);
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
            var player = _game.FindLocalPlayer();
            _hands.ReleaseAllGrabs();

            if (player == null)
            {
                _lastStatus = "drop all: no local player";
                return false;
            }

            var released = _game.TryReleaseHeldHands(player);
            _lastStatus = released ? "dropped held items" : "no held items to drop";
            return released;
        }

        internal bool ToggleTerrainRefresh()
        {
            var masterTerrain = GameObject.Find("MasterTerrain");
            if (masterTerrain == null)
            {
                _lastStatus = "MasterTerrain not found";
                return false;
            }

            if (!_terrainBasePositionSet)
            {
                _terrainBasePosition = masterTerrain.transform.position;
                _terrainBasePositionSet = true;
            }

            if (_terrainDown)
            {
                masterTerrain.transform.position = _terrainBasePosition;
                _terrainDown = false;
                _lastStatus = "terrain restored";
            }
            else
            {
                masterTerrain.transform.position = _terrainBasePosition + Vector3.down * 50f;
                _terrainDown = true;
                _lastStatus = "terrain lowered";
            }

            return true;
        }

        internal bool RecenterToLoadedArea()
        {
            var anchor = _game.FindLoadedAreaAnchor();
            if (anchor == null)
            {
                _lastStatus = "loaded area anchor not found";
                return false;
            }

            if (_camera == null || _camera.name != "Flatscreen Desktop Camera")
            {
                EnsureCamera(true);
                _menuCameraInitialized = false;
            }

            EnsureMenuCamera();
            ConfigureFallbackCamera(_camera);

            var forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            _menuCameraPosition = anchor.position + Vector3.up * _heightOffset - forward * 1.2f;
            _camera.transform.position = _menuCameraPosition;
            _camera.transform.rotation = _input.CameraRotation;
            _followLoadedAreaAnchor = true;
            _lastStatus = "recentered to loaded area";
            return true;
        }

        internal void NotifyCustomizationTeleporterUsed()
        {
            _followLoadedAreaAnchor = false;
            _lastStatus = "waiting for server handoff";
        }

        private void UpdateServerCamera(Transform playerRoot, Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return;
            }

            cameraTransform.localPosition = new Vector3(0f, _heightOffset, 0f);

            var euler = _input.CameraRotation.eulerAngles;
            var pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            var yaw = euler.y;
            if (playerRoot != null)
            {
                playerRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            else
            {
                cameraTransform.rotation = _input.CameraRotation;
            }
        }

        private void UpdateThirdPersonCamera(Transform playerRoot, Camera sourceCamera)
        {
            if (sourceCamera == null)
            {
                DisableThirdPersonCamera();
                return;
            }

            EnsureThirdPersonCamera(sourceCamera);
            if (_thirdPersonCamera == null)
            {
                return;
            }

            SyncThirdPersonCameraSettings(sourceCamera, _thirdPersonCamera);

            var focus = playerRoot != null
                ? playerRoot.position + Vector3.up * (_heightOffset + 0.1f)
                : sourceCamera.transform.position;
            var rotation = _input.CameraRotation;
            var offset = rotation * new Vector3(0f, _thirdPersonHeight, -_thirdPersonDistance);
            _thirdPersonCamera.transform.position = focus + offset;
            _thirdPersonCamera.transform.rotation = Quaternion.LookRotation(focus - _thirdPersonCamera.transform.position, Vector3.up);
            _thirdPersonCamera.enabled = true;
            _thirdPersonCamera.tag = "MainCamera";
        }

        private void EnsureThirdPersonCamera(Camera sourceCamera)
        {
            if (_thirdPersonCamera != null)
            {
                return;
            }

            var cameraObject = new GameObject("Flatscreen Third Person Camera");
            Object.DontDestroyOnLoad(cameraObject);
            _thirdPersonCamera = cameraObject.AddComponent<Camera>();
            SyncThirdPersonCameraSettings(sourceCamera, _thirdPersonCamera);
            _thirdPersonCamera.depth = sourceCamera.depth + 10f;
            _thirdPersonCamera.enabled = true;
        }

        private static void SyncThirdPersonCameraSettings(Camera sourceCamera, Camera targetCamera)
        {
            if (sourceCamera == null || targetCamera == null)
            {
                return;
            }

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

        private void DisableThirdPersonCamera()
        {
            if (_thirdPersonCamera != null)
            {
                _thirdPersonCamera.enabled = false;
            }
        }

        private void MovePlayer(object player, object controller, Transform playerTransform, object leftInput, object rightInput)
        {
            var move = _input.ReadMoveVector();
            if (move.sqrMagnitude <= 0.0001f)
            {
                _game.TrySetMovementAxis(leftInput, rightInput, Vector2.zero);
                return;
            }

            var axis = new Vector2(Vector3.Dot(move, _input.RightOnPlane), Vector3.Dot(move, _input.ForwardOnPlane));
            var sentAxis = _game.TrySetMovementAxis(leftInput, rightInput, axis);

            var speed = _input.IsRunPressed ? 6.0f : 3.2f;
            var translation = move * speed * Time.deltaTime;
            translation.y = 0f;

            if (playerTransform != null)
            {
                var position = playerTransform.position + translation;
                position.y = playerTransform.position.y;
                playerTransform.position = position;
                _lastStatus = sentAxis ? "server movement input axis + direct" : "server movement direct";
                return;
            }

            if (!_game.TryTranslateWithLocomotion(player, translation) &&
                !_game.TryTranslateWithLocomotion(controller, translation) &&
                !_game.TryTranslateWithController(controller, translation) &&
                !_game.TryTranslateWithController(player, translation))
            {
                _lastStatus = sentAxis ? "server movement input axis only" : "server movement unavailable";
            }
            else
            {
                _lastStatus = sentAxis ? "server movement input axis + locomotion" : "server movement locomotion";
            }
        }

        private void MoveMenuCamera()
        {
            var move = _input.ReadMoveVector();
            if (move.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var speed = _input.IsRunPressed ? 6.0f : 3.2f;
            if (_input.IsMenuFlyUpPressed)
            {
                move += Vector3.up;
            }

            if (_input.IsMenuFlyDownPressed)
            {
                move -= Vector3.up;
            }

            _menuCameraPosition += move * speed * Time.deltaTime;
        }

        private bool FollowLoadedAreaAnchor()
        {
            var controller = _game.FindLocalController();
            var controllerCamera = _game.GetControllerCamera(controller);
            var controllerTransform = _game.GetControllerTransform(controller);
            if (controllerCamera != null)
            {
                _menuCameraPosition = controllerCamera.transform.position;
                _input.SetInitialRotation(controllerCamera.transform.rotation);
                return true;
            }

            if (controllerTransform != null && !LooksLikeScenePlaceholder(controllerTransform) && !LooksLikeRemoteProxy(controllerTransform))
            {
                _menuCameraPosition = controllerTransform.position + Vector3.up * _heightOffset;
                return true;
            }

            var leftTarget = _game.GetInputTargetTransform(_lastLeftInput);
            var rightTarget = _game.GetInputTargetTransform(_lastRightInput);
            if (leftTarget != null || rightTarget != null)
            {
                var center = Vector3.zero;
                var count = 0f;
                if (leftTarget != null)
                {
                    center += leftTarget.position;
                    count += 1f;
                }

                if (rightTarget != null)
                {
                    center += rightTarget.position;
                    count += 1f;
                }

                center /= Mathf.Max(count, 1f);
                var flatRotation = Quaternion.Euler(0f, _input.CameraRotation.eulerAngles.y, 0f);
                _menuCameraPosition = center + Vector3.up * 0.35f - (flatRotation * Vector3.forward * 1.0f);
                return true;
            }

            var anchor = _game.FindLoadedAreaAnchor();
            if (anchor == null)
            {
                _followLoadedAreaAnchor = false;
                return false;
            }

            var forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            _menuCameraPosition = anchor.position + Vector3.up * _heightOffset - forward * 1.2f;
            return true;
        }

        private void AdjustHeight()
        {
            var change = 0f;
            if (_input.IsHeightUpPressed)
            {
                change += 1f;
            }

            if (_input.IsHeightDownPressed)
            {
                change -= 1f;
            }

            if (Mathf.Abs(change) > 0.001f)
            {
                _heightOffset = Mathf.Clamp(_heightOffset + change * Time.deltaTime * 0.8f, 0.0f, 2.0f);
            }
        }

        internal float HeightOffset
        {
            get { return _heightOffset; }
        }

        internal void SetHeightOffset(float value)
        {
            _heightOffset = Mathf.Clamp(value, 0.0f, 2.0f);
        }

        internal void ResetHeightOffset()
        {
            _heightOffset = DefaultHeightOffset;
        }

        internal float CameraFieldOfView
        {
            get
            {
                var activeCamera = _camera != null ? _camera : Camera.main;
                return activeCamera != null ? activeCamera.fieldOfView : 75f;
            }
        }

        internal void SetCameraFieldOfView(float value)
        {
            var clamped = Mathf.Clamp(value, 45f, 110f);

            if (_camera != null)
            {
                _camera.fieldOfView = clamped;
            }

            if (Camera.main != null && Camera.main != _camera)
            {
                Camera.main.fieldOfView = clamped;
            }
        }

        internal bool ThirdPersonEnabled
        {
            get { return _thirdPersonEnabled; }
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

        internal float ThirdPersonDistance
        {
            get { return _thirdPersonDistance; }
        }

        internal void SetThirdPersonDistance(float value)
        {
            _thirdPersonDistance = Mathf.Clamp(value, 0.8f, 4.5f);
        }

        private void ApplyCursorState()
        {
            var shouldUnlock = _showServerBrowser;
            Cursor.lockState = _cursorLocked && !shouldUnlock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_cursorLocked || shouldUnlock;
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
            var viewCamera = GetViewCamera();
            return viewCamera != null ? viewCamera.transform : null;
        }

        private static bool IsMenuLikeScene()
        {
            var scene = SceneManager.GetActiveScene();
            var name = scene.IsValid() ? scene.name : string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            name = name.ToLowerInvariant();
            return name.Contains("menu") ||
                   name.Contains("cosmetic") ||
                   name.Contains("custom") ||
                   name.Contains("character") ||
                   name.Contains("lobby") ||
                   name.Contains("selection");
        }

        private bool LooksLikeCustomizationPlayer()
        {
            var playerName = _lastPlayer == null ? string.Empty : _lastPlayer.GetType().Name;
            var transformName = _lastPlayerTransform == null ? string.Empty : _lastPlayerTransform.name;
            var controllerName = _game.PlayerControllerTypeName;
            var combined = (playerName + " " + transformName + " " + controllerName).ToLowerInvariant();
            return combined.Contains("customization") ||
                   combined.Contains("customise") ||
                   combined.Contains("customize") ||
                   combined.Contains("cosmetic") ||
                   combined.Contains("charactercreator") ||
                   combined.Contains("character creator");
        }

        private bool ShouldPreserveBlockedHandInteraction()
        {
            var targetName = _hands.TargetInteractableName;
            if (!string.IsNullOrEmpty(targetName))
            {
                var text = targetName.ToLowerInvariant();
                if (text.Contains("revive") || text.Contains("respawn") || text.Contains("death orb"))
                {
                    return true;
                }
            }

            var parentComponents = _hands.TargetParentComponents;
            if (!string.IsNullOrEmpty(parentComponents))
            {
                var text = parentComponents.ToLowerInvariant();
                if (text.Contains("reviveorb") || text.Contains("respawnorb") || text.Contains("deathorb"))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
