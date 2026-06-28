using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneBatchTransforms
    {
        public static object PlacePrefabGrid(JToken args)
        {
            string assetPath = ((string?)args?["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required (e.g. Assets/Prefabs/X.prefab)");

            int rows = (int?)args?["rows"] ?? 0;
            int cols = (int?)args?["cols"] ?? 0;
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException("args.rows and args.cols must be > 0");

            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int maxTotal = (int?)args?["maxTotal"] ?? 5000;
            if (maxTotal < 1) maxTotal = 1;
            if (maxTotal > 50000) maxTotal = 50000;

            int total = checked(rows * cols);
            if (total > maxTotal)
                return new { ok = false, error = $"Requested rows*cols={total} exceeds maxTotal={maxTotal}" };

            string plane = ((string?)args?["plane"] ?? "xz").Trim().ToLowerInvariant();
            if (plane != "xz" && plane != "xy" && plane != "yz")
                return new { ok = false, error = "args.plane must be 'xz' or 'xy' or 'yz'." };

            bool centered = (bool?)args?["centered"] ?? false;

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ ReadVec3
            Vector3 origin = SceneUtils.ReadVec3(args?["origin"], Vector3.zero);
            Vector3 spacing = SceneUtils.ReadVec3(args?["spacing"], new Vector3(1, 0, 1));
            Vector3 rotEuler = SceneUtils.ReadVec3(args?["rotationEuler"], Vector3.zero);
            Quaternion rot = Quaternion.Euler(rotEuler);
            Vector3 scale = SceneUtils.ReadVec3(args?["scale"], Vector3.one);

            string nameTemplate = ((string?)args?["nameTemplate"] ?? "Grid_{r}_{c}").Trim();
            if (string.IsNullOrWhiteSpace(nameTemplate)) nameTemplate = "Grid_{r}_{c}";

            int parentId = (int?)args?["parentInstanceId"] ?? 0;
            string parentName = ((string?)args?["parentName"] ?? "").Trim();
            bool createParentIfMissing = (bool?)args?["createParentIfMissing"] ?? true;

            Vector3 parentPos = SceneUtils.ReadVec3(args?["parentPosition"], origin);
            Vector3 parentRotEuler = SceneUtils.ReadVec3(args?["parentRotationEuler"], Vector3.zero);
            Vector3 parentScale = SceneUtils.ReadVec3(args?["parentScale"], Vector3.one);

            bool makeParentNameUnique = (bool?)args?["makeParentNameUnique"] ?? true;

            if (parentId != 0 && !string.IsNullOrWhiteSpace(parentName))
                return new { ok = false, error = "Provide either parentInstanceId OR parentName, not both." };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return new { ok = false, error = $"Prefab not found at assetPath: {assetPath}" };

            Vector3 start = origin;
            if (centered)
            {
                float w = (cols - 1);
                float h = (rows - 1);

                Vector3 half = plane switch
                {
                    "xz" => new Vector3(spacing.x * w * 0.5f, 0f, spacing.z * h * 0.5f),
                    "xy" => new Vector3(spacing.x * w * 0.5f, spacing.y * h * 0.5f, 0f),
                    _ => new Vector3(0f, spacing.y * h * 0.5f, spacing.z * w * 0.5f)
                };

                start = origin - half;
            }

            Transform? parent = null;
            GameObject? createdParent = null;

            if (!dryRun)
            {
                if (parentId != 0)
                {
                    var po = EditorUtility.InstanceIDToObject(parentId);
                    parent = po switch
                    {
                        GameObject g => g.transform,
                        Transform t => t,
                        _ => null
                    };

                    if (parent == null)
                        return new { ok = false, error = $"parentInstanceId={parentId} is not a GameObject/Transform" };
                }
                else if (!string.IsNullOrWhiteSpace(parentName))
                {
                    // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ FindGameObjectByName
                    parent = SceneUtils.FindExactByName(parentName)?.transform;

                    if (parent == null && createParentIfMissing)
                    {
                        string finalName = makeParentNameUnique
                            ? MakeUniqueName(parentName)
                            : parentName;

                        createdParent = new GameObject(finalName);
                        Undo.RegisterCreatedObjectUndo(createdParent, "Create Grid Parent");

                        createdParent.transform.position = parentPos;
                        createdParent.transform.rotation = Quaternion.Euler(parentRotEuler);
                        createdParent.transform.localScale = parentScale;

                        parent = createdParent.transform;
                    }
                }
            }

            int created = 0;
            var sample = new List<object>(Mathf.Min(10, total));

            if (dryRun)
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var pos = CalcGridPos(start, spacing, plane, r, c);
                        created++;
                        if (sample.Count < 10)
                            // ֵֵַָּֽֽ־: SceneUtils.Vec3ToObj גלוסעמ כמךאכüםמדמ ToObj
                            sample.Add(new { r, c, name = FormatName(nameTemplate, r, c), position = SceneUtils.Vec3ToObj(pos) });
                    }

                return new
                {
                    ok = true,
                    dryRun = true,
                    assetPath,
                    rows,
                    cols,
                    totalPlanned = created,
                    plane,
                    centered,
                    origin = SceneUtils.Vec3ToObj(origin),
                    start = SceneUtils.Vec3ToObj(start),
                    spacing = SceneUtils.Vec3ToObj(spacing),
                    rotationEuler = SceneUtils.Vec3ToObj(rotEuler),
                    scale = SceneUtils.Vec3ToObj(scale),
                    parent = new
                    {
                        parentInstanceId = parentId,
                        parentName,
                        createParentIfMissing,
                        wouldCreateParent = parentId == 0 && !string.IsNullOrWhiteSpace(parentName) && createParentIfMissing,
                        parentPosition = SceneUtils.Vec3ToObj(parentPos),
                        parentRotationEuler = SceneUtils.Vec3ToObj(parentRotEuler),
                        parentScale = SceneUtils.Vec3ToObj(parentScale)
                    },
                    samples = sample
                };
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator PlacePrefabGrid");

            try
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var pos = CalcGridPos(start, spacing, plane, r, c);

                        var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                        if (instance == null) continue;

                        Undo.RegisterCreatedObjectUndo(instance, "Create Prefab Grid Item");

                        if (parent != null)
                            instance.transform.SetParent(parent, worldPositionStays: true);

                        instance.transform.position = pos;
                        instance.transform.rotation = rot;
                        instance.transform.localScale = scale;
                        instance.name = FormatName(nameTemplate, r, c);

                        created++;
                        if (sample.Count < 10)
                        {
                            sample.Add(new
                            {
                                r,
                                c,
                                name = instance.name,
                                instanceId = instance.GetInstanceID(),
                                position = SceneUtils.Vec3ToObj(pos)
                            });
                        }
                    }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                ok = true,
                dryRun = false,
                assetPath,
                rows,
                cols,
                createdCount = created,
                plane,
                centered,
                origin = SceneUtils.Vec3ToObj(origin),
                start = SceneUtils.Vec3ToObj(start),
                spacing = SceneUtils.Vec3ToObj(spacing),
                rotationEuler = SceneUtils.Vec3ToObj(rotEuler),
                scale = SceneUtils.Vec3ToObj(scale),
                parent = new
                {
                    usedParentInstanceId = parent != null ? parent.gameObject.GetInstanceID() : 0,
                    requestedParentInstanceId = parentId,
                    requestedParentName = parentName,
                    createdParent = createdParent != null,
                    createdParentInstanceId = createdParent != null ? createdParent.GetInstanceID() : 0,
                    createdParentName = createdParent != null ? createdParent.name : null
                },
                samples = sample
            };
        }

        // MakeUniqueName מסעאגכום — סןוצטפטקום הכÿ ‎עמדמ פאיכא
        private static string MakeUniqueName(string baseName)
        {
            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ FindGameObjectByName
            if (SceneUtils.FindExactByName(baseName) == null) return baseName;

            for (int i = 2; i < 10000; i++)
            {
                string n = $"{baseName} ({i})";
                if (SceneUtils.FindExactByName(n) == null) return n;
            }

            return $"{baseName} ({Guid.NewGuid().ToString("N")[..6]})";
        }

        private static Vector3 CalcGridPos(Vector3 start, Vector3 spacing, string plane, int r, int c)
        {
            return plane switch
            {
                "xz" => new Vector3(start.x + c * spacing.x, start.y, start.z + r * spacing.z),
                "xy" => new Vector3(start.x + c * spacing.x, start.y + r * spacing.y, start.z),
                _ => new Vector3(start.x, start.y + r * spacing.y, start.z + c * spacing.z),
            };
        }

        private static string FormatName(string template, int r, int c)
        {
            return template
                .Replace("{r}", r.ToString())
                .Replace("{c}", c.ToString())
                .Replace("{r1}", (r + 1).ToString())
                .Replace("{c1}", (c + 1).ToString());
        }

        public static object SnapToGrid(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            string space = ((string?)args?["space"] ?? "world").Trim().ToLowerInvariant();
            bool isLocal = space == "local";

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            int sampleLimit = (int?)args?["sampleLimit"] ?? 10;
            if (sampleLimit < 0) sampleLimit = 0;
            if (sampleLimit > 50) sampleLimit = 50;

            Vector3 step = ReadGridStep(args?["gridStep"]);
            if (step.x <= 0 || step.y <= 0 || step.z <= 0)
                return new { ok = false, error = "gridStep must be > 0 (number or {x,y,z})." };

            bool snapX = (bool?)args?["snapX"] ?? true;
            bool snapY = (bool?)args?["snapY"] ?? false;
            bool snapZ = (bool?)args?["snapZ"] ?? true;

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ ReadVec3
            Vector3 offset = SceneUtils.ReadVec3(args?["offset"], Vector3.zero);

            string mode = ((string?)args?["mode"] ?? "nearest").Trim().ToLowerInvariant();
            if (mode != "nearest" && mode != "floor" && mode != "ceil")
                return new { ok = false, error = "mode must be 'nearest' or 'floor' or 'ceil'." };

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(sampleLimit);

            float Snap(float v, float step1, float off, string m)
            {
                float t = (v - off) / step1;
                float n = m == "floor" ? Mathf.Floor(t) : m == "ceil" ? Mathf.Ceil(t) : Mathf.Round(t);
                return n * step1 + off;
            }

            Vector3 SnapVec(Vector3 v)
            {
                if (snapX) v.x = Snap(v.x, step.x, offset.x, mode);
                if (snapY) v.y = Snap(v.y, step.y, offset.y, mode);
                if (snapZ) v.z = Snap(v.z, step.z, offset.z, mode);
                return v;
            }

            bool WouldChange(Transform t)
            {
                var cur = isLocal ? t.localPosition : t.position;
                var next = SnapVec(cur);
                return (cur - next).sqrMagnitude > 1e-12f;
            }

            void Apply(Transform t)
            {
                var cur = isLocal ? t.localPosition : t.position;
                var next = SnapVec(cur);
                if (isLocal) t.localPosition = next;
                else t.position = next;
            }

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator SnapToGrid");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }
                        var t = go.transform;
                        if (t == null) { skipped++; continue; }

                        var before = (sampleLimit > 0 && samples.Count < sampleLimit) ? CaptureTransform(t) : null;
                        if (!WouldChange(t)) continue;

                        Undo.RecordObject(t, "Snap To Grid");
                        Apply(t);
                        changed++;
                        EditorUtility.SetDirty(t);

                        if (before != null)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), before, after = CaptureTransform(t) });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var t = go.transform;
                    if (t == null) { skipped++; continue; }

                    bool wc = WouldChange(t);
                    if (wc) changed++;

                    if (sampleLimit > 0 && samples.Count < sampleLimit)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), current = CaptureTransform(t), wouldChange = wc });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                space = isLocal ? "local" : "world",
                mode,
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                grid = new
                {
                    step = SceneUtils.Vec3ToObj(step),
                    offset = SceneUtils.Vec3ToObj(offset),
                    snapX,
                    snapY,
                    snapZ
                },
                samples
            };
        }

        public static object SetAxis(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            string space = ((string?)args?["space"] ?? "world").Trim().ToLowerInvariant();
            bool isLocal = space == "local";

            string axis = ((string?)args?["axis"] ?? "").Trim().ToLowerInvariant();
            if (axis != "x" && axis != "y" && axis != "z")
                return new { ok = false, error = "args.axis must be 'x' or 'y' or 'z'." };

            if (args?["value"] == null)
                return new { ok = false, error = "args.value is required." };

            float value = (float?)args?["value"] ?? 0f;

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            int sampleLimit = (int?)args?["sampleLimit"] ?? 10;
            if (sampleLimit < 0) sampleLimit = 0;
            if (sampleLimit > 50) sampleLimit = 50;

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(sampleLimit);

            static float GetAxis(Vector3 v, string a) => a == "x" ? v.x : (a == "y" ? v.y : v.z);
            static Vector3 SetAxisValue(Vector3 v, string a, float val)
            {
                if (a == "x") v.x = val;
                else if (a == "y") v.y = val;
                else v.z = val;
                return v;
            }

            bool WouldChange(Transform t)
            {
                var cur = isLocal ? t.localPosition : t.position;
                return Mathf.Abs(GetAxis(cur, axis) - value) > 1e-6f;
            }

            void Apply(Transform t)
            {
                var cur = isLocal ? t.localPosition : t.position;
                var next = SetAxisValue(cur, axis, value);
                if (isLocal) t.localPosition = next;
                else t.position = next;
            }

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator SetAxis");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }
                        var t = go.transform;
                        if (t == null) { skipped++; continue; }

                        var before = (sampleLimit > 0 && samples.Count < sampleLimit) ? CaptureTransform(t) : null;
                        if (!WouldChange(t)) continue;

                        Undo.RecordObject(t, "Set Axis");
                        Apply(t);
                        changed++;
                        EditorUtility.SetDirty(t);

                        if (before != null)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), before, after = CaptureTransform(t) });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var t = go.transform;
                    if (t == null) { skipped++; continue; }

                    bool wc = WouldChange(t);
                    if (wc) changed++;

                    if (sampleLimit > 0 && samples.Count < sampleLimit)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), current = CaptureTransform(t), wouldChange = wc });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                space = isLocal ? "local" : "world",
                axis,
                value,
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                samples
            };
        }

        public static object SetTransform(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            string space = ((string?)args?["space"] ?? "world").Trim().ToLowerInvariant();
            bool isLocal = space == "local";

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            int sampleLimit = (int?)args?["sampleLimit"] ?? 10;
            if (sampleLimit < 0) sampleLimit = 0;
            if (sampleLimit > 50) sampleLimit = 50;

            bool setPos = args?["position"] != null && args["position"]!.Type != JTokenType.Null;
            bool setRot = args?["rotationEuler"] != null && args["rotationEuler"]!.Type != JTokenType.Null;
            bool setScale = args?["scale"] != null && args["scale"]!.Type != JTokenType.Null;

            if (!setPos && !setRot && !setScale)
                return new { ok = false, error = "Nothing to set: provide position and/or rotationEuler and/or scale." };

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ ReadVec3
            Vector3 targetPos = setPos ? SceneUtils.ReadVec3(args["position"], Vector3.zero) : Vector3.zero;
            Vector3 targetRotEuler = setRot ? SceneUtils.ReadVec3(args["rotationEuler"], Vector3.zero) : Vector3.zero;
            Vector3 targetScale = setScale ? SceneUtils.ReadVec3(args["scale"], Vector3.one) : Vector3.one;
            Quaternion targetRot = Quaternion.Euler(targetRotEuler);

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(sampleLimit);

            bool WouldChange(Transform t)
            {
                if (setPos && (isLocal ? t.localPosition : t.position - targetPos).sqrMagnitude > 1e-10f) return true;
                if (setRot && Quaternion.Angle(isLocal ? t.localRotation : t.rotation, targetRot) > 0.0001f) return true;
                if (setScale && (t.localScale - targetScale).sqrMagnitude > 1e-10f) return true;
                return false;
            }

            void Apply(Transform t)
            {
                if (setPos) { if (isLocal) t.localPosition = targetPos; else t.position = targetPos; }
                if (setRot) { if (isLocal) t.localRotation = targetRot; else t.rotation = targetRot; }
                if (setScale) t.localScale = targetScale;
            }

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator SetTransform");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }
                        var t = go.transform;
                        if (t == null) { skipped++; continue; }

                        var before = (sampleLimit > 0 && samples.Count < sampleLimit) ? CaptureTransform(t) : null;
                        if (!WouldChange(t)) continue;

                        Undo.RecordObject(t, "Set Transform");
                        Apply(t);
                        changed++;
                        EditorUtility.SetDirty(t);

                        if (before != null)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), before, after = CaptureTransform(t) });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var t = go.transform;
                    if (t == null) { skipped++; continue; }

                    bool wc = WouldChange(t);
                    if (wc) changed++;

                    if (sampleLimit > 0 && samples.Count < sampleLimit)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), current = CaptureTransform(t), wouldChange = wc });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                space = isLocal ? "local" : "world",
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                set = new
                {
                    position = setPos ? SceneUtils.Vec3ToObj(targetPos) : null,
                    rotationEuler = setRot ? SceneUtils.Vec3ToObj(targetRotEuler) : null,
                    scale = setScale ? SceneUtils.Vec3ToObj(targetScale) : null
                },
                samples
            };
        }

        public static object OffsetTransform(JToken args)
        {
            if (args?["query"] == null)
                throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            string space = ((string?)args?["space"] ?? "world").Trim().ToLowerInvariant();
            bool isLocal = space == "local";

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            int sampleLimit = (int?)args?["sampleLimit"] ?? 10;
            if (sampleLimit < 0) sampleLimit = 0;
            if (sampleLimit > 50) sampleLimit = 50;

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ כמךאכüםמדמ ReadVec3
            Vector3 posDelta = SceneUtils.ReadVec3(args?["positionDelta"], Vector3.zero);
            Vector3 rotDeltaEuler = SceneUtils.ReadVec3(args?["rotationDeltaEuler"], Vector3.zero);
            Vector3 scaleMul = SceneUtils.ReadVec3(args?["scaleMul"], Vector3.one);
            Vector3 scaleDelta = SceneUtils.ReadVec3(args?["scaleDelta"], Vector3.zero);

            bool anyOp = posDelta != Vector3.zero || rotDeltaEuler != Vector3.zero
                      || scaleMul != Vector3.one || scaleDelta != Vector3.zero;

            if (!anyOp)
                return new { ok = false, error = "Nothing to do: all deltas are zero and scaleMul is (1,1,1)." };

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(sampleLimit);

            Quaternion rotDeltaQ = rotDeltaEuler != Vector3.zero
                ? Quaternion.Euler(rotDeltaEuler)
                : Quaternion.identity;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator OffsetTransform");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }
                        var t = go.transform;
                        if (t == null) { skipped++; continue; }

                        var before = (sampleLimit > 0 && samples.Count < sampleLimit) ? CaptureTransform(t) : null;

                        if (!WouldChange(t, isLocal, posDelta, rotDeltaEuler, scaleMul, scaleDelta)) continue;

                        Undo.RecordObject(t, "Offset Transform");
                        ApplyOffsets(t, isLocal, posDelta, rotDeltaQ, scaleMul, scaleDelta);
                        changed++;
                        EditorUtility.SetDirty(t);

                        if (before != null)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), before, after = CaptureTransform(t) });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var t = go.transform;
                    if (t == null) { skipped++; continue; }

                    bool wouldChange = WouldChange(t, isLocal, posDelta, rotDeltaEuler, scaleMul, scaleDelta);
                    if (wouldChange) changed++;

                    if (sampleLimit > 0 && samples.Count < sampleLimit)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), current = CaptureTransform(t), wouldChange });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                space = isLocal ? "local" : "world",
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                ops = new
                {
                    positionDelta = SceneUtils.Vec3ToObj(posDelta),
                    rotationDeltaEuler = SceneUtils.Vec3ToObj(rotDeltaEuler),
                    scaleMul = SceneUtils.Vec3ToObj(scaleMul),
                    scaleDelta = SceneUtils.Vec3ToObj(scaleDelta)
                },
                samples
            };
        }

        private static void ApplyOffsets(Transform t, bool isLocal, Vector3 posDelta, Quaternion rotDelta, Vector3 scaleMul, Vector3 scaleDelta)
        {
            if (posDelta != Vector3.zero)
            {
                if (isLocal) t.localPosition += posDelta;
                else t.position += posDelta;
            }

            if (rotDelta != Quaternion.identity)
            {
                if (isLocal) t.localRotation = t.localRotation * rotDelta;
                else t.rotation = t.rotation * rotDelta;
            }

            if (scaleMul != Vector3.one)
            {
                var s = t.localScale;
                t.localScale = new Vector3(s.x * scaleMul.x, s.y * scaleMul.y, s.z * scaleMul.z);
            }

            if (scaleDelta != Vector3.zero)
                t.localScale += scaleDelta;
        }

        private static bool WouldChange(Transform t, bool isLocal, Vector3 posDelta, Vector3 rotDeltaEuler, Vector3 scaleMul, Vector3 scaleDelta)
        {
            if (posDelta != Vector3.zero) return true;
            if (rotDeltaEuler != Vector3.zero) return true;
            if (scaleMul != Vector3.one) return true;
            if (scaleDelta != Vector3.zero) return true;
            return false;
        }

        private static object CaptureTransform(Transform t) => new
        {
            worldPos = SceneUtils.Vec3ToObj(t.position),
            worldRotEuler = SceneUtils.Vec3ToObj(t.rotation.eulerAngles),
            localPos = SceneUtils.Vec3ToObj(t.localPosition),
            localRotEuler = SceneUtils.Vec3ToObj(t.localRotation.eulerAngles),
            localScale = SceneUtils.Vec3ToObj(t.localScale)
        };

        private static Vector3 ReadGridStep(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return new Vector3(1, 1, 1);

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                float s = (float)token;
                return new Vector3(s, s, s);
            }

            float x = (float?)token["x"] ?? 1f;
            float y = (float?)token["y"] ?? 1f;
            float z = (float?)token["z"] ?? 1f;
            return new Vector3(x, y, z);
        }

        // ׃ְִֵֻֽÛ: כמךאכüםûו ReadVec3, ToObj, FindGameObjectByName — עוןונü טסןמכüחףי SceneUtils
    }
}