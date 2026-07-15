using System;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
    internal sealed class LookTargeter
    {
        private const float MaxDistance = 4.0f;

        private Type _physicsType;
        private Type _raycastHitType;
        private MethodInfo _raycastMethod;
        private MethodInfo _raycastAllMethod;
        private PropertyInfo _hitPointProperty;
        private PropertyInfo _hitColliderProperty;
        private GameObject _marker;

        public Vector3 CurrentPoint { get; private set; }
        public bool HasTarget { get; private set; }
        public object CurrentPickup { get; private set; }
        public object CurrentInteractable { get; private set; }
        public object CurrentMenuTarget { get; private set; }
        public string CurrentColliderName { get; private set; }
        public string CurrentInteractableName { get; private set; }
        public string CurrentMenuTargetName { get; private set; }
        public string CurrentParentComponents { get; private set; }

        public Vector3 Update(Camera camera, Transform cameraTransform, Vector2 pointerPosition, bool cursorLocked, float fallbackDistance)
        {
            var fallback = cameraTransform.position + cameraTransform.forward * fallbackDistance;
            HasTarget = false;
            CurrentPickup = null;
            CurrentInteractable = null;
            CurrentMenuTarget = null;
            CurrentColliderName = "none";
            CurrentInteractableName = "none";
            CurrentMenuTargetName = "none";
            CurrentParentComponents = "none";
            CurrentPoint = fallback;

            EnsurePhysics();
            if (_raycastMethod == null || _raycastHitType == null)
            {
                SetMarker(false, fallback);
                return fallback;
            }

            var ray = camera != null
                ? camera.ScreenPointToRay(cursorLocked ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : pointerPosition)
                : new Ray(cameraTransform.position, cameraTransform.forward);
            FindPickupFromRaycastAll(ray);
            var hit = Activator.CreateInstance(_raycastHitType);
            var args = new object[] { ray, hit, MaxDistance };

            try
            {
                var didHit = (bool)_raycastMethod.Invoke(null, args);
                if (didHit)
                {
                    var hitPoint = _hitPointProperty == null ? null : _hitPointProperty.GetValue(args[1], null);
                    if (hitPoint is Vector3)
                    {
                        HasTarget = true;
                        CurrentPoint = (Vector3)hitPoint;
                        FindTargets(args[1]);
                    }
                }
            }
            catch
            {
                HasTarget = false;
                CurrentPoint = fallback;
            }

            SetMarker(HasTarget, CurrentPoint);
            return CurrentPoint;
        }

        private void SetMarker(bool visible, Vector3 position)
        {
            if (_marker == null)
            {
                _marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _marker.name = "Flatscreen Look Target";
                _marker.layer = 2;
                UnityEngine.Object.DontDestroyOnLoad(_marker);

                var collider = _marker.GetComponent("SphereCollider");
                if (collider != null)
                {
                    UnityEngine.Object.Destroy(collider);
                }

                var renderer = _marker.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.yellow;
                }
            }

            _marker.SetActive(visible);
            if (visible)
            {
                _marker.transform.position = position;
                _marker.transform.localScale = Vector3.one * 0.055f;
            }
        }

        private void EnsurePhysics()
        {
            if (_raycastMethod != null || _physicsType != null)
            {
                return;
            }

            TryFindPhysicsTypes();
            if (_physicsType == null || _raycastHitType == null)
            {
                try
                {
                    Assembly.Load("UnityEngine.PhysicsModule");
                }
                catch
                {
                }

                TryFindPhysicsTypes();
            }

            if (_physicsType == null || _raycastHitType == null)
            {
                return;
            }

            _hitPointProperty = _raycastHitType.GetProperty("point", BindingFlags.Instance | BindingFlags.Public);
            _hitColliderProperty = _raycastHitType.GetProperty("collider", BindingFlags.Instance | BindingFlags.Public);

            foreach (var method in _physicsType.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                var parameters = method.GetParameters();
                if (method.Name == "Raycast" &&
                    parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(Ray) &&
                    parameters[1].ParameterType.IsByRef &&
                    parameters[1].ParameterType.GetElementType() == _raycastHitType &&
                    parameters[2].ParameterType == typeof(float))
                {
                    _raycastMethod = method;
                }

                if (method.Name == "RaycastAll" &&
                    parameters.Length == 3 &&
                    parameters[0].ParameterType == typeof(Ray) &&
                    parameters[1].ParameterType == typeof(float) &&
                    parameters[2].ParameterType == typeof(int))
                {
                    _raycastAllMethod = method;
                }
            }
        }

        private void FindPickupFromRaycastAll(Ray ray)
        {
            if (_raycastAllMethod == null)
            {
                return;
            }

            try
            {
                var hits = _raycastAllMethod.Invoke(null, new object[] { ray, 10f, -1 }) as Array;
                if (hits == null)
                {
                    return;
                }

                foreach (var hit in hits)
                {
                    var pickup = FindPickup(hit);
                    if (pickup != null)
                    {
                        if (IsHeldObject(pickup))
                        {
                            continue;
                        }

                        CurrentPickup = pickup;
                        var component = pickup as Component;
                        if (component != null)
                        {
                            CurrentInteractableName = component.name + " / Pickup";
                        }

                        return;
                    }
                }
            }
            catch
            {
            }
        }

        private object FindPickup(object hit)
        {
            var collider = _hitColliderProperty == null ? null : _hitColliderProperty.GetValue(hit, null) as Component;
            if (collider == null)
            {
                return null;
            }

            foreach (var behaviour in collider.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour != null && IsTypeOrBaseNamed(behaviour.GetType(), "Pickup") && !IsHeldObject(behaviour))
                {
                    return behaviour;
                }
            }

            return null;
        }

        private void FindTargets(object hit)
        {
            var collider = _hitColliderProperty == null ? null : _hitColliderProperty.GetValue(hit, null) as Component;
            if (collider == null)
            {
                return;
            }

            CurrentColliderName = collider.name;
            CurrentParentComponents = string.Empty;
            foreach (var behaviour in collider.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (CurrentParentComponents.Length < 180)
                {
                    if (CurrentParentComponents.Length > 0)
                    {
                        CurrentParentComponents += " > ";
                    }

                    CurrentParentComponents += behaviour.GetType().Name;
                }

                if (behaviour.GetType().Name == "Interactable" && !IsHeldObject(behaviour))
                {
                    CurrentInteractableName = behaviour.name;
                    CurrentInteractable = behaviour;
                    return;
                }

                if (CurrentPickup == null && IsTypeOrBaseNamed(behaviour.GetType(), "Pickup") && !IsHeldObject(behaviour))
                {
                    CurrentPickup = behaviour;
                    CurrentInteractableName = behaviour.name + " / Pickup";
                    CurrentInteractable = behaviour;
                    return;
                }

                if (IsMenuTarget(behaviour))
                {
                    CurrentMenuTarget = behaviour;
                    CurrentMenuTargetName = behaviour.name + " / " + behaviour.GetType().Name;
                }

                var referencedInteractable = FindReferencedInteractable(behaviour);
                if (referencedInteractable != null && !IsHeldObject(referencedInteractable))
                {
                    var component = referencedInteractable as Component;
                    CurrentInteractableName = component == null ? referencedInteractable.GetType().Name : component.name;
                    CurrentInteractable = referencedInteractable;
                    return;
                }
            }
        }

        private static bool IsMenuTarget(MonoBehaviour behaviour)
        {
            var typeName = behaviour.GetType().Name;
            return typeName == "CaptainsWheel" ||
                   typeName == "WheelGrab" ||
                   typeName == "ServerSelectionMenu" ||
                   typeName == "VrMainMenu";
        }

        private static object FindReferencedInteractable(object component)
        {
            var type = component.GetType();

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!CouldBeInteractableReference(field.Name, field.FieldType))
                {
                    continue;
                }

                var value = SafeGetField(field, component);
                var interactable = ResolveInteractable(value);
                if (interactable != null)
                {
                    return interactable;
                }
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || !CouldBeInteractableReference(property.Name, property.PropertyType))
                {
                    continue;
                }

                var value = SafeGetProperty(property, component);
                var interactable = ResolveInteractable(value);
                if (interactable != null)
                {
                    return interactable;
                }
            }

            return null;
        }

        private static bool CouldBeInteractableReference(string memberName, Type memberType)
        {
            var name = memberName == null ? string.Empty : memberName.ToLowerInvariant();
            return IsInteractableLikeType(memberType) ||
                   name.Contains("interactable") ||
                   name == "pickup" ||
                   name == "handle" ||
                   name == "targetpickup" ||
                   name.Contains("slot") ||
                   name.Contains("bag") ||
                   name.Contains("pouch") ||
                   name.Contains("container") ||
                   name.Contains("inventory");
        }

        private static object ResolveInteractable(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (IsInteractableLikeType(value.GetType()))
            {
                return value;
            }

            var component = value as Component;
            if (component != null)
            {
                foreach (var behaviour in component.GetComponentsInParent<MonoBehaviour>(true))
                {
                    if (behaviour != null && IsInteractableLikeType(behaviour.GetType()))
                    {
                        return behaviour;
                    }
                }
            }

            return null;
        }

        private static bool IsInteractableLikeType(Type type)
        {
            return IsTypeOrBaseNamed(type, "Interactable") ||
                   TypeNameContains(type, "slot") ||
                   TypeNameContains(type, "bag") ||
                   TypeNameContains(type, "pouch") ||
                   TypeNameContains(type, "container") ||
                   TypeNameContains(type, "inventory");
        }

        private static bool IsTypeOrBaseNamed(Type type, string name)
        {
            while (type != null)
            {
                if (type.Name == name)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool TypeNameContains(Type type, string text)
        {
            while (type != null)
            {
                var typeName = type.Name;
                if (!string.IsNullOrEmpty(typeName) && typeName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool IsHeldObject(object target)
        {
            if (target == null)
            {
                return false;
            }

            var boolNames = new[] { "IsHeld", "Held", "IsGrabbed", "Grabbed", "InHand", "IsInteracting" };
            for (var i = 0; i < boolNames.Length; i++)
            {
                var value = ReadBoolMember(target, boolNames[i]);
                if (value.HasValue && value.Value)
                {
                    return true;
                }
            }

            var ownerRefs = new[] { "Holder", "HeldBy", "Interactor", "CurrentInteractor", "Grabber", "Owner", "Player", "Controller" };
            for (var i = 0; i < ownerRefs.Length; i++)
            {
                var owner = ReadObjectMember(target, ownerRefs[i]);
                if (owner == null)
                {
                    continue;
                }

                var localFlags = new[] { "IsLocal", "IsLocalPlayer", "IsOwner", "IsOwned", "HasAuthority", "IsMine" };
                var hasAnyLocalFlag = false;
                for (var j = 0; j < localFlags.Length; j++)
                {
                    var local = ReadBoolMember(owner, localFlags[j]);
                    if (!local.HasValue)
                    {
                        continue;
                    }

                    hasAnyLocalFlag = true;
                    if (!local.Value)
                    {
                        return true;
                    }
                }

                if (hasAnyLocalFlag)
                {
                    return false;
                }
            }

            return false;
        }

        private static bool? ReadBoolMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
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

            return null;
        }

        private static object ReadObjectMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead)
            {
                try
                {
                    return property.GetValue(instance, null);
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
                    return field.GetValue(instance);
                }
                catch
                {
                }
            }

            return null;
        }

        private static object SafeGetField(FieldInfo field, object instance)
        {
            try
            {
                return field.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private static object SafeGetProperty(PropertyInfo property, object instance)
        {
            try
            {
                return property.GetValue(instance, null);
            }
            catch
            {
                return null;
            }
        }

        private void TryFindPhysicsTypes()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (_physicsType == null)
                {
                    _physicsType = assembly.GetType("UnityEngine.Physics");
                }

                if (_raycastHitType == null)
                {
                    _raycastHitType = assembly.GetType("UnityEngine.RaycastHit");
                }
            }
        }
    }
}
