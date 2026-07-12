using System;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
    internal sealed class GameReflection
    {
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
        public string LastInteractorName { get; private set; }
        public string LastInteractResult { get; private set; }

        public string PlayerTypeName { get { return _playerType == null ? "not found" : _playerType.FullName; } }
        public string PlayerControllerTypeName { get { return _playerControllerType == null ? "not found" : _playerControllerType.FullName; } }
        public bool HasCurrentPlayerProperty { get { return _currentPlayerProperty != null; } }
        public bool HasCurrentPlayerControllerProperty { get { return _currentPlayerControllerProperty != null; } }
        public bool HasPlayerTransformProperty { get { return _playerTransformProperty != null; } }
        public bool HasLeftInputProperty { get { return _leftInputProperty != null; } }
        public bool HasRightInputProperty { get { return _rightInputProperty != null; } }
        public bool HasLocomotionController { get { return (_locomotionController as Component) != null; } }

        public object FindLocalPlayer()
        {
            EnsurePlayerType();
            var current = _currentPlayerProperty == null ? null : _currentPlayerProperty.GetValue(null, null);
            if (IsPlayableLocalPlayer(current))
            {
                return current;
            }

            var scenePlayer = FindScenePlayer();
            if (scenePlayer != null)
            {
                return scenePlayer;
            }

            return current;
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
                var transform = _playerTransformProperty.GetValue(player, null) as Transform;
                if (transform != null)
                {
                    return transform;
                }
            }

            var component = player as Component;
            if (component != null)
            {
                return component.transform;
            }

            return GetProperty<Transform>(player, "transform");
        }

        public Transform GetPlayerRootTransform(object player)
        {
            var playerTransform = GetPlayerTransform(player);
            if (playerTransform == null)
            {
                return null;
            }

            return playerTransform.root != null ? playerTransform.root : playerTransform;
        }

        public Camera GetPlayerCamera(object player)
        {
            if (player == null)
            {
                return null;
            }

            var camera = GetProperty<Camera>(player, "Camera");
            if (camera != null)
            {
                return camera;
            }

            var component = player as Component;
            if (component != null)
            {
                camera = component.GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    return camera;
                }
            }

            return Camera.main;
        }

        public object FindLocalController()
        {
            EnsurePlayerControllerType();
            var current = _currentPlayerControllerProperty == null ? null : _currentPlayerControllerProperty.GetValue(null, null);
            if (current != null)
            {
                return current;
            }

            return FindSceneController();
        }

        public Transform GetControllerTransform(object controller)
        {
            if (controller == null)
            {
                return null;
            }

            var component = controller as Component;
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

            var camera = GetProperty<Camera>(controller, "Camera");
            if (camera != null)
            {
                return camera;
            }

            var component = controller as Component;
            if (component != null)
            {
                camera = component.GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    return camera;
                }
            }

            return Camera.main;
        }

        public object GetLeftInput(object player)
        {
            EnsurePlayerMembers(player.GetType());
            return _leftInputProperty == null ? null : _leftInputProperty.GetValue(player, null);
        }

        public object GetRightInput(object player)
        {
            EnsurePlayerMembers(player.GetType());
            return _rightInputProperty == null ? null : _rightInputProperty.GetValue(player, null);
        }

        public PlayerInputPair FindLoosePlayerInputs()
        {
            var leftComponent = _looseInputPair.Left as Component;
            var rightComponent = _looseInputPair.Right as Component;
            if (leftComponent != null || rightComponent != null)
            {
                return _looseInputPair;
            }

            _looseInputPair = new PlayerInputPair();

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "PlayerInput")
                {
                    continue;
                }

                var handName = GetProperty<object>(behaviour, "HandIndex");
                var handText = handName == null ? string.Empty : handName.ToString().ToLowerInvariant();

                if (handText.Contains("left"))
                {
                    _looseInputPair.Left = behaviour;
                }
                else if (handText.Contains("right"))
                {
                    _looseInputPair.Right = behaviour;
                }
                else if (_looseInputPair.Left == null)
                {
                    _looseInputPair.Left = behaviour;
                }
                else if (_looseInputPair.Right == null && !object.ReferenceEquals(_looseInputPair.Left, behaviour))
                {
                    _looseInputPair.Right = behaviour;
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
            var locomotion = FindLocomotionController(player);
            if (locomotion == null)
            {
                return TryTranslateWithController(player, translation);
            }

            if (_locomotionTranslateMethod == null)
            {
                _locomotionTranslateMethod = locomotion.GetType().GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
            }
            if (_locomotionTranslateMethod == null)
            {
                return false;
            }

            try
            {
                _locomotionTranslateMethod.Invoke(locomotion, new object[] { translation });
                return true;
            }
            catch
            {
                return TryTranslateWithController(player, translation);
            }
        }

        public bool TryTranslateWithController(object controller, Vector3 translation)
        {
            var component = controller as Component;
            if (component == null)
            {
                return TryTranslateWithCharacterController(controller, translation);
            }

            var type = controller.GetType();
            var method = type.GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
            if (method == null)
            {
                method = type.GetMethod("Translate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
            }

            if (method != null)
            {
                try
                {
                    method.Invoke(controller, new object[] { translation });
                    LastInteractResult = type.Name + "." + method.Name + " called";
                    return true;
                }
                catch
                {
                }
            }

            var root = component.transform;
            if (root != null)
            {
                try
                {
                    root.position += translation;
                    LastInteractResult = "controller transform moved";
                    return true;
                }
                catch
                {
                }
            }

            return TryTranslateWithCharacterController(controller, translation);
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
            var inputSource = GetProperty<object>(rawInput, propertyName);
            SetProperty(inputSource, "State", value);
            InvokeInputEvent(inputSource, playerInput);
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

            var target = GetInputTargetTransform(playerInput);
            object closest = null;
            var closestDistance = float.MaxValue;

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "Interactor")
                {
                    continue;
                }

                var input = GetProperty<object>(behaviour, "Input");
                if (object.ReferenceEquals(input, playerInput))
                {
                    LastInteractorName = behaviour.name;
                    return behaviour;
                }

                var controller = GetProperty<object>(behaviour, "Controller");
                var controllerInput = GetProperty<object>(controller, "PlayerInput");
                if (object.ReferenceEquals(controllerInput, playerInput))
                {
                    LastInteractorName = behaviour.name;
                    return behaviour;
                }

                if (target != null)
                {
                    var distance = (behaviour.transform.position - target.position).sqrMagnitude;
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = behaviour;
                    }
                }
            }

            if (closest != null && closestDistance < 4f)
            {
                LastInteractorName = ((Component)closest).name + " (nearest)";
                return closest;
            }

            return null;
        }

        public bool TryStartInteract(object interactor, object interactable)
        {
            if (interactor == null || interactable == null)
            {
                LastInteractResult = "start failed: missing " + (interactor == null ? "interactor" : "interactable");
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
                _startInteractMethod.Invoke(interactor, new object[] { interactable, false, true, true });
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
                _stopInteractMethod.Invoke(interactor, new object[] { false, true });
                LastInteractResult = "stop called";
                return true;
            }
            catch
            {
                LastInteractResult = "stop threw";
                return false;
            }
        }

        public bool IsInteractorInteracting(object interactor)
        {
            if (interactor == null)
            {
                return false;
            }

            var value = GetProperty<object>(interactor, "IsInteracting");
            return value is bool && (bool)value;
        }

        public bool TryPickupGrab(object interactor, object pickup)
        {
            if (interactor == null || pickup == null)
            {
                LastInteractResult = "pickup grab failed: missing " + (interactor == null ? "interactor" : "pickup");
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
                _startInteractPickupMethod.Invoke(interactor, new object[] { pickup, false, false, false });
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
            var isDocked = GetProperty<object>(pickup, "IsDocked");
            if (!(isDocked is bool) || !(bool)isDocked)
            {
                return;
            }

            var undock = pickup.GetType().GetMethod("Undock", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (undock == null)
            {
                return;
            }

            try
            {
                undock.Invoke(pickup, new object[] { interactor, true, 1 });
            }
            catch
            {
            }
        }

        private void TryResetTimeout(object interactor)
        {
            var resetTimeout = interactor.GetType().GetMethod("ResetTimeout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (resetTimeout == null)
            {
                return;
            }

            try
            {
                resetTimeout.Invoke(interactor, null);
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

            var method = GetTestGrabMethod(interactable.GetType(), isLeft, false);
            if (method == null)
            {
                LastInteractResult = "test grab failed: no method";
                return false;
            }

            try
            {
                method.Invoke(interactable, null);
                LastInteractResult = isLeft ? "TestLeftHand called" : "TestRightHand called";
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

            var method = GetTestGrabMethod(interactable.GetType(), isLeft, true);
            if (method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(interactable, null);
                LastInteractResult = isLeft ? "TestLeftHandEnd called" : "TestRightHandEnd called";
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

            var type = menuTarget.GetType();
            var typeName = type.Name;
            if (typeName == "CaptainsWheel" && TryAdjustFloatProperty(menuTarget, "Value", direction * 0.2f))
            {
                LastInteractResult = "CaptainsWheel.Value adjusted";
                return true;
            }

            if (typeName == "WheelGrab" && TryAdjustFloatProperty(menuTarget, "Progress", direction * 0.2f))
            {
                LastInteractResult = "WheelGrab.Progress adjusted";
                return true;
            }

            var menu = typeName == "ServerSelectionMenu" ? menuTarget : FindParentComponent(menuTarget as Component, "ServerSelectionMenu");
            if (menu != null && TryCallScrollServerListFromWheel(menu, direction))
            {
                LastInteractResult = "ServerSelectionMenu wheel scroll called";
                return true;
            }

            LastInteractResult = "menu wheel failed: " + typeName;
            return false;
        }

        public bool TryReturnToMainMenu()
        {
            var modeManagerType = FindType("GameModeManager");
            if (modeManagerType != null)
            {
                var stopCurrentModeAsync = modeManagerType.GetMethod("StopCurrentModeAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (stopCurrentModeAsync != null)
                {
                    try
                    {
                        stopCurrentModeAsync.Invoke(null, new object[] { "return to menu", true });
                        LastInteractResult = "GameModeManager.StopCurrentModeAsync called";
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            var methodNames = new[] { "Disconnect", "LeaveServer", "ReturnToMainMenu", "QuitToMainMenu", "BackToMenu", "LoadMainMenu" };

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                foreach (var methodName in methodNames)
                {
                    var method = behaviour.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null || method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    try
                    {
                        method.Invoke(behaviour, null);
                        LastInteractResult = methodName + " called";
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            LastInteractResult = "disconnect failed: no main-menu method";
            return false;
        }

        public bool TryToggleSceneBoolean(string[] typeNameHints, string[] memberNames)
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (!TypeMatchesAny(behaviour.GetType(), typeNameHints))
                {
                    continue;
                }

                if (TryToggleBooleanMember(behaviour, memberNames))
                {
                    LastInteractResult = behaviour.GetType().Name + " toggled";
                    return true;
                }
            }

            LastInteractResult = "toggle failed: no matching scene member";
            return false;
        }

        public bool TryInvokeSceneMethod(string[] typeNameHints, string[] methodNames)
        {
            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (!TypeMatchesAny(behaviour.GetType(), typeNameHints))
                {
                    continue;
                }

                var type = behaviour.GetType();
                foreach (var methodName in methodNames)
                {
                    var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null || method.GetParameters().Length != 0)
                    {
                        continue;
                    }

                    try
                    {
                        method.Invoke(behaviour, null);
                        LastInteractResult = type.Name + "." + methodName + " called";
                        return true;
                    }
                    catch
                    {
                    }
                }
            }

            LastInteractResult = "invoke failed: no matching scene method";
            return false;
        }

        public bool TryRunQuickAccessAction(string typeName, string assetName, string openMethodName, string runMethodName)
        {
            return TryRunQuickAccessAction(
                string.IsNullOrEmpty(typeName) ? null : new[] { typeName },
                string.IsNullOrEmpty(assetName) ? null : new[] { assetName },
                string.IsNullOrEmpty(openMethodName) ? null : new[] { openMethodName },
                string.IsNullOrEmpty(runMethodName) ? null : new[] { runMethodName });
        }

        public bool TryRunQuickAccessAction(string[] typeNameHints, string[] assetNameHints, string[] openMethodNames, string[] runMethodNames)
        {
            var target = FindResourceObject(typeNameHints, assetNameHints);
            if (target == null)
            {
                LastInteractResult = "quick access failed: " + DescribeHints(typeNameHints, assetNameHints) + " not found";
                return false;
            }

            if (openMethodNames != null)
            {
                for (var i = 0; i < openMethodNames.Length; i++)
                {
                    InvokeIfExists(target, openMethodNames[i], null);
                }
            }

            if (runMethodNames != null)
            {
                for (var i = 0; i < runMethodNames.Length; i++)
                {
                    var runMethodName = runMethodNames[i];
                    if (InvokeIfExists(target, runMethodName, new object[] { null }))
                    {
                        LastInteractResult = target.GetType().Name + "." + runMethodName + " called";
                        return true;
                    }

                    if (InvokeIfExists(target, runMethodName, null))
                    {
                        LastInteractResult = target.GetType().Name + "." + runMethodName + " called";
                        return true;
                    }
                }
            }

            LastInteractResult = "quick access failed: " + target.GetType().Name;
            return false;
        }

        public bool TrySetShowNames(bool enabled)
        {
            var target = FindResourceObject("ShowNames", null);
            if (target == null)
            {
                LastInteractResult = "ShowNames asset not found";
                return false;
            }

            var method = target.GetType().GetMethod("Activate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                LastInteractResult = "ShowNames.Activate not found";
                return false;
            }

            try
            {
                method.Invoke(target, new object[] { enabled });
                LastInteractResult = "ShowNames set to " + enabled;
                return true;
            }
            catch
            {
                LastInteractResult = "ShowNames activate threw";
                return false;
            }
        }

        private static bool TryAdjustFloatProperty(object target, string propertyName, float delta)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanRead || !property.CanWrite)
            {
                return false;
            }

            try
            {
                var oldValue = (float)property.GetValue(target, null);
                property.SetValue(target, oldValue + delta, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryToggleBooleanMember(object target, string[] memberNames)
        {
            var type = target.GetType();
            foreach (var memberName in memberNames)
            {
                var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanRead && property.CanWrite && property.PropertyType == typeof(bool))
                {
                    try
                    {
                        var oldValue = (bool)property.GetValue(target, null);
                        property.SetValue(target, !oldValue, null);
                        return true;
                    }
                    catch
                    {
                    }
                }

                var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    try
                    {
                        var oldValue = (bool)field.GetValue(target);
                        field.SetValue(target, !oldValue);
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
                for (var i = 0; i < typeNameHints.Length; i++)
                {
                    var hint = typeNameHints[i];
                    if (string.IsNullOrEmpty(hint))
                    {
                        continue;
                    }

                    if (type.Name == hint || type.FullName == hint)
                    {
                        return true;
                    }

                    if ((type.Name != null && type.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (type.FullName != null && type.FullName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }

                type = type.BaseType;
            }

            return false;
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }

                var types = assembly.GetTypes();
                for (var i = 0; i < types.Length; i++)
                {
                    var candidate = types[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.Name == typeName || candidate.FullName == typeName ||
                        (candidate.Name != null && candidate.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (candidate.FullName != null && candidate.FullName.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return candidate;
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
            return FindResourceObject(
                string.IsNullOrEmpty(typeNameHint) ? null : new[] { typeNameHint },
                string.IsNullOrEmpty(nameHint) ? null : new[] { nameHint });
        }

        private static object FindResourceObject(string[] typeNameHints, string[] nameHints)
        {
            var objects = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
            for (var i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj == null)
                {
                    continue;
                }

                var type = obj.GetType();
                if (!TypeMatchesAny(type, typeNameHints))
                {
                    continue;
                }

                if (!NameMatchesAny(obj.name, nameHints))
                {
                    continue;
                }

                return obj;
            }

            return null;
        }

        private static bool NameMatchesAny(string value, string[] nameHints)
        {
            if (nameHints == null || nameHints.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < nameHints.Length; i++)
            {
                var hint = nameHints[i];
                if (string.IsNullOrEmpty(hint))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(value) && value.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
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

            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                var argCount = args == null ? 0 : args.Length;
                if (parameters.Length != argCount)
                {
                    continue;
                }

                try
                {
                    method.Invoke(target, args);
                    return true;
                }
                catch
                {
                }
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
            var method = menu.GetType().GetMethod("ScrollServerListFromWheel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(menu, new object[] { 0f, direction * 0.2f });
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

            foreach (var behaviour in component.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == typeName)
                {
                    return behaviour;
                }
            }

            return null;
        }

        private object FindLocomotionController(object player)
        {
            var cached = _locomotionController as Component;
            if (cached != null)
            {
                return _locomotionController;
            }

            var playerTransform = GetPlayerTransform(player);
            if (playerTransform == null)
            {
                return null;
            }

            foreach (var behaviour in playerTransform.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == "PlayerLocomotionController")
                {
                    _locomotionController = behaviour;
                    return behaviour;
                }
            }

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "PlayerLocomotionController")
                {
                    _locomotionController = behaviour;
                    return behaviour;
                }
            }

            return null;
        }

        private bool TryTranslateWithCharacterController(object player, Vector3 translation)
        {
            var component = player as Component;
            if (component == null)
            {
                return false;
            }

            var controller = component.GetComponent("CharacterController");
            if (controller != null)
            {
                try
                {
                    controller.GetType().GetMethod("Move", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null).Invoke(controller, new object[] { translation });
                    LastInteractResult = "character controller moved";
                    return true;
                }
                catch
                {
                }
            }

            var rigidbody = component.GetComponent("Rigidbody");
            if (rigidbody != null)
            {
                try
                {
                    var movePosition = rigidbody.GetType().GetMethod("MovePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null);
                    if (movePosition != null)
                    {
                        var positionProperty = rigidbody.GetType().GetProperty("position", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        var currentPosition = positionProperty == null ? component.transform.position : (Vector3)positionProperty.GetValue(rigidbody, null);
                        movePosition.Invoke(rigidbody, new object[] { currentPosition + translation });
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

            object fallback = null;

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType() != _playerType)
                {
                    continue;
                }

                if (IsPlayableLocalPlayer(behaviour))
                {
                    return behaviour;
                }

                if (fallback == null && IsPlayablePlayerCandidate(behaviour))
                {
                    fallback = behaviour;
                }
            }

            return fallback;
        }

        private object FindSceneController()
        {
            EnsurePlayerControllerType();
            if (_playerControllerType == null)
            {
                return null;
            }

            foreach (var behaviour in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType() == _playerControllerType)
                {
                    return behaviour;
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

            if (ReadBoolMember(player, "IsLocal") ||
                ReadBoolMember(player, "IsLocalPlayer") ||
                ReadBoolMember(player, "IsOwned") ||
                ReadBoolMember(player, "IsOwner") ||
                ReadBoolMember(player, "HasAuthority") ||
                ReadBoolMember(player, "IsMine"))
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

            var transform = GetPlayerTransform(player);
            if (transform == null)
            {
                return false;
            }

            var left = GetLeftInput(player);
            var right = GetRightInput(player);
            if (left != null || right != null)
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

            var type = instance.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                try
                {
                    var value = property.GetValue(instance, null);
                    if (value is bool)
                    {
                        return (bool)value;
                    }
                }
                catch
                {
                }
            }

            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    var value = field.GetValue(instance);
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

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _playerType = assembly.GetType("Player");
                if (_playerType != null)
                {
                    break;
                }
            }

            _currentPlayerProperty = _playerType == null ? null : _playerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void EnsurePlayerControllerType()
        {
            if (_playerControllerType != null)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                _playerControllerType = assembly.GetType("PlayerController");
                if (_playerControllerType != null)
                {
                    break;
                }
            }

            _currentPlayerControllerProperty = _playerControllerType == null ? null : _playerControllerType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void EnsurePlayerMembers(Type playerType)
        {
            if (_playerTransformProperty != null)
            {
                return;
            }

            _playerTransformProperty = playerType.GetProperty("PlayerTransform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _leftInputProperty = playerType.GetProperty("LeftInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _rightInputProperty = playerType.GetProperty("RightInput", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            if (instance == null)
            {
                return default(T);
            }

            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return default(T);
            }

            return (T)property.GetValue(instance, null);
        }

        private static void SetProperty(object instance, string propertyName, object value)
        {
            if (instance == null)
            {
                return;
            }

            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(instance, value, null);
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
                _inputSourceEventMethod.Invoke(inputSource, new[] { playerInput });
            }
            catch
            {
            }
        }

        internal sealed class PlayerInputPair
        {
            public object Left;
            public object Right;

            public bool HasAny
            {
                get { return Left != null || Right != null; }
            }
        }
    }
}
