using System.Collections.Concurrent;
using System.Collections.Generic;
using RustPlus.Core.Bootstrap;
using RustPlus.Core.Diagnostics;
using RustPlus.Core.Save;
using RustPlus.Core.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RustPlus.UI.Debugging
{
    /// <summary>
    /// Development overlay (IMGUI, no scene setup needed). Toggle with the backquote key.
    /// Shows frame rate, simulation tick, session and save status, the latest structured log
    /// lines, and quick save / reload buttons for the player state.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visibleOnStart = true;
        [SerializeField, Range(5, 200)] private int maxLines = 30;

        private readonly ConcurrentQueue<StructuredLogEntry> _incoming = new ConcurrentQueue<StructuredLogEntry>();
        private readonly Queue<StructuredLogEntry> _lines = new Queue<StructuredLogEntry>();

        private bool _visible;
        private float _smoothedDeltaTime;
        private Vector2 _scroll;
        private RuntimePlayerState _player;
        private GUIStyle _lineStyle;

        public bool Visible => _visible;

        private void Awake()
        {
            _visible = visibleOnStart;
        }

        private void OnEnable()
        {
            StructuredLogger.LogReceived += OnLogReceived;
        }

        private void OnDisable()
        {
            StructuredLogger.LogReceived -= OnLogReceived;
        }

        // May be called from any thread; the queue hands entries to the main thread.
        private void OnLogReceived(StructuredLogEntry entry)
        {
            _incoming.Enqueue(entry);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
            {
                _visible = !_visible;
            }

            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime <= 0f ? Time.unscaledDeltaTime : _smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);

            while (_incoming.TryDequeue(out StructuredLogEntry entry))
            {
                _lines.Enqueue(entry);
                while (_lines.Count > maxLines)
                {
                    _lines.Dequeue();
                }
            }
        }

        /// <summary>Lines currently held for display, oldest first.</summary>
        public IEnumerable<StructuredLogEntry> Lines => _lines;

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            _lineStyle ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 12 };

            float width = Mathf.Min(560f, Screen.width - 20f);
            GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height * 0.6f), GUI.skin.box);

            float fps = _smoothedDeltaTime > 0f ? 1f / _smoothedDeltaTime : 0f;
            GameBootstrapper boot = GameBootstrapper.Instance;
            GUILayout.Label($"FPS {fps:0}   Tick {SimulationClock.CurrentTick}   Session {(boot != null ? boot.SessionId.Substring(0, 8) : "-")}");
            GUILayout.Label($"Boot save: {(boot != null && boot.BootSave != null ? boot.BootSave.Outcome.ToString() : "-")}   Telemetry: {(Telemetry.IsEnabled ? "on" : "off")}   [`] toggle");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save now"))
            {
                FindPlayer()?.Save();
            }

            if (GUILayout.Button("Reload save"))
            {
                FindPlayer()?.Load();
            }

            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (StructuredLogEntry entry in _lines)
            {
                GUILayout.Label(Format(entry), _lineStyle);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private RuntimePlayerState FindPlayer()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<RuntimePlayerState>();
            }

            return _player;
        }

        private static string Format(StructuredLogEntry entry)
        {
            string color = entry.Severity switch
            {
                LogSeverity.Error => "#ff6b6b",
                LogSeverity.Warning => "#ffd166",
                _ => "#e0e0e0"
            };

            string tick = entry.Tick >= 0 ? $"t{entry.Tick} " : string.Empty;
            return $"<color={color}>{tick}{entry}</color>";
        }
    }
}
