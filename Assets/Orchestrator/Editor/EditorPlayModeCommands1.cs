using System;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class EditorPlayModeCommands
    {
        // command: editor.play.status
        public static object Status(JToken args)
        {
            return new
            {
                ok = true,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode,
                timeSinceStartup = EditorApplication.timeSinceStartup
            };
        }

        // command: editor.play.enter
        // args: { "dryRun": false }
        public static object Enter(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (EditorApplication.isPlaying)
                return new { ok = true, changed = false, state = "already_playing" };

            if (!dryRun)
                EditorApplication.isPlaying = true;

            return new { ok = true, changed = true, state = dryRun ? "would_enter" : "entered" };
        }

        // command: editor.play.exit
        // args: { "dryRun": false }
        public static object Exit(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (!EditorApplication.isPlaying)
                return new { ok = true, changed = false, state = "already_stopped" };

            if (!dryRun)
                EditorApplication.isPlaying = false;

            return new { ok = true, changed = true, state = dryRun ? "would_exit" : "exited" };
        }

        // command: editor.play.wait
        // args: { "targetState": "playing", "timeoutSeconds": 10 }
        // targetState: "playing" | "stopped"
        public static object Wait(JToken args)
        {
            string targetState = ((string?)args?["targetState"] ?? "playing").Trim().ToLowerInvariant();
            float timeoutSeconds = (float?)args?["timeoutSeconds"] ?? 10f;

            if (targetState != "playing" && targetState != "stopped")
                return new { ok = false, error = "targetState must be 'playing' or 'stopped'" };

            bool wantPlaying = targetState == "playing";

            double start = EditorApplication.timeSinceStartup;
            double deadline = start + timeoutSeconds;

            // Поллим синхронно (Editor-only, не блокирует Unity loop)
            while (EditorApplication.timeSinceStartup < deadline)
            {
                bool currentlyPlaying = EditorApplication.isPlaying &&
                                        !EditorApplication.isPlayingOrWillChangePlaymode;
                bool currentlyStopped = !EditorApplication.isPlaying &&
                                        !EditorApplication.isPlayingOrWillChangePlaymode;

                if (wantPlaying && currentlyPlaying)
                    return new
                    {
                        ok = true,
                        reached = true,
                        state = "playing",
                        elapsedSeconds = EditorApplication.timeSinceStartup - start
                    };

                if (!wantPlaying && currentlyStopped)
                    return new
                    {
                        ok = true,
                        reached = true,
                        state = "stopped",
                        elapsedSeconds = EditorApplication.timeSinceStartup - start
                    };

                System.Threading.Thread.Sleep(100);
            }

            return new
            {
                ok = false,
                reached = false,
                timeout = true,
                targetState,
                timeoutSeconds,
                isPlaying = EditorApplication.isPlaying,
                isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode
            };
        }
    }
}