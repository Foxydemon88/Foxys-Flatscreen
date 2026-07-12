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
            var controllerType = FindType("OpenXRInputController");
            if (controllerType == null)
            {
                return null;
            }

            return controllerType.GetMethod("UpdatePosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
