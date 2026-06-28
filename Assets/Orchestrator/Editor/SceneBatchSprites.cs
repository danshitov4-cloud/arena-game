using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneBatchSprites
    {
        private static Sprite _whiteSprite;

        private static Sprite GetWhiteSprite(float pixelsPerUnit)
        {
            if (_whiteSprite != null) return _whiteSprite;

            var tex = Texture2D.whiteTexture; // built-in
            _whiteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit <= 0 ? 100f : pixelsPerUnit
            );
            _whiteSprite.name = "Orch_WhiteSprite";
            return _whiteSprite;
        }

        // command: scene.batch.setSpriteRendererSprite
        // args:
        // {
        //   "query": { ... },                 // required
        //   "sprite": "white",               // optional, default "white"
        //   "pixelsPerUnit": 100,            // optional
        //   "dryRun": false
        // }
        public static object SetSpriteRendererSprite(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            string spriteKind = ((string?)args?["sprite"] ?? "white").Trim().ToLowerInvariant();
            float ppu = (float?)args?["pixelsPerUnit"] ?? 100f;

            if (spriteKind != "white")
                return new { ok = false, error = "Only sprite:'white' is supported for now." };

            var sprite = GetWhiteSprite(ppu);

            // используем ваш query-хелпер (он у тебя уже есть)
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]);

            int matchedObjects = gos.Length;
            int matchedRenderers = 0;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Set SpriteRenderer.sprite");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var r = go.GetComponent<SpriteRenderer>();
                        if (r == null) { skipped++; continue; }

                        matchedRenderers++;

                        bool wouldChange = r.sprite != sprite;
                        if (wouldChange)
                        {
                            Undo.RecordObject(r, "Set Sprite");
                            r.sprite = sprite;
                            EditorUtility.SetDirty(r);
                            changed++;
                        }

                        if (samples.Count < 10)
                        {
                            samples.Add(new
                            {
                                name = go.name,
                                id = go.GetInstanceID(),
                                hadSprite = r.sprite != null,
                                wouldChange
                            });
                        }
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

                    var r = go.GetComponent<SpriteRenderer>();
                    if (r == null) { skipped++; continue; }

                    matchedRenderers++;

                    bool wouldChange = r.sprite != sprite;
                    if (wouldChange) changed++;

                    if (samples.Count < 10)
                    {
                        samples.Add(new
                        {
                            name = go.name,
                            id = go.GetInstanceID(),
                            hadSprite = r.sprite != null,
                            wouldChange
                        });
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                sprite = "white",
                pixelsPerUnit = ppu,
                matchedObjects,
                matchedRenderers,
                changed,
                skipped,
                samples
            };
        }
    }
}