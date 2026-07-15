using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
    internal sealed class ServerBrowserOverlay
    {
        private enum MenuTab
        {
            Servers = 0,
            Hands = 1,
            Camera = 2,
            Actions = 3,
            System = 4
        }

        private enum MenuSubTab
        {
            Camera = 0,
            Names = 1,
            Wheel = 2
        }

        private enum HandTab
        {
            Left = 0,
            Right = 1
        }

        private readonly List<object> _servers = new List<object>();
        private Vector2 _serverScroll;
        private MenuTab _menuTab = MenuTab.Servers;
        private MenuSubTab _menuSubTab = MenuSubTab.Camera;
        private HandTab _handTab = HandTab.Left;
        private int _selectedIndex = -1;
        private bool _showNamesState = true;
        private string _status = "Not refreshed yet";

        public string Status { get { return _status; } }
        public int Count { get { return _servers.Count; } }

        public void Draw(GameReflection game, HandEmulator hands)
        {
            GUILayout.BeginArea(new Rect(18f, 12f, 900f, 720f), GUI.skin.box);
            GUILayout.Label("Flatscreen Control Menu");
            GUILayout.Label("Status: " + _status);

            GUILayout.BeginHorizontal();
            if (TabButton("Servers", _menuTab == MenuTab.Servers, 132f))
            {
                _menuTab = MenuTab.Servers;
            }

            if (TabButton("Hands", _menuTab == MenuTab.Hands, 132f))
            {
                _menuTab = MenuTab.Hands;
            }

            if (TabButton("Camera", _menuTab == MenuTab.Camera, 132f))
            {
                _menuTab = MenuTab.Camera;
            }

            if (TabButton("Actions", _menuTab == MenuTab.Actions, 132f))
            {
                _menuTab = MenuTab.Actions;
            }

            if (TabButton("System", _menuTab == MenuTab.System, 132f))
            {
                _menuTab = MenuTab.System;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);

            switch (_menuTab)
            {
                case MenuTab.Servers:
                    DrawServers();
                    break;
                case MenuTab.Hands:
                    DrawHands(hands);
                    break;
                case MenuTab.Camera:
                    DrawMenu(game);
                    break;
                case MenuTab.Actions:
                    DrawQuickAccess(game);
                    break;
                case MenuTab.System:
                    DrawSystem(game);
                    break;
            }

            GUILayout.Space(6f);
            GUILayout.Label("F7 toggles this menu. Caps Lock toggles control lock. F10 debug.");
            GUILayout.EndArea();
        }

        public void Refresh()
        {
            _servers.Clear();
            _selectedIndex = -1;

            var menu = FindMonoBehaviour("ServerSelectionMenu");
            if (menu == null)
            {
                _status = "ServerSelectionMenu not found";
                return;
            }

            InvokeNoThrow(menu, "RefreshServersList");

            var boards = GetFieldOrProperty(menu, "boards") as IEnumerable;
            if (boards != null)
            {
                foreach (var board in boards)
                {
                    AddServersFromBoard(board);
                }
            }

            var currentBoard = GetFieldOrProperty(menu, "CurrentBoard");
            AddServersFromBoard(currentBoard);

            _status = _servers.Count == 0 ? "No servers found yet. Wait a moment and refresh." : "Loaded " + _servers.Count + " servers";
        }

        private void DrawServers()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
            {
                Refresh();
            }

            if (GUILayout.Button("Join Selected", GUILayout.Width(140f)))
            {
                JoinSelected();
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("Servers: " + _servers.Count);
            _serverScroll = GUILayout.BeginScrollView(_serverScroll, GUILayout.Height(520f));
            for (var i = 0; i < _servers.Count; i++)
            {
                var server = _servers[i];
                var selected = i == _selectedIndex;
                var label = (selected ? "> " : "  ") + GetText(server, "Name", "Unnamed Server");

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(label, GUILayout.Width(420f)))
                {
                    _selectedIndex = i;
                }

                if (GUILayout.Button("Join", GUILayout.Width(90f)))
                {
                    _selectedIndex = i;
                    JoinSelected();
                }
                GUILayout.EndHorizontal();

                var description = GetText(server, "Description", string.Empty);
                if (!string.IsNullOrEmpty(description))
                {
                    GUILayout.Label(description);
                }

                GUILayout.EndVertical();
            }
            GUILayout.EndScrollView();
        }

        private void DrawHands(HandEmulator hands)
        {
            if (hands == null)
            {
                GUILayout.Label("Hand controller not available.");
                return;
            }

            GUILayout.BeginHorizontal();
            if (TabButton("Left Hand", _handTab == HandTab.Left))
            {
                _handTab = HandTab.Left;
            }

            if (TabButton("Right Hand", _handTab == HandTab.Right))
            {
                _handTab = HandTab.Right;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label(_handTab == HandTab.Left ? "Left hand" : "Right hand");
            GUILayout.Label(new GUIContent("Q / E toggles hands", "Q toggles left hand. E toggles right hand. Both can stay active."));
            GUILayout.Label(new GUIContent("Left click / Right click", "Grab with the selected hand."));
            GUILayout.Label(new GUIContent("Mouse wheel", "Move the selected hand forward or back."));

            var horizontal = _handTab == HandTab.Left ? hands.LeftHorizontalAngle : hands.RightHorizontalAngle;
            var vertical = _handTab == HandTab.Left ? hands.LeftVerticalAngle : hands.RightVerticalAngle;
            var depth = _handTab == HandTab.Left ? hands.LeftDepth : hands.RightDepth;

            GUILayout.Label("Horizontal");
            horizontal = GUILayout.HorizontalSlider(horizontal, -90f, 90f, GUILayout.Width(500f));

            GUILayout.Label("Vertical");
            vertical = GUILayout.HorizontalSlider(vertical, -80f, 80f, GUILayout.Width(500f));

            GUILayout.Label("Depth");
            depth = GUILayout.HorizontalSlider(depth, 0.2f, 1.6f, GUILayout.Width(500f));

            if (_handTab == HandTab.Left)
            {
                hands.SetLeftHorizontalAngle(horizontal);
                hands.SetLeftVerticalAngle(vertical);
                hands.SetLeftDepth(depth);
            }
            else
            {
                hands.SetRightHorizontalAngle(horizontal);
                hands.SetRightVerticalAngle(vertical);
                hands.SetRightDepth(depth);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Selected Pose", GUILayout.Width(160f)))
            {
                if (_handTab == HandTab.Left)
                {
                    hands.ResetLeftPose();
                }
                else
                {
                    hands.ResetRightPose();
                }
            }

            if (GUILayout.Button("Reset All Hands", GUILayout.Width(150f)))
            {
                hands.ResetHands();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSystem(GameReflection game)
        {
            GUILayout.Label("System actions");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Disconnect", GUILayout.Width(140f)))
            {
                FlatscreenMod.SuppressOpenXRInputUpdates = false;
                _status = "returning to menu";
                if (FlatscreenMod.Instance != null)
                {
                    FlatscreenMod.Instance.RequestReturnToMenu();
                }
            }

            if (GUILayout.Button("Drop All Held", GUILayout.Width(130f)))
            {
                if (FlatscreenMod.Instance == null || !FlatscreenMod.Instance.DropAllHeldItems())
                {
                    _status = FlatscreenMod.Instance == null ? "mod unavailable" : "no held items to drop";
                }
                else
                {
                    _status = "dropped held items";
                }
            }

            if (GUILayout.Button("Quit Game", GUILayout.Width(120f)))
            {
                Application.Quit();
                _status = "Quit requested";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            if (GUILayout.Button("Refresh Terrain", GUILayout.Width(160f)))
            {
                if (FlatscreenMod.Instance != null && !FlatscreenMod.Instance.ToggleTerrainRefresh())
                {
                    _status = "MasterTerrain not found";
                }
            }

            if (GUILayout.Button("Leave Customization", GUILayout.Width(180f)))
            {
                if (!game.TryLeaveCustomizationArea())
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    if (FlatscreenMod.Instance != null)
                    {
                        FlatscreenMod.Instance.NotifyCustomizationTeleporterUsed();
                    }
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Use Customization Teleporter", GUILayout.Width(220f)))
            {
                if (!game.TryActivateCustomizationTeleporter())
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    if (FlatscreenMod.Instance != null)
                    {
                        FlatscreenMod.Instance.NotifyCustomizationTeleporterUsed();
                    }
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Recenter To Loaded Area", GUILayout.Width(210f)))
            {
                if (FlatscreenMod.Instance == null || !FlatscreenMod.Instance.RecenterToLoadedArea())
                {
                    _status = FlatscreenMod.Instance == null ? "mod unavailable" : "loaded area anchor not found";
                }
                else
                {
                    _status = "recentered to loaded area";
                }
            }

            if (GUILayout.Button("Refresh Server List", GUILayout.Width(180f)))
            {
                Refresh();
            }
        }

        private void DrawMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal();
            if (TabButton("Camera", _menuSubTab == MenuSubTab.Camera))
            {
                _menuSubTab = MenuSubTab.Camera;
            }

            if (TabButton("Names", _menuSubTab == MenuSubTab.Names))
            {
                _menuSubTab = MenuSubTab.Names;
            }

            if (TabButton("Wheel", _menuSubTab == MenuSubTab.Wheel))
            {
                _menuSubTab = MenuSubTab.Wheel;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            switch (_menuSubTab)
            {
                case MenuSubTab.Camera:
                    DrawCameraMenu(game);
                    break;
                case MenuSubTab.Names:
                    DrawNameMenu(game);
                    break;
                case MenuSubTab.Wheel:
                    DrawWheelMenu(game);
                    break;
            }
        }

        private void DrawCameraMenu(GameReflection game)
        {
            GUILayout.Label("Camera");
            if (FlatscreenMod.Instance != null)
            {
                var fov = FlatscreenMod.Instance.CameraFieldOfView;
                GUILayout.Label("FOV");
                var updatedFov = GUILayout.HorizontalSlider(fov, 45f, 110f, GUILayout.Width(320f));
                if (Mathf.Abs(updatedFov - fov) > 0.01f)
                {
                    FlatscreenMod.Instance.SetCameraFieldOfView(updatedFov);
                    _status = "FOV " + updatedFov.ToString("0");
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Screenshot Camera", GUILayout.Width(150f)))
            {
                if (!game.TryRunQuickAccessAction(
                    new[] { "SummonCameraQuickAccess", "ScreenshotCameraQuickAccess", "CameraQuickAccess" },
                    new[] { "Summon Camera Action", "Screenshot Camera", "Screenshot" },
                    new[] { "Open" },
                    new[] { "Run" }))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Enable Filters", GUILayout.Width(130f)))
            {
                if (!game.TryRunQuickAccessAction(
                    new[] { "CameraFilter", "ScreenshotCameraQuickAccess", "PhotoFilter", "Filter" },
                    new[] { "Enable Filters", "Filters", "Filter" },
                    new[] { "Open", "Activate" },
                    new[] { "Run", "Apply", "Enable", "Toggle" }))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("View");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(FlatscreenMod.Instance != null && FlatscreenMod.Instance.ThirdPersonEnabled ? "Third Person On" : "Third Person Off", GUILayout.Width(150f)))
            {
                if (FlatscreenMod.Instance != null)
                {
                    FlatscreenMod.Instance.ToggleThirdPerson();
                    _status = FlatscreenMod.Instance.ThirdPersonEnabled ? "Third person enabled" : "Third person disabled";
                }
            }

            if (GUILayout.Button("Open Bag", GUILayout.Width(110f)))
            {
                if (FlatscreenMod.Instance == null || !FlatscreenMod.Instance.TryToggleBag())
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = "bag assist triggered";
                }
            }
            GUILayout.EndHorizontal();

            if (FlatscreenMod.Instance != null)
            {
                var distance = FlatscreenMod.Instance.ThirdPersonDistance;
                GUILayout.Label(new GUIContent("Third-person distance", "V toggles third person. I opens the bag."));
                var updatedDistance = GUILayout.HorizontalSlider(distance, 0.8f, 4.5f, GUILayout.Width(320f));
                if (Mathf.Abs(updatedDistance - distance) > 0.01f)
                {
                    FlatscreenMod.Instance.SetThirdPersonDistance(updatedDistance);
                    _status = "Third-person distance " + updatedDistance.ToString("0.0");
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Height", GUILayout.Width(120f)))
            {
                if (FlatscreenMod.Instance != null)
                {
                    FlatscreenMod.Instance.ResetHeightOffset();
                    _status = "Height reset";
                }
            }

            if (GUILayout.Button("Lower", GUILayout.Width(80f)))
            {
                if (FlatscreenMod.Instance != null)
                {
                    FlatscreenMod.Instance.SetHeightOffset(FlatscreenMod.Instance.HeightOffset - 0.15f);
                    _status = "Height lowered";
                }
            }

            if (GUILayout.Button("Raise", GUILayout.Width(80f)))
            {
                if (FlatscreenMod.Instance != null)
                {
                    FlatscreenMod.Instance.SetHeightOffset(FlatscreenMod.Instance.HeightOffset + 0.15f);
                    _status = "Height raised";
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawQuickAccess(GameReflection game)
        {
            GUILayout.Label("Actions");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showNamesState ? "Hide Names" : "Show Names", GUILayout.Width(120f)))
            {
                _showNamesState = !_showNamesState;
                if (!game.TrySetShowNames(_showNamesState))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawNameMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle Usernames", GUILayout.Width(150f)))
            {
                if (!game.TryToggleSceneBoolean(new[] { "Nameplate", "NameTag", "PlayerName", "PlayerNameplate", "NameDisplay" }, new[] { "ShowNames", "ShowUsernames", "ShowNameplates", "DisplayNames", "DisplayUsernames" }))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Toggle Name UI", GUILayout.Width(130f)))
            {
                if (!game.TryInvokeSceneMethod(new[] { "Nameplate", "NameTag", "PlayerName", "PlayerNameplate", "NameDisplay" }, new[] { "Toggle", "Show", "Hide", "SetVisible" }))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawWheelMenu(GameReflection game)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Wheel Left", GUILayout.Width(100f)))
            {
                if (!_TryWheelScroll(game, -1f))
                {
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Wheel Right", GUILayout.Width(100f)))
            {
                if (!_TryWheelScroll(game, 1f))
                {
                    _status = game.LastInteractResult;
                }
            }

            if (GUILayout.Button("Reset Wheel", GUILayout.Width(120f)))
            {
                if (!game.TryInvokeSceneMethod(new[] { "CaptainsWheel", "WheelGrab", "ServerSelectionMenu", "VrMainMenu" }, new[] { "Reset", "Recenter", "ResetWheel", "ResetPosition", "Home" }))
                {
                    _status = game.LastInteractResult;
                }
                else
                {
                    _status = game.LastInteractResult;
                }
            }
            GUILayout.EndHorizontal();
        }

        private bool _TryWheelScroll(GameReflection game, float direction)
        {
            var target = FindMonoBehaviour("CaptainsWheel");
            if (target != null && game.TryAdjustMenuWheel(target, direction))
            {
                _status = game.LastInteractResult;
                return true;
            }

            target = FindMonoBehaviour("WheelGrab");
            if (target != null && game.TryAdjustMenuWheel(target, direction))
            {
                _status = game.LastInteractResult;
                return true;
            }

            target = FindMonoBehaviour("ServerSelectionMenu");
            if (target != null && game.TryAdjustMenuWheel(target, direction))
            {
                _status = game.LastInteractResult;
                return true;
            }

            _status = "wheel target not found";
            return false;
        }

        private void JoinSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _servers.Count)
            {
                _status = "Select a server first";
                return;
            }

            var mainMenu = FindMonoBehaviour("VrMainMenu");
            if (mainMenu == null)
            {
                _status = "VrMainMenu not found";
                return;
            }

            var joinMethod = mainMenu.GetType().GetMethod("JoinServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (joinMethod == null)
            {
                _status = "JoinServer method not found";
                return;
            }

            try
            {
                joinMethod.Invoke(mainMenu, new[] { _servers[_selectedIndex] });
                _status = "Joining " + GetText(_servers[_selectedIndex], "Name", "server");
            }
            catch (Exception ex)
            {
                _status = "Join failed: " + ex.GetType().Name;
            }
        }

        private void AddServersFromBoard(object board)
        {
            if (board == null)
            {
                return;
            }

            AddServerEnumerable(GetFieldOrProperty(board, "lastFilteredServers") as IEnumerable);
            AddServerEnumerable(GetFieldOrProperty(board, "lastReceivedServers") as IEnumerable);
        }

        private void AddServerEnumerable(IEnumerable enumerable)
        {
            if (enumerable == null)
            {
                return;
            }

            foreach (var server in enumerable)
            {
                if (server != null && !_servers.Contains(server))
                {
                    _servers.Add(server);
                }
            }
        }

        private static bool TabButton(string label, bool active)
        {
            return TabButton(label, active, 120f);
        }

        private static bool TabButton(string label, bool active, float width)
        {
            var options = active ? new[] { GUILayout.Width(width + 10f) } : new[] { GUILayout.Width(width) };
            return GUILayout.Button(active ? "[ " + label + " ]" : label, options);
        }

        private static MonoBehaviour FindMonoBehaviour(string typeName)
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == typeName)
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static object GetFieldOrProperty(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    return field.GetValue(instance);
                }
                catch
                {
                }
            }

            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(instance, null);
                }
                catch
                {
                }
            }

            return null;
        }

        private static string GetText(object instance, string memberName, string fallback)
        {
            var value = GetFieldOrProperty(instance, memberName);
            return value == null ? fallback : value.ToString();
        }

        private static void InvokeNoThrow(object instance, string methodName)
        {
            if (instance == null)
            {
                return;
            }

            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
            {
                return;
            }

            try
            {
                method.Invoke(instance, null);
            }
            catch
            {
            }
        }
    }
}
