using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Orchestrator.Editor
{
    public static class OptimizerPlan
    {
        // command: scene.optimize.plan
        public static object BuildPlan()
        {
            // Берём уже существующие отчёты
            var upd = SceneReports.Updates();
            var ren = RenderReports.Renderers();

            // Достаём данные через dynamic (быстро для MVP)
            // Если хочешь "строго", позже сделаем модели.
            dynamic updDyn = upd;
            dynamic renDyn = ren;

            int totalMono = (int)updDyn.summary.totalMonoBehaviours;
            int missingScripts = (int)updDyn.summary.missingScripts;
            int typesWithUpdates = (int)updDyn.summary.typesWithUpdates;

            int totalRenderers = (int)renDyn.summary.totalRenderers;
            int castShadows = (int)renDyn.summary.castShadows;
            int receiveShadows = (int)renDyn.summary.receiveShadows;
            int uniqueMaterials = (int)renDyn.summary.uniqueMaterials;

            var actions = new List<object>();

            // 1) Missing scripts
            if (missingScripts > 0)
            {
                actions.Add(new
                {
                    id = "fix-missing-scripts",
                    priority = 1,
                    risk = "safe",
                    reason = $"Есть Missing Script: {missingScripts}",
                    suggestion = "Удалить/починить missing scripts (они могут давать ошибки и лишние вызовы).",
                    command = (string)null,
                    args = (object)null
                });
            }

            // 2) Update/LateUpdate/FixedUpdate — топ по количеству
            // updDyn.items — это массив анонимных объектов, но dynamic позволяет читать поля
            var updItems = new List<dynamic>();
            foreach (var it in updDyn.items) updItems.Add(it);

            // Сортируем по count
            var top = updItems.OrderByDescending(x => (int)x.count).Take(10).ToList();
            // Системные типы — не предлагаем авто-выключать
            var denyAutoDisable = new HashSet<string>(StringComparer.Ordinal)
{
    "UnityEngine.EventSystems.EventSystem",
    "UnityEngine.Rendering.Universal.Light2D"
};
            foreach (var it in top)
            {
                string type = (string)it.type;
                int count = (int)it.count;
                bool hasUpdate = (bool)it.hasUpdate;
                bool hasLate = (bool)it.hasLateUpdate;
                bool hasFixed = (bool)it.hasFixedUpdate;

                bool hasAnyTick = hasUpdate || hasLate || hasFixed;

                // Системные типы — только review, без автокоманд
                if (denyAutoDisable.Contains(type))
                {
                    actions.Add(new
                    {
                        id = $"review-{type}",
                        priority = 3,
                        risk = "info",
                        reason = $"{type}: экземпляров={count} (Update={hasUpdate}, LateUpdate={hasLate}, FixedUpdate={hasFixed})",
                        suggestion = "Системный компонент. Обычно отключать нельзя. Если есть лаги — профилировать и смотреть настройки/использование.",
                        command = (string)null,
                        args = (object)null
                    });

                    continue;
                }

                // Новое правило:
                // - если >=3, или >=2 и есть Update/LateUpdate/FixedUpdate — предлагаем тестовое отключение
                bool suggestTestDisable = (count >= 3) || (count >= 2 && hasAnyTick);

                if (suggestTestDisable)
                {
                    actions.Add(new
                    {
                        id = $"test-disable-{ShortType(type)}",
                        priority = 2,
                        risk = "medium",
                        reason = $"{type}: экземпляров={count} (Update={hasUpdate}, LateUpdate={hasLate}, FixedUpdate={hasFixed})",
                        suggestion = "Для диагностики: временно отключить этот тип и посмотреть влияние на FPS/лаг.",
                        command = "scene.batchSetEnabledByType",
                        args = new { typeName = ShortType(type), enabled = false, max = 10000 }
                    });
                }
                else
                {
                    actions.Add(new
                    {
                        id = $"review-{ShortType(type)}",
                        priority = 3,
                        risk = "info",
                        reason = $"{type}: экземпляров={count} (Update={hasUpdate}, LateUpdate={hasLate}, FixedUpdate={hasFixed})",
                        suggestion = "Проверить, что этот Update действительно нужен каждый кадр. Возможно заменить на событие/таймер.",
                        command = (string)null,
                        args = (object)null
                    });
                }
            }

            // 3) Рендер-эвристики
            if (castShadows > 0 || receiveShadows > 0)
            {
                actions.Add(new
                {
                    id = "disable-shadows",
                    priority = 2,
                    risk = "safe",
                    reason = $"Shadows: cast={castShadows}, receive={receiveShadows}",
                    suggestion = "Отключить тени у рендеров, если они не нужны (даёт быстрый выигрыш).",
                    command = (string)null, // позже добавим scene.batchDisableShadows
                    args = (object)null
                });
            }

            if (uniqueMaterials > 50)
            {
                actions.Add(new
                {
                    id = "reduce-materials",
                    priority = 3,
                    risk = "info",
                    reason = $"Unique materials: {uniqueMaterials}",
                    suggestion = "Много уникальных материалов ? больше draw calls. Подумай про атласы/материал-шэринг.",
                    command = (string)null,
                    args = (object)null
                });
            }

            // Итог
            return new
            {
                summary = new
                {
                    totalMonoBehaviours = totalMono,
                    missingScripts,
                    typesWithUpdates,
                    totalRenderers,
                    castShadows,
                    receiveShadows,
                    uniqueMaterials
                },
                actions = actions
                    .OrderBy(a => (int)a.GetType().GetProperty("priority")!.GetValue(a)!)
                    .ToArray()
            };
        }

        private static string ShortType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            int idx = fullName.LastIndexOf('.');
            return idx >= 0 ? fullName[(idx + 1)..] : fullName;
        }
    }
}