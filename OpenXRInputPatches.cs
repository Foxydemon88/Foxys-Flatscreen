using System;
using System.Reflection;
using HarmonyLib;

namespace FlatscreenATTMod
{
	[HarmonyPatch]
	internal static class OpenXRInputPatches
	{
		private static MethodBase TargetMethod()
		{
			Type type = FindType("OpenXRInputController");
			if (type == null)
			{
				return null;
			}
			return type.GetMethod("UpdatePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static bool Prepare()
		{
			return TargetMethod() != null;
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
