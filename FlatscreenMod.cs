using MelonLoader;
using HarmonyLib;
using UnityEngine;

[assembly: MelonInfo(typeof(FlatscreenATTMod.FlatscreenMod), "Flatscreen ATT Mod", "1.4.0", "Keyon + Codex")]
[assembly: MelonGame(null, "A Township Tale")]

namespace FlatscreenATTMod
{
    public sealed class FlatscreenMod : MelonMod
    {
        internal static FlatscreenMod Instance { get; private set; }
        internal static bool SuppressOpenXRInputUpdates;

        private readonly DesktopInput _input = new DesktopInput();
        private readonly GameReflection _game = new GameReflection();
        private readonly HandEmulator _hands = new HandEmulator();
        private readonly ServerBrowserOverlay _serverBrowser = new ServerBrowserOverlay();

        private Camera _camera;
        private bool _cursorLocked = true;
        private bool _showDebug;
        private bool _showServerBrowser = true;
        private bool _menuCameraInitialized;
        private bool _loggedMissingInput;
        private bool _pendingReturnToMenu;
        private string _lastStatus = "starting";
        private string _lastMode = "none";
        private object _lastPlayer;
        private Transform _lastPlayerTransform;
        private object _lastLeftInput;
        private object _lastRightInput;
        private Vector3 _menuCameraPosition;
        private float _heightOffset = 1.15f;
        private bool _serverCameraAttached;
        private Vector3 _serverCameraBaseLocalPosition;

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

            ApplyCursorState();
        }

        public override void OnLateUpdate()
        {
            SuppressOpenXRInputUpdates = true;

            if (!_input.IsAvailable)
            {
                SuppressOpenXRInputUpdates = false;
                return;
            }

            if (_pendingReturnToMenu)
            {
                _lastStatus = "returning to menu";
                _lastMode = "server";
                _camera = null;
                _serverCameraAttached = false;
                _serverCameraBaseLocalPosition = Vector3.zero;

                _game.TryReturnToMainMenu();

                var stillPresent = _game.FindLocalPlayer();
                if (stillPresent == null)
                {
                    _pendingReturnToMenu = false;
                }

                return;
            }

            var player = _game.FindLocalPlayer();
            _lastPlayer = player;

            if (player != null)
            {
                UpdateServerPlayer(player);
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
            GUILayout.Label("SteamVR/Alta launcher note: menu uses free-fly, server mode stays horizontal.");
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
            GUILayout.Label("Left input: " + (_lastLeftInput == null ? "null" : _lastLeftInput.GetType().FullName));
            GUILayout.Label("Right input: " + (_lastRightInput == null ? "null" : _lastRightInput.GetType().FullName));
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
            GUILayout.Label("F7 menu, Caps Lock control lock, F10 debug");
            GUILayout.EndArea();
        }

        private void UpdateServerPlayer(object player)
        {
            var controller = _game.FindLocalController();
            var controllerTransform = _game.GetControllerTransform(controller);
            var controllerCamera = _game.GetControllerCamera(controller);
            var playerTransform = _game.GetPlayerTransform(player);
            var playerRoot = controllerTransform != null ? controllerTransform : _game.GetPlayerRootTransform(player);
            var playerCamera = controllerCamera != null ? controllerCamera : _game.GetPlayerCamera(player);
            _lastPlayerTransform = playerRoot != null ? playerRoot : playerTransform;
            if (playerRoot == null)
            {
                _lastStatus = "server player found, but no root transform";
                _lastMode = "server";
                return;
            }

            EnsureCamera(false);
            if (playerCamera != null)
            {
                _camera = playerCamera;
                _camera.enabled = true;
                _camera.tag = "MainCamera";
                UpdateServerCamera(playerRoot);
            }
            else
            {
                EnsureFallbackServerCamera();
            }

            AdjustHeight();
            _input.UpdateLook(_cursorLocked);
            if (_cursorLocked)
            {
                MovePlayer(controller != null ? controller : player, playerRoot);
            }
            _lastLeftInput = _game.GetLeftInput(player);
            _lastRightInput = _game.GetRightInput(player);
            _hands.UpdateInputs(_lastLeftInput, _lastRightInput, _game, _input, _camera.transform);
            _lastStatus = _cursorLocked ? "controlling server player" : "server controls unlocked";
            _lastMode = "server";
        }

        private void UpdateMainMenu()
        {
            _lastPlayerTransform = null;

            var inputs = _game.FindLoosePlayerInputs();
            _lastLeftInput = inputs.Left;
            _lastRightInput = inputs.Right;

            if (!inputs.HasAny)
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
            _hands.UpdateInputs(inputs.Left, inputs.Right, _game, _input, _camera.transform);
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

                if (forceDedicatedCamera)
                {
                    foreach (var otherCamera in Object.FindObjectsOfType<Camera>())
                    {
                        if (otherCamera != null && otherCamera != _camera)
                        {
                            otherCamera.enabled = false;
                        }
                    }
                }
            }

            _camera.enabled = true;
            _camera.tag = "MainCamera";
        }

