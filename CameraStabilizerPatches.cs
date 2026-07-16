using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FlatscreenATTMod
{
	[HarmonyPatch]
	internal static class CameraStabilizerPatches
	{
		private static IEnumerable<MethodBase> TargetMethods()
		{
			Type type = FindType("CameraStabelizer") ?? FindType("CameraStabilizer");
			if (type == null)
			{
				yield break;
			}
			string[] methodNames = new string[4] { "StabelizeCamera", "StabilizeCamera", "LateUpdate", "Update" };
			for (int i = 0; i < methodNames.Length; i++)
			{
				MethodInfo method = type.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					yield return method;
				}
			}
		}

		private static bool Prefix()
		{
			return !FlatscreenMod.SuppressOpenXRInputUpdates;
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
			}
			return null;
		}
	}
}
