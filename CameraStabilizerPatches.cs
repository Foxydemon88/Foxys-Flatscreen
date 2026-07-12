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
            var type = FindType("CameraStabelizer") ?? FindType("CameraStabilizer");
            if (type == null)
            {
                yield break;
            }

            var methodNames = new[] { "OnEnable", "Activate", "StabelizeCamera", "ToggleCamera", "OnDisable" };
            for (var i = 0; i < methodNames.Length; i++)
            {
                var method = type.GetMethod(methodNames[i], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
