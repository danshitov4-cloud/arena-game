using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using JObject = Unity.Plastic.Newtonsoft.Json.Linq.JObject;

namespace Orchestrator.Editor
{
    [InitializeOnLoad]
    public static class OrchestratorServer
    {
        private static HttpListener? _listener;
        private static Thread? _thread;
        private static readonly object _lock = new();

        public static int Port { get; private set; } = 5137;
        public static bool IsRunning => _listener != null;

        private static readonly Queue<Action> _mainThreadQueue = new();

        static OrchestratorServer()
        {
            EditorApplication.update += PumpMainThreadQueue;
            EditorApplication.delayCall += AutoStart;
        }

        private static void AutoStart()
        {
            if (IsRunning) return;
            try { Start(Port); }
            catch (Exception ex) { Debug.LogError($"OrchestratorServer auto-start failed: {ex}"); }
        }

        public static void Start(int port = 5137)
        {
            lock (_lock)
            {
                if (_listener != null) return;

                Port = port;

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");

                try
                {
                    _listener.Start();
                }
                catch (Exception ex)
                {
                    _listener = null;
                    Debug.LogError($"OrchestratorServer: HttpListener.Start failed: {ex}");
                    throw;
                }

                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "Unity-Orchestrator-HttpServer"
                };
                _thread.Start();

                Debug.Log($"OrchestratorServer STARTED on http://127.0.0.1:{Port}/");
            }
        }

        public static void Stop()
        {
            lock (_lock)
            {
                if (_listener == null) return;

                try { _listener.Stop(); } catch { }
                try { _listener.Close(); } catch { }

                _listener = null;
                _thread = null;

                Debug.Log("OrchestratorServer STOPPED");
            }
        }

        private static void ListenLoop()
        {
            while (true)
            {
                HttpListener? listener;
                lock (_lock) listener = _listener;
                if (listener == null) break;

                try
                {
                    var ctx = listener.GetContext();
                    HandleRequest(ctx);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError("OrchestratorServer ListenLoop error: " + ex);
                }
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                if (ctx.Request.Url == null ||
                    ctx.Request.HttpMethod != "POST" ||
                    ctx.Request.Url.AbsolutePath != "/command")
                {
                    WriteJson(ctx, 404, new { ok = false, error = "Not found. Use POST /command" });
                    return;
                }

                using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = sr.ReadToEnd();

                var req = JsonConvert.DeserializeObject<CommandRequest>(body)
                          ?? throw new InvalidOperationException("Invalid JSON request");

                // Выполняем команду на главном потоке Unity
                CommandResponse? resp = null;
                Exception? error = null;

                var ev = new ManualResetEvent(false);
                EnqueueMainThread(() =>
                {
                    try
                    {
                        resp = CommandDispatcher.Execute(req);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        ev.Set();
                    }
                });

                if (!ev.WaitOne(TimeSpan.FromSeconds(10)))
                {
                    WriteJson(ctx, 500, new CommandResponse
                    {
                        id = req.id,
                        ok = false,
                        error = "Timeout waiting for main thread."
                    });
                    return;
                }

                if (error != null)
                {
                    WriteJson(ctx, 500, new CommandResponse
                    {
                        id = req.id,
                        ok = false,
                        error = error.ToString()
                    });
                    return;
                }

                if (resp == null)
                {
                    WriteJson(ctx, 500, new { ok = false, error = "No response." });
                    return;
                }

                WriteJson(ctx, 200, resp);
            }
            catch (Exception ex)
            {
                WriteJson(ctx, 500, new { ok = false, error = ex.ToString() });
            }
        }

        private static void EnqueueMainThread(Action a)
        {
            lock (_mainThreadQueue) _mainThreadQueue.Enqueue(a);
        }

        private static void PumpMainThreadQueue()
        {
            while (true)
            {
                Action? a = null;
                lock (_mainThreadQueue)
                {
                    if (_mainThreadQueue.Count == 0) break;
                    a = _mainThreadQueue.Dequeue();
                }
                a?.Invoke();
            }
        }

        private static void WriteJson(HttpListenerContext ctx, int statusCode, object payload)
        {
            var json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            var bytes = Encoding.UTF8.GetBytes(json);

            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = bytes.Length;

            using var os = ctx.Response.OutputStream;
            os.Write(bytes, 0, bytes.Length);
        }
    }

    public class CommandRequest
    {
        public string? id { get; set; }
        public string command { get; set; } = "";
        public JToken args { get; set; } = new JObject();
        
        public bool dryRun { get; set; } = false;
    }

    public class CommandResponse
    {
        public string? id { get; set; }
        public bool ok { get; set; }
        public object? result { get; set; }
        public string? error { get; set; }
    }
}