        private void EnsureFallbackServerCamera()
        {
            if (_camera != null && _camera.name == "Flatscreen Desktop Camera")
            {
                return;
            }

            var cameraObject = new GameObject("Flatscreen Desktop Camera");
            Object.DontDestroyOnLoad(cameraObject);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.03f;
            _camera.farClipPlane = 2000f;
            _camera.fieldOfView = 75f;
            _camera.enabled = true;
            _camera.tag = "MainCamera";
        }

        private void EnsureMenuCamera()
        {
            EnsureCamera();

            if (!_menuCameraInitialized)
            {
                _menuCameraPosition = _camera.transform.position;
                _input.SetInitialRotation(_camera.transform.rotation);
                _menuCameraInitialized = true;
            }
        }

        private void UpdatePlayerCamera(Transform playerTransform)
        {
            UpdateServerCamera(playerTransform);
        }

        internal void RequestReturnToMenu()
        {
            _pendingReturnToMenu = true;
        }

        private void UpdateServerCamera(Transform playerTransform)
        {
            var cameraTransform = _camera.transform;
            if (playerTransform != null && cameraTransform.parent != playerTransform)
            {
                cameraTransform.SetParent(playerTransform, true);
                _serverCameraBaseLocalPosition = cameraTransform.localPosition;
                _serverCameraAttached = true;
            }

            if (!_serverCameraAttached)
            {
                _serverCameraBaseLocalPosition = cameraTransform.localPosition;
                _serverCameraAttached = true;
            }

            var localPosition = _serverCameraBaseLocalPosition;
            localPosition.y += _heightOffset;
            cameraTransform.localPosition = localPosition;

            var euler = _input.CameraRotation.eulerAngles;
            var pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            var yaw = euler.y;
            if (playerTransform != null)
            {
                playerTransform.rotation = Quaternion.Euler(0f, yaw, 0f);
                cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            else
            {
                cameraTransform.rotation = _input.CameraRotation;
            }
        }

        private void MovePlayer(object player, Transform playerTransform)
        {
            var move = _input.ReadMoveVector();
            if (move.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var speed = _input.IsRunPressed ? 6.0f : 3.2f;
            var translation = move * speed * Time.deltaTime;
            translation.y = 0f;

            if (!_game.TryTranslateWithController(player, translation) && !_game.TryTranslateWithLocomotion(player, translation))
            {
                var position = playerTransform.position + translation;
                position.y = playerTransform.position.y;
                playerTransform.position = position;
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
            _heightOffset = 1.15f;
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

        private void ApplyCursorState()
        {
            var shouldUnlock = _showServerBrowser;
            Cursor.lockState = _cursorLocked && !shouldUnlock ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_cursorLocked || shouldUnlock;
        }
    }
}
