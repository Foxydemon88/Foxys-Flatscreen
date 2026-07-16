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
			Servers,
			Hands,
			Camera,
			Actions,
			System
		}

		private enum MenuSubTab
		{
			Camera,
			Names,
			Wheel
		}

		private enum HandTab
		{
			Left,
			Right
		}

		private readonly List<object> _servers = new List<object>();

		private Vector2 _serverScroll;

		private MenuTab _menuTab = MenuTab.Servers;

		private MenuSubTab _menuSubTab = MenuSubTab.Camera;

		private HandTab _handTab = HandTab.Left;

		private int _selectedIndex = -1;

		private bool _showNamesState = true;

		private string _status = "Not refreshed yet";

		public string Status
		{
			get
			{
				return _status;
			}
		}

		public int Count
		{
			get
			{
				return _servers.Count;
			}
		}

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
			MonoBehaviour monoBehaviour = FindMonoBehaviour("ServerSelectionMenu");
			if (monoBehaviour == null)
			{
				_status = "ServerSelectionMenu not found";
				return;
			}
			InvokeNoThrow(monoBehaviour, "RefreshServersList");
			IEnumerable enumerable = GetFieldOrProperty(monoBehaviour, "boards") as IEnumerable;
			if (enumerable != null)
			{
				foreach (object item in enumerable)
				{
					AddServersFromBoard(item);
				}
			}
			object fieldOrProperty = GetFieldOrProperty(monoBehaviour, "CurrentBoard");
			AddServersFromBoard(fieldOrProperty);
			_status = ((_servers.Count == 0) ? "No servers found yet. Wait a moment and refresh." : ("Loaded " + _servers.Count + " servers"));
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
			for (int i = 0; i < _servers.Count; i++)
			{
				object instance = _servers[i];
				string text = ((i == _selectedIndex) ? "> " : "  ") + GetText(instance, "Name", "Unnamed Server");
				GUILayout.BeginVertical(GUI.skin.box);
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(text, GUILayout.Width(420f)))
				{
					_selectedIndex = i;
				}
				if (GUILayout.Button("Join", GUILayout.Width(90f)))
				{
					_selectedIndex = i;
					JoinSelected();
				}
				GUILayout.EndHorizontal();
				string text2 = GetText(instance, "Description", string.Empty);
				if (!string.IsNullOrEmpty(text2))
				{
					GUILayout.Label(text2);
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
			hands.ClimbingModeEnabled = GUILayout.Toggle(hands.ClimbingModeEnabled, "Climbing Mode");
			GUILayout.Label("Climb sensitivity: " + hands.ClimbSensitivity.ToString("0.00"));
			hands.ClimbSensitivity = GUILayout.HorizontalSlider(hands.ClimbSensitivity, 0.25f, 3f, GUILayout.Width(500f));
			GUILayout.Label("Climb: hold left/right click, then A/D moves the held hand sideways. W/S still walks forward/back.");
			GUILayout.Space(6f);
			GUILayout.Label((_handTab == HandTab.Left) ? "Left hand" : "Right hand");
			GUILayout.Label(new GUIContent("Q / E toggles hands", "Q toggles left hand. E toggles right hand. Both can stay active."));
			GUILayout.Label(new GUIContent("Left click / Right click", "Grab with the selected hand."));
			GUILayout.Label(new GUIContent("Mouse wheel", "Move the selected hand forward or back."));
			float value = ((_handTab == HandTab.Left) ? hands.LeftHorizontalAngle : hands.RightHorizontalAngle);
			float value2 = ((_handTab == HandTab.Left) ? hands.LeftVerticalAngle : hands.RightVerticalAngle);
			float value3 = ((_handTab == HandTab.Left) ? hands.LeftDepth : hands.RightDepth);
			float value4 = ((_handTab == HandTab.Left) ? hands.LeftRollAngle : hands.RightRollAngle);
			GUILayout.Label("Horizontal");
			value = GUILayout.HorizontalSlider(value, -180f, 180f, GUILayout.Width(500f));
			GUILayout.Label("Vertical");
			value2 = GUILayout.HorizontalSlider(value2, -180f, 180f, GUILayout.Width(500f));
			GUILayout.Label("Roll");
			value4 = GUILayout.HorizontalSlider(value4, -180f, 180f, GUILayout.Width(500f));
			GUILayout.Label("Depth");
			value3 = GUILayout.HorizontalSlider(value3, 0.2f, 1.6f, GUILayout.Width(500f));
			if (_handTab == HandTab.Left)
			{
				hands.SetLeftHorizontalAngle(value);
				hands.SetLeftVerticalAngle(value2);
				hands.SetLeftRollAngle(value4);
				hands.SetLeftDepth(value3);
			}
			else
			{
				hands.SetRightHorizontalAngle(value);
				hands.SetRightVerticalAngle(value2);
				hands.SetRightRollAngle(value4);
				hands.SetRightDepth(value3);
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Palm Down", GUILayout.Width(120f)))
			{
				if (_handTab == HandTab.Left)
				{
					hands.SetLeftPalmDownPose();
				}
				else
				{
					hands.SetRightPalmDownPose();
				}
			}
			if (GUILayout.Button("Both Palm Down", GUILayout.Width(140f)))
			{
				hands.SetBothPalmDownPose();
			}
			if (GUILayout.Button("Handshake", GUILayout.Width(120f)))
			{
				hands.SetBothHandshakePose();
			}
			if (GUILayout.Button("Tips Down In", GUILayout.Width(130f)))
			{
				hands.SetBothFingertipsDownInPose();
			}
			GUILayout.EndHorizontal();
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
					_status = ((FlatscreenMod.Instance == null) ? "mod unavailable" : "no held items to drop");
				}
				else
				{
					_status = "dropped held items";
				}
			}
			if (GUILayout.Button("Escape Customization", GUILayout.Width(180f)))
			{
				if (game != null && game.TryEscapeCustomization())
				{
					_status = game.LastInteractResult;
				}
				else
				{
					_status = (game == null) ? "game reflection unavailable" : game.LastInteractResult;
				}
			}
			if (GUILayout.Button("Quit Game", GUILayout.Width(120f)))
			{
				Application.Quit();
				_status = "Quit requested";
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(8f);
			if (GUILayout.Button("Refresh Terrain", GUILayout.Width(160f)) && FlatscreenMod.Instance != null && !FlatscreenMod.Instance.ToggleTerrainRefresh())
			{
				_status = "MasterTerrain not found";
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
				float cameraFieldOfView = FlatscreenMod.Instance.CameraFieldOfView;
				GUILayout.Label("FOV");
				float num = GUILayout.HorizontalSlider(cameraFieldOfView, 45f, 110f, GUILayout.Width(320f));
				if (Mathf.Abs(num - cameraFieldOfView) > 0.01f)
				{
					FlatscreenMod.Instance.SetCameraFieldOfView(num);
					_status = "FOV " + num.ToString("0");
				}
				float lookSensitivity = FlatscreenMod.Instance.LookSensitivity;
				GUILayout.Label("Look sensitivity: " + lookSensitivity.ToString("0.00"));
				float num2 = GUILayout.HorizontalSlider(lookSensitivity, 0.25f, 3f, GUILayout.Width(320f));
				if (Mathf.Abs(num2 - lookSensitivity) > 0.01f)
				{
					FlatscreenMod.Instance.SetLookSensitivity(num2);
					_status = "Look sensitivity " + num2.ToString("0.00");
				}
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Screenshot Camera", GUILayout.Width(150f)))
			{
				if (!game.TryRunQuickAccessAction(new string[3] { "SummonCameraQuickAccess", "ScreenshotCameraQuickAccess", "CameraQuickAccess" }, new string[3] { "Summon Camera Action", "Screenshot Camera", "Screenshot" }, new string[1] { "Open" }, new string[1] { "Run" }))
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
				if (!game.TryRunQuickAccessAction(new string[4] { "CameraFilter", "ScreenshotCameraQuickAccess", "PhotoFilter", "Filter" }, new string[3] { "Enable Filters", "Filters", "Filter" }, new string[2] { "Open", "Activate" }, new string[4] { "Run", "Apply", "Enable", "Toggle" }))
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
			if (GUILayout.Button((FlatscreenMod.Instance != null && FlatscreenMod.Instance.ThirdPersonEnabled) ? "Third Person On" : "Third Person Off", GUILayout.Width(150f)) && FlatscreenMod.Instance != null)
			{
				FlatscreenMod.Instance.ToggleThirdPerson();
				_status = (FlatscreenMod.Instance.ThirdPersonEnabled ? "Third person enabled" : "Third person disabled");
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
				float thirdPersonDistance = FlatscreenMod.Instance.ThirdPersonDistance;
				GUILayout.Label(new GUIContent("Third-person distance", "V toggles third person. I opens the bag."));
				float num3 = GUILayout.HorizontalSlider(thirdPersonDistance, 0.8f, 4.5f, GUILayout.Width(320f));
				if (Mathf.Abs(num3 - thirdPersonDistance) > 0.01f)
				{
					FlatscreenMod.Instance.SetThirdPersonDistance(num3);
					_status = "Third-person distance " + num3.ToString("0.0");
				}
			}
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Reset Height", GUILayout.Width(120f)) && FlatscreenMod.Instance != null)
			{
				FlatscreenMod.Instance.ResetHeightOffset();
				_status = "Height reset";
			}
			if (GUILayout.Button("Lower", GUILayout.Width(80f)) && FlatscreenMod.Instance != null)
			{
				FlatscreenMod.Instance.SetHeightOffset(FlatscreenMod.Instance.HeightOffset - 0.15f);
				_status = "Height lowered";
			}
			if (GUILayout.Button("Raise", GUILayout.Width(80f)) && FlatscreenMod.Instance != null)
			{
				FlatscreenMod.Instance.SetHeightOffset(FlatscreenMod.Instance.HeightOffset + 0.15f);
				_status = "Height raised";
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
				if (!game.TryToggleSceneBoolean(new string[5] { "Nameplate", "NameTag", "PlayerName", "PlayerNameplate", "NameDisplay" }, new string[5] { "ShowNames", "ShowUsernames", "ShowNameplates", "DisplayNames", "DisplayUsernames" }))
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
				if (!game.TryInvokeSceneMethod(new string[5] { "Nameplate", "NameTag", "PlayerName", "PlayerNameplate", "NameDisplay" }, new string[4] { "Toggle", "Show", "Hide", "SetVisible" }))
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
			if (GUILayout.Button("Wheel Left", GUILayout.Width(100f)) && !_TryWheelScroll(game, -1f))
			{
				_status = game.LastInteractResult;
			}
			if (GUILayout.Button("Wheel Right", GUILayout.Width(100f)) && !_TryWheelScroll(game, 1f))
			{
				_status = game.LastInteractResult;
			}
			if (GUILayout.Button("Reset Wheel", GUILayout.Width(120f)))
			{
				if (!game.TryInvokeSceneMethod(new string[4] { "CaptainsWheel", "WheelGrab", "ServerSelectionMenu", "VrMainMenu" }, new string[5] { "Reset", "Recenter", "ResetWheel", "ResetPosition", "Home" }))
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
			MonoBehaviour monoBehaviour = FindMonoBehaviour("CaptainsWheel");
			if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
			{
				_status = game.LastInteractResult;
				return true;
			}
			monoBehaviour = FindMonoBehaviour("WheelGrab");
			if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
			{
				_status = game.LastInteractResult;
				return true;
			}
			monoBehaviour = FindMonoBehaviour("ServerSelectionMenu");
			if (monoBehaviour != null && game.TryAdjustMenuWheel(monoBehaviour, direction))
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
			MonoBehaviour monoBehaviour = FindMonoBehaviour("VrMainMenu");
			if (monoBehaviour == null)
			{
				_status = "VrMainMenu not found";
				return;
			}
			MethodInfo method = monoBehaviour.GetType().GetMethod("JoinServer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				_status = "JoinServer method not found";
				return;
			}
			try
			{
				method.Invoke(monoBehaviour, new object[1] { _servers[_selectedIndex] });
				_status = "Joining " + GetText(_servers[_selectedIndex], "Name", "server");
			}
			catch (Exception ex)
			{
				_status = "Join failed: " + ex.GetType().Name;
			}
		}

		private void AddServersFromBoard(object board)
		{
			if (board != null)
			{
				AddServerEnumerable(GetFieldOrProperty(board, "lastFilteredServers") as IEnumerable);
				AddServerEnumerable(GetFieldOrProperty(board, "lastReceivedServers") as IEnumerable);
			}
		}

		private void AddServerEnumerable(IEnumerable enumerable)
		{
			if (enumerable == null)
			{
				return;
			}
			foreach (object item in enumerable)
			{
				if (item != null && !_servers.Contains(item))
				{
					_servers.Add(item);
				}
			}
		}

		private static bool TabButton(string label, bool active)
		{
			return TabButton(label, active, 120f);
		}

		private static bool TabButton(string label, bool active, float width)
		{
			GUILayoutOption[] options = (active ? new GUILayoutOption[1] { GUILayout.Width(width + 10f) } : new GUILayoutOption[1] { GUILayout.Width(width) });
			return GUILayout.Button(active ? ("[ " + label + " ]") : label, options);
		}

		private static MonoBehaviour FindMonoBehaviour(string typeName)
		{
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (monoBehaviour != null && monoBehaviour.GetType().Name == typeName)
				{
					return monoBehaviour;
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
			Type type = instance.GetType();
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
			object fieldOrProperty = GetFieldOrProperty(instance, memberName);
			return (fieldOrProperty == null) ? fallback : fieldOrProperty.ToString();
		}

		private static void InvokeNoThrow(object instance, string methodName)
		{
			if (instance == null)
			{
				return;
			}
			MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
