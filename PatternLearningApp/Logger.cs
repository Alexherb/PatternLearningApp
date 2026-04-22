using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PatternLearningApp
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public sealed class LogEntry
    {
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? Exception { get; init; }
        public IReadOnlyDictionary<string, object?> Context { get; init; } = new Dictionary<string, object?>();
        public string? LoggerName { get; init; }
        public int ThreadId { get; init; } = Environment.CurrentManagedThreadId;
        public string MachineName { get; init; } = Environment.MachineName;
    }

    public interface ILogSink : IDisposable
    {
        Task EmitAsync(LogEntry entry, CancellationToken ct = default);
    }

    public interface ILogFormatter
    {
        string Format(LogEntry entry);
    }

    public sealed class JsonLogFormatter : ILogFormatter
    {
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public string Format(LogEntry entry)
        {
            return JsonSerializer.Serialize(entry, _opts);
        }
    }

    public sealed class ConsoleLogSink : ILogSink
    {
        private bool _disposed;

        public Task EmitAsync(LogEntry entry, CancellationToken ct = default)
        {
        Console.WriteLine("Hello from ConsoleLogSink!");
            if (_disposed) throw new ObjectDisposedException(nameof(ConsoleLogSink));
            try
            {
                var formatted = new JsonLogFormatter().Format(entry);
                if (entry.Level == LogLevel.Error)
                    Console.Error.WriteLine(formatted);
                else
                    Console.WriteLine(formatted);
            }
            catch
            {
                // swallow
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    public sealed class LoggerConfiguration
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        public IList<ILogSink> Sinks { get; } = new List<ILogSink>();
        public ILogFormatter Formatter { get; set; } = new JsonLogFormatter();
    }

    /// <summary>
    /// Thread-safe singleton logger with levels, sinks, JSON formatting and simple scope-based context.
    /// Usage: Logger.Instance.UpdateConfiguration(cfg => { cfg.MinimumLevel = LogLevel.Debug; cfg.Sinks.Add(new ConsoleLogSink()); });
    ///        Logger.Instance.Debug("message", new Dictionary<string,object?>{{"requestId","abc"}});
    /// </summary>
    public sealed class Logger : IDisposable
    {
        private static readonly Lazy<Logger> _lazy = new(() => new Logger());
        public static Logger Instance => _lazy.Value;

        private readonly object _sync = new();
        private bool _disposed;
        private LoggerConfiguration _configuration = new LoggerConfiguration();

        private readonly AsyncLocal<Stack<IReadOnlyDictionary<string, object?>>> _scopes = new();

        private Logger()
        {
            // default to console sink for backwards compatibility
            _configuration.Sinks.Add(new ConsoleLogSink());
        }

        public LoggerConfiguration CurrentConfiguration
        {
            get
            {
                lock (_sync) { return _configuration; }
            }
        }

        public void UpdateConfiguration(Action<LoggerConfiguration> update)
        {
            if (update == null) return;
            lock (_sync)
            {
                var copy = new LoggerConfiguration
                {
                    MinimumLevel = _configuration.MinimumLevel,
                    Formatter = _configuration.Formatter
                };
                foreach (var s in _configuration.Sinks) copy.Sinks.Add(s);
                update(copy);
                _configuration = copy;
            }
        }

        private IReadOnlyDictionary<string, object?> BuildContext(IReadOnlyDictionary<string, object?>? ctx)
        {
            var dict = new Dictionary<string, object?>();
            var stack = _scopes.Value;
            if (stack != null)
            {
                foreach (var scope in stack)
                {
                    foreach (var kv in scope)
                    {
                        dict[kv.Key] = kv.Value; // last wins
                    }
                }
            }
            if (ctx != null)
            {
                foreach (var kv in ctx)
                {
                    dict[kv.Key] = kv.Value;
                }
            }
            return dict;
        }

        public void Log(LogLevel level, string message, Exception? ex = null, IReadOnlyDictionary<string, object?>? context = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(Logger));
            var cfg = CurrentConfiguration;
            if (level < cfg.MinimumLevel) return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
                Message = message ?? string.Empty,
                Exception = ex?.ToString(),
                Context = BuildContext(context)
            };

            // emit to sinks (synchronously wait to keep ordering simple)
            foreach (var sink in cfg.Sinks)
            {
                try
                {
                    sink.EmitAsync(entry).GetAwaiter().GetResult();
                }
                catch
                {
                    // swallow sink errors
                }
            }
        }

        public void Debug(string message, IReadOnlyDictionary<string, object?>? context = null) => Log(LogLevel.Debug, message, null, context);
        public void Info(string message, IReadOnlyDictionary<string, object?>? context = null) => Log(LogLevel.Info, message, null, context);
        public void Warn(string message, IReadOnlyDictionary<string, object?>? context = null) => Log(LogLevel.Warn, message, null, context);
        public void Error(string message, Exception? ex = null, IReadOnlyDictionary<string, object?>? context = null) => Log(LogLevel.Error, message, ex, context);

        public IDisposable BeginScope(IReadOnlyDictionary<string, object?> scope)
        {
            if (_scopes.Value == null) _scopes.Value = new Stack<IReadOnlyDictionary<string, object?>>();
            _scopes.Value.Push(scope ?? new Dictionary<string, object?>());
            return new ScopeHandle(_scopes);
        }

        private sealed class ScopeHandle : IDisposable
        {
            private readonly AsyncLocal<Stack<IReadOnlyDictionary<string, object?>>> _local;
            private bool _disposed;
            public ScopeHandle(AsyncLocal<Stack<IReadOnlyDictionary<string, object?>>> local) => _local = local;
            public void Dispose()
            {
                if (_disposed) return;
                var stack = _local.Value;
                if (stack != null && stack.Count > 0) stack.Pop();
                _disposed = true;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            lock (_sync)
            {
                if (_disposed) return;
                foreach (var s in _configuration.Sinks)
                {
                    try { s.Dispose(); } catch { }
                }
                _disposed = true;
            }
        }
    }
}
