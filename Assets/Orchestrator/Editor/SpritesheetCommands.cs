using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SpritesheetCommands
    {
        // project.sprites.configure
        // { "path":"Assets/Sprites/Pacman/pacman_sheet.png", "pixelsPerUnit":16,
        //   "filterMode":"Point", "spriteMode":"Multiple", "dryRun":false }
        public static object Configure(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter found at: {path} — check the path and run project.assets.refresh first");

            float ppu          = (float?)args?["pixelsPerUnit"] ?? importer.spritePixelsPerUnit;
            string filterStr   = ((string?)args?["filterMode"] ?? "").Trim().ToLowerInvariant();
            string spriteModeStr = ((string?)args?["spriteMode"] ?? "Multiple").Trim();

            var filterMode = filterStr switch
            {
                "point"   => FilterMode.Point,
                "bilinear" => FilterMode.Bilinear,
                "trilinear" => FilterMode.Trilinear,
                _ => FilterMode.Point
            };

            var spriteMode = spriteModeStr.ToLowerInvariant() switch
            {
                "single"   => SpriteImportMode.Single,
                "multiple" => SpriteImportMode.Multiple,
                _          => SpriteImportMode.Multiple
            };

            if (dryRun)
                return new { ok = true, dryRun = true, path, ppu, filterMode = filterMode.ToString(), spriteMode = spriteMode.ToString() };

            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode   = spriteMode;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode         = filterMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return new { ok = true, path, ppu, filterMode = filterMode.ToString(), spriteMode = spriteMode.ToString() };
        }

        // project.sprites.slice
        // По сетке:   { "path":"...", "cellWidth":64, "cellHeight":64, "dryRun":false }
        // По рэктам:  { "path":"...", "rects":[{"x":0,"y":0,"w":64,"h":64,"name":"frame_0"},...] }
        public static object Slice(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"No TextureImporter at: {path}");
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                throw new InvalidOperationException("spriteImportMode must be Multiple — run project.sprites.configure first");

            List<SpriteMetaData> metas;

            var rectsToken = args?["rects"] as JArray;
            if (rectsToken != null && rectsToken.Count > 0)
            {
                metas = BuildMetasFromRects(rectsToken);
            }
            else
            {
                int cellW = (int?)args?["cellWidth"] ?? 0;
                int cellH = (int?)args?["cellHeight"] ?? 0;
                if (cellW <= 0 || cellH <= 0)
                    throw new ArgumentException("Provide either args.rects or args.cellWidth + args.cellHeight");

                importer.GetSourceTextureWidthAndHeight(out int texW, out int texH);
                metas = BuildMetasFromGrid(path, cellW, cellH, texW, texH);
            }

            if (dryRun)
                return new { ok = true, dryRun = true, path, spriteCount = metas.Count, sprites = metas.Select(m => new { m.name, rect = new { m.rect.x, m.rect.y, m.rect.width, m.rect.height } }) };

            importer.spritesheet = metas.ToArray();
            EditorUtility.SetDirty(importer);
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return new { ok = true, path, spriteCount = metas.Count, sprites = metas.Select(m => new { m.name }) };
        }

        // project.sprites.list
        // { "path":"Assets/Sprites/Pacman/pacman_sheet.png" }
        public static object List(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required");

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            var sprites = all.OfType<Sprite>().OrderBy(s => s.name).ToArray();

            if (sprites.Length == 0)
                return new { ok = true, path, count = 0, note = "No sub-sprites found. Run project.sprites.configure + project.sprites.slice first.", sprites = Array.Empty<object>() };

            var items = sprites.Select(s => new
            {
                name    = s.name,
                spritePath = $"{path}/{s.name}",
                rect    = new { x = s.rect.x, y = s.rect.y, w = s.rect.width, h = s.rect.height }
            }).ToArray();

            return new { ok = true, path, count = items.Length, sprites = items };
        }

        // --- helpers ---

        private static List<SpriteMetaData> BuildMetasFromGrid(string baseName, int cellW, int cellH, int texW, int texH)
        {
            string nameBase = System.IO.Path.GetFileNameWithoutExtension(baseName);
            int cols = texW / cellW;
            int rows = texH / cellH;
            var metas = new List<SpriteMetaData>();
            int index = 0;

            // Iterate top-to-bottom visually; Unity Rect y starts from bottom
            for (int row = rows - 1; row >= 0; row--)
            {
                for (int col = 0; col < cols; col++)
                {
                    metas.Add(new SpriteMetaData
                    {
                        name      = $"{nameBase}_{index}",
                        rect      = new Rect(col * cellW, row * cellH, cellW, cellH),
                        pivot     = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center
                    });
                    index++;
                }
            }
            return metas;
        }

        private static List<SpriteMetaData> BuildMetasFromRects(JArray arr)
        {
            var metas = new List<SpriteMetaData>();
            for (int i = 0; i < arr.Count; i++)
            {
                var t = arr[i];
                metas.Add(new SpriteMetaData
                {
                    name      = ((string?)t["name"] ?? $"sprite_{i}"),
                    rect      = new Rect((float?)t["x"] ?? 0, (float?)t["y"] ?? 0, (float?)t["w"] ?? 16, (float?)t["h"] ?? 16),
                    pivot     = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                });
            }
            return metas;
        }
    }
}
