using System;
using System.Reflection;
using UnityEngine;

namespace FlatscreenATTMod
{
	internal sealed class LookTargeter
	{
		private const float MaxDistance = 4f;

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
			Vector3 vector = cameraTransform.position + cameraTransform.forward * fallbackDistance;
			HasTarget = false;
			CurrentPickup = null;
			CurrentInteractable = null;
			CurrentMenuTarget = null;
			CurrentColliderName = "none";
			CurrentInteractableName = "none";
			CurrentMenuTargetName = "none";
			CurrentParentComponents = "none";
			CurrentPoint = vector;
			EnsurePhysics();
			if (_raycastMethod == null || _raycastHitType == null)
			{
				SetMarker(false, vector);
				return vector;
			}
			Ray ray = ((camera != null) ? camera.ScreenPointToRay(cursorLocked ? new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f) : pointerPosition) : new Ray(cameraTransform.position, cameraTransform.forward));
			FindPickupFromRaycastAll(ray);
			object obj = Activator.CreateInstance(_raycastHitType);
			object[] array = new object[3] { ray, obj, 4f };
			try
			{
				if ((bool)_raycastMethod.Invoke(null, array))
				{
					object obj2 = ((_hitPointProperty == null) ? null : _hitPointProperty.GetValue(array[1], null));
					if (obj2 is Vector3)
					{
						HasTarget = true;
						CurrentPoint = (Vector3)obj2;
						FindTargets(array[1]);
					}
				}
			}
			catch
			{
				HasTarget = false;
				CurrentPoint = vector;
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
				Component component = _marker.GetComponent("SphereCollider");
				if (component != null)
				{
					UnityEngine.Object.Destroy(component);
				}
				Renderer component2 = _marker.GetComponent<Renderer>();
				if (component2 != null)
				{
					component2.material.color = Color.yellow;
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
			MethodInfo[] methods = _physicsType.GetMethods(BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo in methods)
			{
				ParameterInfo[] parameters = methodInfo.GetParameters();
				if (methodInfo.Name == "Raycast" && parameters.Length == 3 && parameters[0].ParameterType == typeof(Ray) && parameters[1].ParameterType.IsByRef && parameters[1].ParameterType.GetElementType() == _raycastHitType && parameters[2].ParameterType == typeof(float))
				{
					_raycastMethod = methodInfo;
				}
				if (methodInfo.Name == "RaycastAll" && parameters.Length == 3 && parameters[0].ParameterType == typeof(Ray) && parameters[1].ParameterType == typeof(float) && parameters[2].ParameterType == typeof(int))
				{
					_raycastAllMethod = methodInfo;
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
				Array array = _raycastAllMethod.Invoke(null, new object[3] { ray, 10f, -1 }) as Array;
				if (array == null)
				{
					return;
				}
				foreach (object item in array)
				{
					object obj = FindPickup(item);
					if (obj == null || IsHeldObject(obj))
					{
						continue;
					}
					CurrentPickup = obj;
					Component component = obj as Component;
					if (component != null)
					{
						CurrentInteractableName = component.name + " / Pickup";
					}
					break;
				}
			}
			catch
			{
			}
		}

		private object FindPickup(object hit)
		{
			Component component = ((_hitColliderProperty == null) ? null : (_hitColliderProperty.GetValue(hit, null) as Component));
			if (component == null)
			{
				return null;
			}
			MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
			foreach (MonoBehaviour monoBehaviour in componentsInParent)
			{
				if (monoBehaviour != null && IsTypeOrBaseNamed(monoBehaviour.GetType(), "Pickup") && !IsHeldObject(monoBehaviour))
				{
					return monoBehaviour;
				}
			}
			return null;
		}

		private void FindTargets(object hit)
		{
			Component component = ((_hitColliderProperty == null) ? null : (_hitColliderProperty.GetValue(hit, null) as Component));
			if (component == null)
			{
				return;
			}
			CurrentColliderName = component.name;
			CurrentParentComponents = string.Empty;
			MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
			foreach (MonoBehaviour monoBehaviour in componentsInParent)
			{
				if (monoBehaviour == null)
				{
					continue;
				}
				if (CurrentParentComponents.Length < 180)
				{
					if (CurrentParentComponents.Length > 0)
					{
						CurrentParentComponents += " > ";
					}
					CurrentParentComponents += monoBehaviour.GetType().Name;
				}
				if (monoBehaviour.GetType().Name == "Interactable" && !IsHeldObject(monoBehaviour))
				{
					CurrentInteractableName = monoBehaviour.name;
					CurrentInteractable = monoBehaviour;
					break;
				}
				if (CurrentPickup == null && IsTypeOrBaseNamed(monoBehaviour.GetType(), "Pickup") && !IsHeldObject(monoBehaviour))
				{
					CurrentPickup = monoBehaviour;
					CurrentInteractableName = monoBehaviour.name + " / Pickup";
					CurrentInteractable = monoBehaviour;
					break;
				}
				if (IsMenuTarget(monoBehaviour))
				{
					CurrentMenuTarget = monoBehaviour;
					CurrentMenuTargetName = monoBehaviour.name + " / " + monoBehaviour.GetType().Name;
				}
				object obj = FindReferencedInteractable(monoBehaviour);
				if (obj != null && !IsHeldObject(obj))
				{
					Component component2 = obj as Component;
					CurrentInteractableName = ((component2 == null) ? obj.GetType().Name : component2.name);
					CurrentInteractable = obj;
					break;
				}
			}
		}

		private static bool IsMenuTarget(MonoBehaviour behaviour)
		{
			string name = behaviour.GetType().Name;
			int result;
			switch (name)
			{
			default:
				result = ((name == "VrMainMenu") ? 1 : 0);
				break;
			case "CaptainsWheel":
			case "WheelGrab":
			case "ServerSelectionMenu":
				result = 1;
				break;
			}
			return (byte)result != 0;
		}

		private static object FindReferencedInteractable(object component)
		{
			Type type = component.GetType();
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (FieldInfo fieldInfo in fields)
			{
				if (CouldBeInteractableReference(fieldInfo.Name, fieldInfo.FieldType))
				{
					object value = SafeGetField(fieldInfo, component);
					object obj = ResolveInteractable(value);
					if (obj != null)
					{
						return obj;
					}
				}
			}
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (PropertyInfo propertyInfo in properties)
			{
				if (propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0 && CouldBeInteractableReference(propertyInfo.Name, propertyInfo.PropertyType))
				{
					object value = SafeGetProperty(propertyInfo, component);
					object obj = ResolveInteractable(value);
					if (obj != null)
					{
						return obj;
					}
				}
			}
			return null;
		}

		private static bool CouldBeInteractableReference(string memberName, Type memberType)
		{
			string text = ((memberName == null) ? string.Empty : memberName.ToLowerInvariant());
			int result;
			if (!IsInteractableLikeType(memberType) && !text.Contains("interactable"))
			{
				switch (text)
				{
				case "pickup":
				case "handle":
				case "targetpickup":
					goto IL_0090;
				}
				if (!text.Contains("slot") && !text.Contains("bag") && !text.Contains("pouch") && !text.Contains("container"))
				{
					result = (text.Contains("inventory") ? 1 : 0);
					goto IL_0091;
				}
			}
			goto IL_0090;
			IL_0090:
			result = 1;
			goto IL_0091;
			IL_0091:
			return (byte)result != 0;
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
			Component component = value as Component;
			if (component != null)
			{
				MonoBehaviour[] componentsInParent = component.GetComponentsInParent<MonoBehaviour>(true);
				foreach (MonoBehaviour monoBehaviour in componentsInParent)
				{
					if (monoBehaviour != null && IsInteractableLikeType(monoBehaviour.GetType()))
					{
						return monoBehaviour;
					}
				}
			}
			return null;
		}

		private static bool IsInteractableLikeType(Type type)
		{
			return IsTypeOrBaseNamed(type, "Interactable") || TypeNameContains(type, "slot") || TypeNameContains(type, "bag") || TypeNameContains(type, "pouch") || TypeNameContains(type, "container") || TypeNameContains(type, "inventory");
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
				string name = type.Name;
				if (!string.IsNullOrEmpty(name) && name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
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
			string[] array = new string[6] { "IsHeld", "Held", "IsGrabbed", "Grabbed", "InHand", "IsInteracting" };
			for (int i = 0; i < array.Length; i++)
			{
				bool? flag = ReadBoolMember(target, array[i]);
				if (flag.HasValue && flag.Value)
				{
					return true;
				}
			}
			string[] array2 = new string[8] { "Holder", "HeldBy", "Interactor", "CurrentInteractor", "Grabber", "Owner", "Player", "Controller" };
			for (int i = 0; i < array2.Length; i++)
			{
				object obj = ReadObjectMember(target, array2[i]);
				if (obj == null)
				{
					continue;
				}
				string[] array3 = new string[6] { "IsLocal", "IsLocalPlayer", "IsOwner", "IsOwned", "HasAuthority", "IsMine" };
				bool flag2 = false;
				for (int j = 0; j < array3.Length; j++)
				{
					bool? flag3 = ReadBoolMember(obj, array3[j]);
					if (flag3.HasValue)
					{
						flag2 = true;
						if (!flag3.Value)
						{
							return true;
						}
					}
				}
				if (flag2)
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
			return null;
		}

		private static object ReadObjectMember(object instance, string name)
		{
			if (instance == null)
			{
				return null;
			}
			Type type = instance.GetType();
			PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (Assembly assembly in assemblies)
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
