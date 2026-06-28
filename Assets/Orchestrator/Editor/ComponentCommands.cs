using System;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ComponentCommands
    {
        // command: component.setEnabled
        // args: { "componentInstanceId": -15840, "enabled": false, "ping": false }
        public static object SetEnabled(JToken args)
        {
            int compId = (int?)args?["componentInstanceId"] ?? 0;
            bool enabled = (bool?)args?["enabled"] ?? true;
            bool ping = (bool?)args?["ping"] ?? false; // опционально, по умолчанию выключен

            if (compId == 0)
                throw new ArgumentException("args.componentInstanceId is required");

            var obj = EditorUtility.InstanceIDToObject(compId);
            if (obj == null)
                throw new InvalidOperationException($"No object for componentInstanceId={compId}");

            if (obj is not Component comp)
                throw new InvalidOperationException($"instanceId={compId} is not a Component (it is {obj.GetType().Name})");

            return SetEnabledOnComponent(comp, enabled, ping);
        }

        private static object SetEnabledOnComponent(Component comp, bool enabled, bool ping)
        {
            int compId = comp.GetInstanceID();

            if (comp is Behaviour b)
            {
                Undo.RecordObject(b, "Orchestrator Set Enabled");
                b.enabled = enabled;
                EditorUtility.SetDirty(b);
                if (ping) { Selection.activeObject = b; EditorGUIUtility.PingObject(b); }
                return Result(compId, b.GetType().FullName, b.enabled);
            }

            if (comp is Collider col)
            {
                Undo.RecordObject(col, "Orchestrator Set Enabled");
                col.enabled = enabled;
                EditorUtility.SetDirty(col);
                if (ping) { Selection.activeObject = col; EditorGUIUtility.PingObject(col); }
                return Result(compId, col.GetType().FullName, col.enabled);
            }

            if (comp is Renderer rend)
            {
                Undo.RecordObject(rend, "Orchestrator Set Enabled");
                rend.enabled = enabled;
                EditorUtility.SetDirty(rend);
                if (ping) { Selection.activeObject = rend; EditorGUIUtility.PingObject(rend); }
                return Result(compId, rend.GetType().FullName, rend.enabled);
            }

            throw new InvalidOperationException(
                $"Component type {comp.GetType().FullName} does not support enabled toggle.");
        }

        private static object Result(int compId, string typeName, bool enabled) => new
        {
            updated = new
            {
                componentInstanceId = compId,
                type = typeName,
                enabled
            }
        };
    }
}