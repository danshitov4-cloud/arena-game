using System;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static partial class MaterialsWorkflow
    {
        // Храним "последнюю запланированную" задачу, чтобы можно было отменять/перезапускать
        private static ScheduledRestore? _scheduled;
        public static object CancelScheduledRestore(JToken args)
        {
            bool had = _scheduled.HasValue;
            CancelScheduledRestoreInternal();
            return new { ok = true, cancelled = had };
        }

        // command: materials.workflow.applyPresetAutoTargetDelay
        // args:
        // {
        //   "preset":"highlight|red|ghost|reset",
        //   "target":"Building",
        //   "top":3,
        //   "useShared":true,
        //   "dryRun":false,
        //   "restoreAfterSeconds": 5.0,     // optional; если нет или <=0 — не планируем откат
        //   "snapshotName": "mat-before-..." // optional
        // }
        public static object ApplyPresetAutoTargetDelay(JToken args)
        {
            string preset = ((string?)args?["preset"] ?? "").Trim();
            string target = ((string?)args?["target"] ?? "").Trim();
            int top = (int?)args?["top"] ?? 3;
            bool useShared = (bool?)args?["useShared"] ?? true;
            bool dryRun = (bool?)args?["dryRun"] ?? false;
            double restoreAfterSeconds = (double?)args?["restoreAfterSeconds"] ?? 0.0;

            if (string.IsNullOrWhiteSpace(preset))
                throw new ArgumentException("args.preset is required");
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("args.target is required");
            if (top < 1) top = 1;

            string snapshotName = (string?)args?["snapshotName"]
                                  ?? $"mat-before-{preset}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            // 1) Take material snapshot
            var snapRes = MaterialSnapshotCommands.Take(JObject.FromObject(new
            {
                name = snapshotName,
                nameContains = target,
                top = top,
                useShared = useShared,
                includeAllColorAliases = true
            }));

            // Если Take вернул ok=false (ничего не найдено) — вернём как есть
            // snapRes у нас object, поэтому аккуратно извлекаем id через JObject
            var snapObj = JObject.FromObject(snapRes);
            if ((bool?)snapObj["ok"] == false)
                return new { ok = false, stage = "take", result = snapObj };

            string materialSnapshotId = (string?)snapObj["snapshot"]?["id"] ?? "";

            // 2) Apply preset (существующий инструмент)
            // Мы используем твою уже готовую команду, которая применяет пресет по авто-таргету.
            var applyRes = MaterialsToolkit.PresetsApplyAutoTarget(JObject.FromObject(new
            {
                preset = preset,
                target = target,
                top = top,
                useShared = useShared,
                dryRun = dryRun,
                autoRestore = false
            }));

            // 3) Планируем откат через N секунд (если нужно)
            object? scheduled = null;
            if (!dryRun && restoreAfterSeconds > 0.0)
            {
                CancelScheduledRestoreInternal();

                _scheduled = ScheduleRestore(materialSnapshotId, restoreAfterSeconds);
                scheduled = new
                {
                    willRestore = true,
                    restoreAfterSeconds,
                    restoreAtEditorTime = _scheduled.Value.restoreAtEditorTime
                };
            }
            else
            {
                scheduled = new { willRestore = false, restoreAfterSeconds = 0.0 };
            }

            return new
            {
                ok = true,
                workflow = new
                {
                    preset,
                    target,
                    top,
                    useShared,
                    dryRun,
                    materialSnapshotId,
                    snapshotName
                },
                apply = applyRes,
                scheduled
            };
        }

        // Доп. команда на будущее (удобно): отменить план
        private static void CancelScheduledRestoreInternal()
        {
            if (_scheduled.HasValue)
            {
                EditorApplication.update -= _scheduled.Value.tick;
                _scheduled = null;
            }
        }

        // -------- scheduler --------
        private struct ScheduledRestore
        {
            public string snapshotId;
            public double restoreAtEditorTime;
            public EditorApplication.CallbackFunction tick;
        }

        private static ScheduledRestore ScheduleRestore(string snapshotId, double seconds)
        {
            double when = EditorApplication.timeSinceStartup + seconds;

            EditorApplication.CallbackFunction tick = null; // важно для замыкания

            tick = () =>
            {
                if (!_scheduled.HasValue) { EditorApplication.update -= tick; return; }
                if (EditorApplication.timeSinceStartup < when) return;

                try
                {
                    MaterialSnapshotCommands.Restore(JObject.FromObject(new
                    {
                        id = snapshotId,
                        dryRun = false
                    }));
                }
                catch (Exception ex)
                {
                    Debug.LogError("[Orchestrator] Auto restore failed: " + ex.Message);
                }
                finally
                {
                    EditorApplication.update -= tick;
                    _scheduled = null;
                }
            };

            EditorApplication.update += tick;

            return new ScheduledRestore
            {
                snapshotId = snapshotId,
                restoreAtEditorTime = when,
                tick = tick
            };
        }
    }
}