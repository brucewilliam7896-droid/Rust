using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using RustPlus.Core.Diagnostics;

/// <summary>
/// A simple on-screen debug console for logging structured entries during play.
/// Add this component to any GameObject in your scene to see real-time log output.
/// </summary>
[RequireComponent(typeof(TextMesh))]
public sealed class DebugConsole : MonoBehaviour
{
    #pragma warning disable CS0414 // Field is assigned but never used (reserved for future auto-scroll implementation)
    [SerializeField] private int _maxLines = 50;
    [SerializeField] private float _scrollSpeed = 1f;

    private TextMesh _textMesh;
    private StringBuilder _logBuffer;
    private Queue<string> _lineHistory;

    private void Awake()
    {
        _textMesh = GetComponent<TextMesh>();
        _logBuffer = new StringBuilder();
        _lineHistory = new Queue<string>(_maxLines);
    }

    private void OnEnable()
    {
        StructuredLogger.LogReceived += OnLogReceived;
    }

    private void OnDisable()
    {
        StructuredLogger.LogReceived -= OnLogReceived;
    }

    private void OnLogReceived(LogSeverity severity, string category, string message, string context)
    {
        string formatted = $"[{severity.ToString().ToUpperInvariant()}][{category}] {message}";
        if (!string.IsNullOrWhiteSpace(context))
        {
            formatted += $" | Context={context}";
        }

        // Add to buffer
        _logBuffer.AppendLine(formatted);

        // Keep history queue in sync
        if (_lineHistory.Count >= _maxLines)
        {
            _lineHistory.Dequeue();
        }
        _lineHistory.Enqueue(formatted);

        // Trim buffer to max lines
        int lineCount = _logBuffer.ToString().Split('\n').Length;
        if (lineCount > _maxLines)
        {
            // Remove oldest lines
            var lines = _logBuffer.ToString().Split('\n');
            var trimmed = new string[lineCount - 1];
            Array.Copy(lines, 1, trimmed, 0, trimmed.Length);
            _logBuffer = new StringBuilder(string.Join("\n", trimmed));
        }

        // Apply to UI
        _textMesh.text = _logBuffer.ToString();
    }

    private void Update()
    {
        // Optional: auto-scroll to bottom behavior
        // Could implement fancy scrolling here
    }
}