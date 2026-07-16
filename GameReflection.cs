using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
	internal sealed class GameReflection
	{
		private sealed class BagState
		{
			public Transform Parent;

			public Vector3 LocalPosition;

			public Quaternion LocalRotation;

			public Vector3 LocalScale;

			public bool IsOpen;
		}

		internal sealed class PlayerInputPair
		{
			public object Left;

			public object Right;

			public bool HasAny
			{
				get
				{
					return Left != null || Right != null;
				}
			}
		}

		private Type _playerType;

		private Type _playerControllerType;

		private PropertyInfo _currentPlayerProperty;

		private PropertyInfo _currentPlayerControllerProperty;

		private PropertyInfo _playerTransformProperty;

		private PropertyInfo _leftInputProperty;

		private PropertyInfo _rightInputProperty;

		private MethodInfo _locomotionTranslateMethod;

		private object _locomotionController;

		private PlayerInputPair _looseInputPair = new PlayerInputPair();

		private MethodInfo _inputSourceEventMethod;

		private MethodInfo _startInteractMethod;

		private MethodInfo _startInteractPickupMethod;

		private MethodInfo _stopInteractMethod;

		private MethodInfo _testLeftHandMethod;

		private MethodInfo _testRightHandMethod;

		private MethodInfo _testLeftHandEndMethod;

		private MethodInfo _testRightHandEndMethod;

		private readonly Dictionary<int, BagState> _openBags = new Dictionary<int, BagState>();

		private Transform _cachedLocalBagTransform;

		public string LastInteractorName { get; private set; }

		public string LastInteractResult { get; private set; }

		public string PlayerTypeName
		{
			get
			{
				return (_playerType == null) ? "not found" : _playerType.FullName;
			}
		}

		public string PlayerControllerTypeName
		{
			get
			{
				return (_playerControllerType == null) ? "not found" : _playerControllerType.FullName;
			}
		}

		public bool HasCurrentPlayerProperty
		{
			get
			{
				return _currentPlayerProperty != null;
			}
		}

		public bool HasCurrentPlayerControllerProperty
		{
			get
			{
				return _currentPlayerControllerProperty != null;
			}
		}

		public bool HasPlayerTransformProperty
		{
			get
			{
				return _playerTransformProperty != null;
			}
		}

		public bool HasLeftInputProperty
		{
			get
			{
				return _leftInputProperty != null;
			}
		}

		public bool HasRightInputProperty
		{
			get
			{
				return _rightInputProperty != null;
			}
		}

		public bool HasLocomotionController
		{
			get
			{
				return _locomotionController as Component != null;
			}
		}

		public object FindLocalPlayer()
		{
			EnsurePlayerType();
			object obj = ((_currentPlayerProperty == null) ? null : _currentPlayerProperty.GetValue(null, null));
			if (IsPlayableLocalPlayer(obj))
			{
				return obj;
			}
			object obj2 = FindScenePlayer();
			if (obj2 != null)
			{
				return obj2;
			}
			return obj;
		}

		public Transform GetPlayerTransform(object player)
		{
			if (player == null)
			{
				return null;
			}
			EnsurePlayerMembers(player.GetType());
			if (_playerTransformProperty != null)
			{
				try
				{
					object value = _playerTransformProperty.GetValue(player, null);
					Transform transform = value as Transform;
					if (transform != null)
					{
						return transform;
					}
				}
				catch
				{
				}
			}
			Component component = player as Component;
			if (component != null)
			{
				return component.transform;
			}
			return GetProperty<Transform>(player, "transform");
		}

		public Transform GetPlayerRootTransform(object player)
		{
			Transform playerTransform = GetPlayerTransform(player);
			if (playerTransform == null)
			{
				return null;
			}
			return (playerTransform.root != null) ? playerTransform.root : playerTransform;
		}

		public Camera GetPlayerCamera(object player)
		{
			if (player == null)
			{
				return null;
			}
			Camera property = GetProperty<Camera>(player, "Camera");
			if (property != null)
			{
				return property;
			}
			Component component = player as Component;
			if (component != null)
			{
				property = component.GetComponentInChildren<Camera>(true);
				if (property != null)
				{
					return property;
				}
			}
			return null;
		}

		public object FindLocalController()
		{
			EnsurePlayerControllerType();
			object obj = ((_currentPlayerControllerProperty == null) ? null : _currentPlayerControllerProperty.GetValue(null, null));
			if (obj != null)
			{
				return obj;
			}
			return FindSceneController();
		}

		public Transform GetControllerTransform(object controller)
		{
			if (controller == null)
			{
				return null;
			}
			Component component = controller as Component;
			if (component != null)
			{
				return component.transform;
			}
			return GetProperty<Transform>(controller, "transform");
		}

		public Camera GetControllerCamera(object controller)
		{
			if (controller == null)
			{
				return null;
			}
			Camera property = GetProperty<Camera>(controller, "Camera");
			if (property != null)
			{
				return property;
			}
			Component component = controller as Component;
			if (component != null)
			{
				property = component.GetComponentInChildren<Camera>(true);
				if (property != null)
				{
					return property;
				}
			}
			return null;
		}

		public object GetLeftInput(object player)
		{
			EnsurePlayerMembers(player.GetType());
			return (_leftInputProperty == null) ? null : _leftInputProperty.GetValue(player, null);
		}

		public object GetRightInput(object player)
		{
			EnsurePlayerMembers(player.GetType());
			return (_rightInputProperty == null) ? null : _rightInputProperty.GetValue(player, null);
		}

		public PlayerInputPair FindLoosePlayerInputs()
		{
			Component component = _looseInputPair.Left as Component;
			Component component2 = _looseInputPair.Right as Component;
			if (component != null || component2 != null)
			{
				return _looseInputPair;
			}
			_looseInputPair = new PlayerInputPair();
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (!(monoBehaviour == null) && !(monoBehaviour.GetType().Name != "PlayerInput"))
				{
					object property = GetProperty<object>(monoBehaviour, "HandIndex");
					string text = ((property == null) ? string.Empty : property.ToString().ToLowerInvariant());
					if (text.Contains("left"))
					{
						_looseInputPair.Left = monoBehaviour;
					}
					else if (text.Contains("right"))
					{
						_looseInputPair.Right = monoBehaviour;
					}
					else if (_looseInputPair.Left == null)
					{
						_looseInputPair.Left = monoBehaviour;
					}
					else if (_looseInputPair.Right == null && !object.ReferenceEquals(_looseInputPair.Left, monoBehaviour))
					{
						_looseInputPair.Right = monoBehaviour;
					}
				}
			}
			if (_looseInputPair.Left == null && _looseInputPair.Right != null)
			{
				_looseInputPair.Left = _looseInputPair.Right;
			}
			if (_looseInputPair.Right == null && _looseInputPair.Left != null)
			{
				_looseInputPair.Right = _looseInputPair.Left;
			}
			return _looseInputPair;
		}

		public bool TryTranslateWithLocomotion(object player, Vector3 translation)
		{
			object obj = FindLocomotionController(player);
			if (obj == null)
			{
				return TryTranslateWithController(player, translation);
			}
			if (_locomotionTranslateMethod == null)
			{
				_locomotionTranslateMethod = obj.GetType().GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector3) }, null);
			}
			if (_locomotionTranslateMethod == null)
			{
				return false;
			}
			try
			{
				_locomotionTranslateMethod.Invoke(obj, new object[1] { translation });
				return true;
			}
			catch
			{
				return TryTranslateWithController(player, translation);
			}
		}

		public bool TryTranslateWithController(object controller, Vector3 translation)
		{
			Component component = controller as Component;
			if (component == null)
			{
				return TryTranslateWithCharacterController(controller, translation);
			}
			Type type = controller.GetType();
			MethodInfo method = type.GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector3) }, null);
			if (method == null)
			{
				method = type.GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector3) }, null);
			}
			if (method != null)
			{
				try
				{
					method.Invoke(controller, new object[1] { translation });
					LastInteractResult = type.Name + "." + method.Name + " called";
					return true;
				}
				catch
				{
				}
			}
			return TryTranslateWithCharacterController(controller, translation);
		}

		public bool IsMovementBlocked(object player, object controller)
		{
			return HasBlockedState(player) || HasBlockedState(controller);
		}

		public bool IsTargetHeldByOther(object target)
		{
			if (target == null)
			{
				return false;
			}
			if (IsTargetAttachedToRemotePlayer(target))
			{
				LastInteractResult = "blocked: target attached to remote player";
				return true;
			}
			string[] array = new string[8] { "Holder", "HeldBy", "Interactor", "CurrentInteractor", "Grabber", "Owner", "Player", "Controller" };
			for (int i = 0; i < array.Length; i++)
			{
				object obj = GetProperty<object>(target, array[i]) ?? GetField<object>(target, array[i]);
				if (obj != null && !IsLocalLike(obj))
				{
					LastInteractResult = "blocked: target held by other";
					return true;
				}
			}
			string[] array2 = new string[5] { "IsHeld", "Held", "IsGrabbed", "Grabbed", "InHand" };
			bool flag = false;
			for (int i = 0; i < array2.Length; i++)
			{
				if (ReadBoolMember(target, array2[i]))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return false;
			}
			Component component = target as Component;
			if (component != null)
			{
				MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
				foreach (MonoBehaviour monoBehaviour in componentsInParent)
				{
					if (monoBehaviour == null || object.ReferenceEquals(monoBehaviour, target))
					{
						continue;
					}
					for (int i = 0; i < array.Length; i++)
					{
						object obj = GetProperty<object>(monoBehaviour, array[i]) ?? GetField<object>(monoBehaviour, array[i]);
						if (obj != null && !IsLocalLike(obj))
						{
							LastInteractResult = "blocked: target held by other";
							return true;
						}
					}
				}
			}
			return false;
		}

		public Transform GetInputTargetTransform(object playerInput)
		{
			return GetProperty<Transform>(playerInput, "TargetTransform");
		}

		public object GetRawInput(object playerInput)
		{
			return GetProperty<object>(playerInput, "RawInput");
		}

		public void SetButton(object playerInput, object rawInput, string propertyName, bool value)
		{
			object property = GetProperty<object>(rawInput, propertyName);
			SetProperty(property, "State", value);
			InvokeInputEvent(property, playerInput);
		}

		public void SetRawInputVector2(object rawInput, string propertyName, Vector2 value)
		{
			SetProperty(rawInput, propertyName, value);
		}

		public void SetRawInputVector3(object rawInput, string propertyName, Vector3 value)
		{
			SetProperty(rawInput, propertyName, value);
		}

		public void SetRawInputFloat(object rawInput, string propertyName, float value)
		{
			SetProperty(rawInput, propertyName, value);
		}

		public object FindInteractorForInput(object playerInput)
		{
			LastInteractorName = "none";
			if (playerInput == null)
			{
				return null;
			}
			Transform inputTargetTransform = GetInputTargetTransform(playerInput);
			object obj = null;
			float num = float.MaxValue;
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (monoBehaviour == null || monoBehaviour.GetType().Name != "Interactor")
				{
					continue;
				}
				object property = GetProperty<object>(monoBehaviour, "Input");
				if (object.ReferenceEquals(property, playerInput))
				{
					LastInteractorName = monoBehaviour.name;
					return monoBehaviour;
				}
				object property2 = GetProperty<object>(monoBehaviour, "Controller");
				object property3 = GetProperty<object>(property2, "PlayerInput");
				if (object.ReferenceEquals(property3, playerInput))
				{
					LastInteractorName = monoBehaviour.name;
					return monoBehaviour;
				}
				if (inputTargetTransform != null)
				{
					float sqrMagnitude = (monoBehaviour.transform.position - inputTargetTransform.position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						obj = monoBehaviour;
					}
				}
			}
			if (obj != null && num < 4f)
			{
				LastInteractorName = ((Component)obj).name + " (nearest)";
				return obj;
			}
			object obj2 = FindLocalController();
			if (obj2 != null)
			{
				object property4 = GetProperty<object>(playerInput, "HandIndex");
				string text = ((property4 == null) ? string.Empty : property4.ToString().ToLowerInvariant());
				string text2 = (text.Contains("left") ? "LeftController" : "RightController");
				object property5 = GetProperty<object>(obj2, text2);
				object property6 = GetProperty<object>(property5, "Interactor");
				if (property6 != null)
				{
					LastInteractorName = text2 + ".Interactor";
					return property6;
				}
			}
			return null;
		}

		public bool IsObjectAlive(object value)
		{
			if (value == null)
			{
				return false;
			}
			UnityEngine.Object obj = value as UnityEngine.Object;
			if (obj != null)
			{
				return true;
			}
			return !(value is UnityEngine.Object);
		}

		public bool TryStartInteract(object interactor, object interactable)
		{
			if (interactor == null || interactable == null)
			{
				LastInteractResult = "start failed: missing " + ((interactor == null) ? "interactor" : "interactable");
				return false;
			}
			if (_startInteractMethod == null || _startInteractMethod.DeclaringType != interactor.GetType())
			{
				_startInteractMethod = interactor.GetType().GetMethod("StartInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_startInteractMethod == null)
			{
				LastInteractResult = "start failed: no StartInteract";
				return false;
			}
			try
			{
				_startInteractMethod.Invoke(interactor, new object[4] { interactable, false, true, true });
				LastInteractResult = "start called";
				return true;
			}
			catch
			{
				LastInteractResult = "start threw";
				return false;
			}
		}

		public bool TryStopInteract(object interactor)
		{
			if (interactor == null)
			{
				return false;
			}
			if (_stopInteractMethod == null || _stopInteractMethod.DeclaringType != interactor.GetType())
			{
				_stopInteractMethod = interactor.GetType().GetMethod("StopInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_stopInteractMethod == null)
			{
				return false;
			}
			try
			{
				_stopInteractMethod.Invoke(interactor, new object[2] { false, true });
				LastInteractResult = "stop called";
				return true;
			}
			catch
			{
				LastInteractResult = "stop threw";
				return false;
			}
		}

		public bool TryReleaseHeldHands(object player)
		{
			bool flag = false;
			if (player == null)
			{
				return false;
			}
			object leftInput = GetLeftInput(player);
			object rightInput = GetRightInput(player);
			object obj = FindInteractorForInput(leftInput);
			if (obj != null && TryStopInteract(obj))
			{
				flag = true;
			}
			object obj2 = FindInteractorForInput(rightInput);
			if (obj2 != null && TryStopInteract(obj2))
			{
				flag = true;
			}
			if (flag)
			{
				LastInteractResult = "held hands released";
			}
			return flag;
		}

		public bool IsInteractorInteracting(object interactor)
		{
			if (interactor == null)
			{
				return false;
			}
			object property = GetProperty<object>(interactor, "IsInteracting");
			return property is bool && (bool)property;
		}

		public bool TryPickupGrab(object interactor, object pickup)
		{
			if (interactor == null || pickup == null)
			{
				LastInteractResult = "pickup grab failed: missing " + ((interactor == null) ? "interactor" : "pickup");
				return false;
			}
			if (IsInteractorInteracting(interactor))
			{
				TryStopInteract(interactor);
				LastInteractResult = "pickup grab toggled stop";
				return true;
			}
			TryUndockPickup(pickup, interactor);
			TryResetTimeout(interactor);
			if (_startInteractPickupMethod == null || _startInteractPickupMethod.DeclaringType != interactor.GetType())
			{
				_startInteractPickupMethod = interactor.GetType().GetMethod("StartInteract", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_startInteractPickupMethod == null)
			{
				LastInteractResult = "pickup grab failed: no StartInteract";
				return false;
			}
			try
			{
				_startInteractPickupMethod.Invoke(interactor, new object[4] { pickup, false, false, false });
				LastInteractResult = "pickup StartInteract called";
				return true;
			}
			catch
			{
				LastInteractResult = "pickup StartInteract threw";
				return false;
			}
		}

		private void TryUndockPickup(object pickup, object interactor)
		{
			object property = GetProperty<object>(pickup, "IsDocked");
			if (!(property is bool) || !(bool)property)
			{
				return;
			}
			MethodInfo method = pickup.GetType().GetMethod("Undock", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return;
			}
			try
			{
				method.Invoke(pickup, new object[3] { interactor, true, 1 });
			}
			catch
			{
			}
		}

		private void TryResetTimeout(object interactor)
		{
			MethodInfo method = interactor.GetType().GetMethod("ResetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return;
			}
			try
			{
				method.Invoke(interactor, null);
			}
			catch
			{
			}
		}

		public bool TryTestGrab(object interactable, bool isLeft)
		{
			if (interactable == null)
			{
				LastInteractResult = "test grab failed: missing interactable";
				return false;
			}
			MethodInfo testGrabMethod = GetTestGrabMethod(interactable.GetType(), isLeft, false);
			if (testGrabMethod == null)
			{
				LastInteractResult = "test grab failed: no method";
				return false;
			}
			try
			{
				testGrabMethod.Invoke(interactable, null);
				LastInteractResult = (isLeft ? "TestLeftHand called" : "TestRightHand called");
				return true;
			}
			catch
			{
				LastInteractResult = "test grab threw";
				return false;
			}
		}

		public bool TryTestGrabEnd(object interactable, bool isLeft)
		{
			if (interactable == null)
			{
				return false;
			}
			MethodInfo testGrabMethod = GetTestGrabMethod(interactable.GetType(), isLeft, true);
			if (testGrabMethod == null)
			{
				return false;
			}
			try
			{
				testGrabMethod.Invoke(interactable, null);
				LastInteractResult = (isLeft ? "TestLeftHandEnd called" : "TestRightHandEnd called");
				return true;
			}
			catch
			{
				LastInteractResult = "test grab end threw";
				return false;
			}
		}

		private MethodInfo GetTestGrabMethod(Type type, bool isLeft, bool isEnd)
		{
			if (isLeft && !isEnd)
			{
				return _testLeftHandMethod ?? (_testLeftHandMethod = type.GetMethod("TestLeftHand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			}
			if (!isLeft && !isEnd)
			{
				return _testRightHandMethod ?? (_testRightHandMethod = type.GetMethod("TestRightHand", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			}
			if (isLeft)
			{
				return _testLeftHandEndMethod ?? (_testLeftHandEndMethod = type.GetMethod("TestLeftHandEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
			}
			return _testRightHandEndMethod ?? (_testRightHandEndMethod = type.GetMethod("TestRightHandEnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
		}

		public bool TryAdjustMenuWheel(object menuTarget, float direction)
		{
			if (menuTarget == null)
			{
				LastInteractResult = "menu wheel failed: missing target";
				return false;
			}
			Type type = menuTarget.GetType();
			string name = type.Name;
			if (name == "CaptainsWheel" && TryAdjustFloatProperty(menuTarget, "Value", direction * 0.2f))
			{
				LastInteractResult = "CaptainsWheel.Value adjusted";
				return true;
			}
			if (name == "WheelGrab" && TryAdjustFloatProperty(menuTarget, "Progress", direction * 0.2f))
			{
				LastInteractResult = "WheelGrab.Progress adjusted";
				return true;
			}
			object obj = ((name == "ServerSelectionMenu") ? menuTarget : FindParentComponent(menuTarget as Component, "ServerSelectionMenu"));
			if (obj != null && TryCallScrollServerListFromWheel(obj, direction))
			{
				LastInteractResult = "ServerSelectionMenu wheel scroll called";
				return true;
			}
			LastInteractResult = "menu wheel failed: " + name;
			return false;
		}

		public bool TryReturnToMainMenu()
		{
			Type type = FindType("GameModeManager");
			if (type != null)
			{
				MethodInfo method = type.GetMethod("StopCurrentModeAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					try
					{
						method.Invoke(null, new object[2] { "return to menu", true });
						LastInteractResult = "GameModeManager.StopCurrentModeAsync called";
						return true;
					}
					catch
					{
					}
				}
			}
			string[] array = new string[6] { "Disconnect", "LeaveServer", "ReturnToMainMenu", "QuitToMainMenu", "BackToMenu", "LoadMainMenu" };
			MonoBehaviour[] array2 = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array2)
			{
				if (monoBehaviour == null)
				{
					continue;
				}
				string[] array3 = array;
				foreach (string text in array3)
				{
					MethodInfo method2 = monoBehaviour.GetType().GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (!(method2 == null) && method2.GetParameters().Length == 0)
					{
						try
						{
							method2.Invoke(monoBehaviour, null);
							LastInteractResult = text + " called";
							return true;
						}
						catch
						{
						}
					}
				}
			}
			LastInteractResult = "disconnect failed: no main-menu method";
			return false;
		}

		public bool TryToggleSceneBoolean(string[] typeNameHints, string[] memberNames)
		{
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (!(monoBehaviour == null) && TypeMatchesAny(monoBehaviour.GetType(), typeNameHints) && TryToggleBooleanMember(monoBehaviour, memberNames))
				{
					LastInteractResult = monoBehaviour.GetType().Name + " toggled";
					return true;
				}
			}
			LastInteractResult = "toggle failed: no matching scene member";
			return false;
		}

		public bool TryInvokeSceneMethod(string[] typeNameHints, string[] methodNames)
		{
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (monoBehaviour == null || !TypeMatchesAny(monoBehaviour.GetType(), typeNameHints))
				{
					continue;
				}
				Type type = monoBehaviour.GetType();
				foreach (string text in methodNames)
				{
					MethodInfo method = type.GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (!(method == null) && method.GetParameters().Length == 0)
					{
						try
						{
							method.Invoke(monoBehaviour, null);
							LastInteractResult = type.Name + "." + text + " called";
							return true;
						}
						catch
						{
						}
					}
				}
			}
			LastInteractResult = "invoke failed: no matching scene method";
			return false;
		}

		public bool TryRunQuickAccessAction(string typeName, string assetName, string openMethodName, string runMethodName)
		{
			return TryRunQuickAccessAction(string.IsNullOrEmpty(typeName) ? null : new string[1] { typeName }, string.IsNullOrEmpty(assetName) ? null : new string[1] { assetName }, string.IsNullOrEmpty(openMethodName) ? null : new string[1] { openMethodName }, string.IsNullOrEmpty(runMethodName) ? null : new string[1] { runMethodName });
		}

		public bool TryRunQuickAccessAction(string[] typeNameHints, string[] assetNameHints, string[] openMethodNames, string[] runMethodNames)
		{
			object obj = FindResourceObject(typeNameHints, assetNameHints);
			if (obj == null)
			{
				LastInteractResult = "quick access failed: " + DescribeHints(typeNameHints, assetNameHints) + " not found";
				return false;
			}
			if (openMethodNames != null)
			{
				for (int i = 0; i < openMethodNames.Length; i++)
				{
					InvokeIfExists(obj, openMethodNames[i], null);
				}
			}
			if (runMethodNames != null)
			{
				foreach (string text in runMethodNames)
				{
					object[] args = new object[1];
					if (InvokeIfExists(obj, text, args))
					{
						LastInteractResult = obj.GetType().Name + "." + text + " called";
						return true;
					}
					if (InvokeIfExists(obj, text, null))
					{
						LastInteractResult = obj.GetType().Name + "." + text + " called";
						return true;
					}
				}
			}
			LastInteractResult = "quick access failed: " + obj.GetType().Name;
			return false;
		}

		public bool TrySetShowNames(bool enabled)
		{
			object obj = FindResourceObject("ShowNames", null);
			if (obj == null)
			{
				LastInteractResult = "ShowNames asset not found";
				return false;
			}
			MethodInfo method = obj.GetType().GetMethod("Activate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				LastInteractResult = "ShowNames.Activate not found";
				return false;
			}
			try
			{
				method.Invoke(obj, new object[1] { enabled });
				LastInteractResult = "ShowNames set to " + enabled;
				return true;
			}
			catch
			{
				LastInteractResult = "ShowNames activate threw";
				return false;
			}
		}

		public bool TryTogglePlayerBag(Transform cameraTransform)
		{
			if (cameraTransform == null)
			{
				LastInteractResult = "bag toggle failed: missing camera";
				return false;
			}
			Transform localBagTransform = GetLocalBagTransform();
			if (localBagTransform == null)
			{
				LastInteractResult = "bag toggle failed: local bag not found";
				return false;
			}
			Transform playerRootTransform = GetPlayerRootTransform(FindLocalPlayer());
			if (playerRootTransform == null)
			{
				LastInteractResult = "bag toggle failed: local player root not found";
				return false;
			}
			Transform transform = localBagTransform.Find("Storage");
			if (transform == null)
			{
				LastInteractResult = "bag toggle failed: storage child not found";
				return false;
			}
			int instanceID = localBagTransform.gameObject.GetInstanceID();
			BagState value;
			if (!_openBags.TryGetValue(instanceID, out value))
			{
				value = new BagState();
				value.Parent = localBagTransform.parent;
				value.LocalPosition = localBagTransform.localPosition;
				value.LocalRotation = localBagTransform.localRotation;
				value.LocalScale = localBagTransform.localScale;
				_openBags[instanceID] = value;
			}
			if (!value.IsOpen)
			{
				transform.gameObject.SetActive(true);
				localBagTransform.SetParent(playerRootTransform, true);
				localBagTransform.position = cameraTransform.position + cameraTransform.forward * 0.46f + cameraTransform.up * -0.18f;
				localBagTransform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up), Vector3.up);
				localBagTransform.localScale = value.LocalScale;
				value.IsOpen = true;
				LastInteractResult = "bag opened";
				return true;
			}
			localBagTransform.SetParent(value.Parent, true);
			localBagTransform.localPosition = value.LocalPosition;
			localBagTransform.localRotation = value.LocalRotation;
			localBagTransform.localScale = value.LocalScale;
			transform.gameObject.SetActive(false);
			value.IsOpen = false;
			_openBags.Remove(instanceID);
			LastInteractResult = "bag closed";
			return true;
		}

		public Transform GetLocalBagTransform()
		{
			Transform transform = FindLocalBag();
			if (transform != null)
			{
				_cachedLocalBagTransform = transform;
				return transform;
			}
			if (_cachedLocalBagTransform != null)
			{
				return _cachedLocalBagTransform;
			}
			return null;
		}

		private static bool TryAdjustFloatProperty(object target, string propertyName, float delta)
		{
			PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property == null || !property.CanRead || !property.CanWrite)
			{
				return false;
			}
			try
			{
				float num = (float)property.GetValue(target, null);
				property.SetValue(target, num + delta, null);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryToggleBooleanMember(object target, string[] memberNames)
		{
			Type type = target.GetType();
			foreach (string name in memberNames)
			{
				PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(bool))
				{
					try
					{
						bool flag = (bool)property.GetValue(target, null);
						property.SetValue(target, !flag, null);
						return true;
					}
					catch
					{
					}
				}
				FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(bool))
				{
					try
					{
						bool flag = (bool)field.GetValue(target);
						field.SetValue(target, !flag);
						return true;
					}
					catch
					{
					}
				}
			}
			return false;
		}

		private static bool TypeMatchesAny(Type type, string[] typeNameHints)
		{
			if (typeNameHints == null || typeNameHints.Length == 0)
			{
				return true;
			}
			while (type != null)
			{
				foreach (string text in typeNameHints)
				{
					if (!string.IsNullOrEmpty(text))
					{
						if (type.Name == text || type.FullName == text)
						{
							return true;
						}
						if ((type.Name != null && type.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) || (type.FullName != null && type.FullName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
						{
							return true;
						}
					}
				}
				type = type.BaseType;
			}
			return false;
		}

		private static Type FindType(string typeName)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				Type type = assembly.GetType(typeName);
				if (type != null)
				{
					return type;
				}
				Type[] types = assembly.GetTypes();
				foreach (Type type2 in types)
				{
					if (!(type2 == null) && (type2.Name == typeName || type2.FullName == typeName || (type2.Name != null && type2.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0) || (type2.FullName != null && type2.FullName.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)))
					{
						return type2;
					}
				}
			}
			return null;
		}

		private static Type FindTypeByHint(string typeNameHint)
		{
			if (string.IsNullOrEmpty(typeNameHint))
			{
				return null;
			}
			return FindType(typeNameHint);
		}

		private static object FindResourceObject(string typeNameHint, string nameHint)
		{
			return FindResourceObject(string.IsNullOrEmpty(typeNameHint) ? null : new string[1] { typeNameHint }, string.IsNullOrEmpty(nameHint) ? null : new string[1] { nameHint });
		}

		private static object FindResourceObject(string[] typeNameHints, string[] nameHints)
		{
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
			foreach (UnityEngine.Object obj in array)
			{
				if (!(obj == null))
				{
					Type type = obj.GetType();
					if (TypeMatchesAny(type, typeNameHints) && NameMatchesAny(obj.name, nameHints))
					{
						return obj;
					}
				}
			}
			return null;
		}

		private static bool NameMatchesAny(string value, string[] nameHints)
		{
			if (nameHints == null || nameHints.Length == 0)
			{
				return true;
			}
			foreach (string value2 in nameHints)
			{
				if (!string.IsNullOrEmpty(value2) && !string.IsNullOrEmpty(value) && value.IndexOf(value2, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private static bool InvokeIfExists(object target, string methodName, object[] args)
		{
			if (target == null || string.IsNullOrEmpty(methodName))
			{
				return false;
			}
			MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name != methodName)
				{
					continue;
				}
				ParameterInfo[] parameters = methodInfo.GetParameters();
				int num = ((args != null) ? args.Length : 0);
				if (parameters.Length == num)
				{
					try
					{
						methodInfo.Invoke(target, args);
						return true;
					}
					catch
					{
					}
				}
			}
			return false;
		}

		private Transform FindLocalBag()
		{
			if (_cachedLocalBagTransform != null)
			{
				Transform cachedLocalBagTransform = _cachedLocalBagTransform;
				Transform playerRootTransform = GetPlayerRootTransform(FindLocalPlayer());
				if (cachedLocalBagTransform != null && IsLocalBagTransform(cachedLocalBagTransform, playerRootTransform))
				{
					return cachedLocalBagTransform;
				}
			}
			object player = FindLocalPlayer();
			Transform playerRootTransform2 = GetPlayerRootTransform(player);
			UnityEngine.Object[] array = Resources.FindObjectsOfTypeAll(typeof(GameObject));
			for (int i = 0; i < array.Length; i++)
			{
				GameObject gameObject = array[i] as GameObject;
				if (!(gameObject == null) && gameObject.name.EndsWith("Bag(Clone)", StringComparison.Ordinal))
				{
					Transform transform = gameObject.transform;
					if (IsLocalBagTransform(transform, playerRootTransform2))
					{
						_cachedLocalBagTransform = transform;
						return transform;
					}
				}
			}
			return null;
		}

		private bool IsLocalBagTransform(Transform bagTransform, Transform playerRoot)
		{
			if (bagTransform == null)
			{
				return false;
			}
			string[] array = new string[7] { "Owner", "Player", "Holder", "HeldBy", "Controller", "Interactor", "CurrentInteractor" };
			MonoBehaviour[] componentsInParent = bagTransform.GetComponentsInParent<MonoBehaviour>(true);
			foreach (MonoBehaviour monoBehaviour in componentsInParent)
			{
				if (monoBehaviour == null)
				{
					continue;
				}
				if (IsLocalLike(monoBehaviour))
				{
					return true;
				}
				for (int j = 0; j < array.Length; j++)
				{
					object obj = GetProperty<object>(monoBehaviour, array[j]) ?? GetField<object>(monoBehaviour, array[j]);
					if (obj != null && IsLocalLike(obj))
					{
						return true;
					}
				}
			}
			return playerRoot != null && bagTransform.IsChildOf(playerRoot);
		}

		private static bool HasBlockedState(object target)
		{
			if (target == null)
			{
				return false;
			}
			string[] array = new string[13]
			{
				"IsDowned", "Downed", "IsDead", "Dead", "IsGrave", "InGrave", "IsGhost", "Ghost", "IsRespawning", "Respawning",
				"CanMove", "MovementAllowed", "AllowMovement"
			};
			Type type = target.GetType();
			foreach (string text in array)
			{
				PropertyInfo property = type.GetProperty(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
				{
					try
					{
						bool flag = (bool)property.GetValue(target, null);
						if (text == "CanMove" || text == "MovementAllowed" || text == "AllowMovement")
						{
							if (!flag)
							{
								return true;
							}
						}
						else if (flag)
						{
							return true;
						}
					}
					catch
					{
					}
				}
				FieldInfo field = type.GetField(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (!(field != null) || !(field.FieldType == typeof(bool)))
				{
					continue;
				}
				try
				{
					bool flag = (bool)field.GetValue(target);
					if (text == "CanMove" || text == "MovementAllowed" || text == "AllowMovement")
					{
						if (!flag)
						{
							return true;
						}
					}
					else if (flag)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			object obj3 = GetProperty<object>(target, "State") ?? GetProperty<object>(target, "Status");
			if (obj3 != null)
			{
				string text2 = obj3.ToString();
				if (!string.IsNullOrEmpty(text2))
				{
					text2 = text2.ToLowerInvariant();
					if (text2.Contains("down") || text2.Contains("dead") || text2.Contains("grave") || text2.Contains("ghost") || text2.Contains("respawn"))
					{
						return true;
					}
				}
			}
			return false;
		}

		private bool IsLocalLike(object owner)
		{
			return IsLocalLike(owner, 0);
		}

		private bool IsLocalLike(object owner, int depth)
		{
			if (owner == null || depth > 4)
			{
				return false;
			}
			object obj = FindLocalPlayer();
			if (object.ReferenceEquals(owner, obj))
			{
				return true;
			}
			object objB = FindLocalController();
			if (object.ReferenceEquals(owner, objB))
			{
				return true;
			}
			object objB2 = ((obj == null) ? null : GetLeftInput(obj));
			object objB3 = ((obj == null) ? null : GetRightInput(obj));
			if (object.ReferenceEquals(owner, objB2) || object.ReferenceEquals(owner, objB3))
			{
				return true;
			}
			if (ReadBoolMember(owner, "IsLocal") || ReadBoolMember(owner, "IsLocalPlayer") || ReadBoolMember(owner, "IsOwned") || ReadBoolMember(owner, "IsOwner") || ReadBoolMember(owner, "HasAuthority") || ReadBoolMember(owner, "IsMine"))
			{
				return true;
			}
			object property = GetProperty<object>(owner, "PlayerInput");
			if (property != null && (object.ReferenceEquals(property, objB2) || object.ReferenceEquals(property, objB3)))
			{
				return true;
			}
			object property2 = GetProperty<object>(owner, "Input");
			if (property2 != null && (object.ReferenceEquals(property2, objB2) || object.ReferenceEquals(property2, objB3)))
			{
				return true;
			}
			object property3 = GetProperty<object>(owner, "Controller");
			if (property3 != null && (object.ReferenceEquals(property3, objB) || IsLocalLike(property3, depth + 1)))
			{
				return true;
			}
			object property4 = GetProperty<object>(owner, "Player");
			if (property4 != null && (object.ReferenceEquals(property4, obj) || IsLocalLike(property4, depth + 1)))
			{
				return true;
			}
			object property5 = GetProperty<object>(owner, "Holder");
			if (property5 != null && IsLocalLike(property5, depth + 1))
			{
				return true;
			}
			return false;
		}

		private bool IsTargetAttachedToRemotePlayer(object target)
		{
			Component component = target as Component;
			if (component == null)
			{
				return false;
			}
			object player = FindLocalPlayer();
			Transform playerRootTransform = GetPlayerRootTransform(player);
			object controller = FindLocalController();
			Transform controllerTransform = GetControllerTransform(controller);
			Transform transform = component.transform;
			while (transform != null)
			{
				if (playerRootTransform != null && object.ReferenceEquals(transform, playerRootTransform))
				{
					return false;
				}
				if (controllerTransform != null && object.ReferenceEquals(transform, controllerTransform))
				{
					return false;
				}
				MonoBehaviour[] components = transform.GetComponents<MonoBehaviour>();
				foreach (MonoBehaviour monoBehaviour in components)
				{
					if (!(monoBehaviour == null))
					{
						if (IsPlayablePlayerCandidate(monoBehaviour) && !IsLocalLike(monoBehaviour))
						{
							return true;
						}
						if (monoBehaviour.GetType().Name == "PlayerController" && !IsLocalLike(monoBehaviour))
						{
							return true;
						}
					}
				}
				transform = transform.parent;
			}
			return false;
		}

		private static string DescribeHints(string[] typeNameHints, string[] nameHints)
		{
			if (typeNameHints != null && typeNameHints.Length > 0 && !string.IsNullOrEmpty(typeNameHints[0]))
			{
				return typeNameHints[0];
			}
			if (nameHints != null && nameHints.Length > 0 && !string.IsNullOrEmpty(nameHints[0]))
			{
				return nameHints[0];
			}
			return "target";
		}

		private static bool TryCallScrollServerListFromWheel(object menu, float direction)
		{
			MethodInfo method = menu.GetType().GetMethod("ScrollServerListFromWheel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return false;
			}
			try
			{
				method.Invoke(menu, new object[2]
				{
					0f,
					direction * 0.2f
				});
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static object FindParentComponent(Component component, string typeName)
		{
			if (component == null)
			{
				return null;
			}
			MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
			foreach (MonoBehaviour monoBehaviour in componentsInParent)
			{
				if (monoBehaviour != null && monoBehaviour.GetType().Name == typeName)
				{
					return monoBehaviour;
				}
			}
			return null;
		}

		private object FindLocomotionController(object player)
		{
			Component component = _locomotionController as Component;
			if (component != null)
			{
				return _locomotionController;
			}
			Transform playerTransform = GetPlayerTransform(player);
			if (playerTransform == null)
			{
				return null;
			}
			MonoBehaviour[] componentsInChildren = playerTransform.GetComponentsInChildren<MonoBehaviour>(true);
			foreach (MonoBehaviour monoBehaviour in componentsInChildren)
			{
				if (monoBehaviour != null && monoBehaviour.GetType().Name == "PlayerLocomotionController")
				{
					_locomotionController = monoBehaviour;
					return monoBehaviour;
				}
			}
			componentsInChildren = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in componentsInChildren)
			{
				if (monoBehaviour != null && monoBehaviour.GetType().Name == "PlayerLocomotionController")
				{
					_locomotionController = monoBehaviour;
					return monoBehaviour;
				}
			}
			return null;
		}

		private bool TryTranslateWithCharacterController(object player, Vector3 translation)
		{
			Component component = player as Component;
			if (component == null)
			{
				return false;
			}
			Component component2 = component.GetComponent("CharacterController");
			if (component2 != null)
			{
				try
				{
					component2.GetType().GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector3) }, null).Invoke(component2, new object[1] { translation });
					LastInteractResult = "character controller moved";
					return true;
				}
				catch
				{
				}
			}
			Component component3 = component.GetComponent("Rigidbody");
			if (component3 != null)
			{
				try
				{
					MethodInfo method = component3.GetType().GetMethod("MovePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector3) }, null);
					if (method != null)
					{
						PropertyInfo property = component3.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						Vector3 vector = ((property == null) ? component.transform.position : ((Vector3)property.GetValue(component3, null)));
						method.Invoke(component3, new object[1] { vector + translation });
					}
					LastInteractResult = "rigidbody moved";
					return true;
				}
				catch
				{
				}
			}
			return false;
		}

		private object FindScenePlayer()
		{
			if (_playerType == null)
			{
				return null;
			}
			object obj = null;
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (!(monoBehaviour == null) && !(monoBehaviour.GetType() != _playerType))
				{
					if (IsPlayableLocalPlayer(monoBehaviour))
					{
						return monoBehaviour;
					}
					if (obj == null && IsPlayablePlayerCandidate(monoBehaviour))
					{
						obj = monoBehaviour;
					}
				}
			}
			return obj;
		}

		private object FindSceneController()
		{
			EnsurePlayerControllerType();
			if (_playerControllerType == null)
			{
				return null;
			}
			MonoBehaviour[] array = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in array)
			{
				if (monoBehaviour != null && monoBehaviour.GetType() == _playerControllerType)
				{
					return monoBehaviour;
				}
			}
			return null;
		}

		private bool IsPlayableLocalPlayer(object player)
		{
			if (player == null)
			{
				return false;
			}
			if (ReadBoolMember(player, "IsLocal") || ReadBoolMember(player, "IsLocalPlayer") || ReadBoolMember(player, "IsOwned") || ReadBoolMember(player, "IsOwner") || ReadBoolMember(player, "HasAuthority") || ReadBoolMember(player, "IsMine"))
			{
				return true;
			}
			return IsPlayablePlayerCandidate(player);
		}

		private bool IsPlayablePlayerCandidate(object player)
		{
			if (player == null)
			{
				return false;
			}
			Transform playerTransform;
			try
			{
				playerTransform = GetPlayerTransform(player);
			}
			catch
			{
				return false;
			}
			if (playerTransform == null)
			{
				return false;
			}
			object leftInput = GetLeftInput(player);
			object rightInput = GetRightInput(player);
			if (leftInput != null || rightInput != null)
			{
				return true;
			}
			return FindLocomotionController(player) != null;
		}

		private static bool ReadBoolMember(object instance, string name)
		{
			if (instance == null)
			{
				return false;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null)
			{
				try
				{
					object value = property.GetValue(instance, null);
					if (value is bool)
					{
						return (bool)value;
					}
				}
				catch
				{
				}
			}
			FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field != null)
			{
				try
				{
					object value = field.GetValue(instance);
					if (value is bool)
					{
						return (bool)value;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		private void EnsurePlayerType()
		{
			if (_playerType != null)
			{
				return;
			}
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				_playerType = assembly.GetType("Player");
				if (_playerType != null)
				{
					break;
				}
			}
			_currentPlayerProperty = ((_playerType == null) ? null : _playerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
		}

		private void EnsurePlayerControllerType()
		{
			if (_playerControllerType != null)
			{
				return;
			}
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
			{
				_playerControllerType = assembly.GetType("PlayerController");
				if (_playerControllerType != null)
				{
					break;
				}
			}
			_currentPlayerControllerProperty = ((_playerControllerType == null) ? null : _playerControllerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
		}

		private void EnsurePlayerMembers(Type playerType)
		{
			if (!(_playerTransformProperty != null))
			{
				_playerTransformProperty = playerType.GetProperty("PlayerTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_leftInputProperty = playerType.GetProperty("LeftInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_rightInputProperty = playerType.GetProperty("RightInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
		}

		private static T GetProperty<T>(object instance, string propertyName)
		{
			if (instance == null)
			{
				return default(T);
			}
			PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (property == null)
			{
				return default(T);
			}
			try
			{
				object value = property.GetValue(instance, null);
				if (value is T)
				{
					return (T)value;
				}
			}
			catch
			{
			}
			return default(T);
		}

		private static T GetField<T>(object instance, string fieldName)
		{
			if (instance == null)
			{
				return default(T);
			}
			FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
			{
				return default(T);
			}
			return (T)field.GetValue(instance);
		}

		private static void SetProperty(object instance, string propertyName, object value)
		{
			if (instance != null)
			{
				PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null)
				{
					property.SetValue(instance, value, null);
				}
			}
		}

		private void InvokeInputEvent(object inputSource, object playerInput)
		{
			if (inputSource == null || playerInput == null)
			{
				return;
			}
			if (_inputSourceEventMethod == null || _inputSourceEventMethod.DeclaringType != inputSource.GetType())
			{
				_inputSourceEventMethod = inputSource.GetType().GetMethod("Event", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_inputSourceEventMethod == null)
			{
				return;
			}
			try
			{
				_inputSourceEventMethod.Invoke(inputSource, new object[1] { playerInput });
			}
			catch
			{
			}
		}
	}
}
