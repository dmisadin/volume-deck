using NAudio.CoreAudioApi;
using System.Data;
using System.Diagnostics;
using System.IO.Ports;
using VolumeDeck.Models;
using VolumeDeck.Models.Enums;

class Program
{
    static string PortName = "COM4";
    static int BaudRate = 9600;
    static float VolumeStep = 0.02f;

    static readonly object _lock = new();
    static List<SessionItem> _sessions = new();
    static int _selectedIndex = 0;

    static SerialPort? _port;
    static Timer? _refreshTimer;

    static HashSet<string> ProcessesUsingLastSessionOnly = ["Discord"];
    static HashSet<string> ExcludedSessions = ["steam"];

    static void Main()
    {
        Console.WriteLine("Per-App Volume (Buttons)");
        Console.WriteLine("D2 Prev | D3 Next | D4 Vol- | D5 Vol+");
        Console.WriteLine();

        RefreshSessions(print: true);

        _refreshTimer = new Timer(_ => RefreshSessions(print: false), null, 2000, 2000);

        _port = new SerialPort(PortName, BaudRate)
        {
            NewLine = "\n",
            ReadTimeout = 500
        };

        _port.DataReceived += (_, __) =>
        {
            try
            {
                string line = _port!.ReadLine().Trim();
                HandleSerialLine(line);
            }
            catch { }
        };

        try
        {
            _port.Open();
            Console.WriteLine($"Listening on {PortName} @ {BaudRate} baud");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to open port:");
            Console.WriteLine(ex.Message);
            Console.WriteLine("Tip: Close Arduino Serial Monitor/Plotter (only one app can use the COM port).");
            return;
        }

        Console.WriteLine("Press ENTER to quit.");
        Console.ReadLine();

        _port.Close();
        _refreshTimer.Dispose();
    }

    static void HandleSerialLine(string line)
    {
        if (!int.TryParse(line, out int pin))
            return;

        if (!Enum.IsDefined(typeof(SerialVolumeControl), pin))
            return;

        lock (_lock)
        {
            if (_sessions.Count == 0)
            {
                RefreshSessions(print: true);
                return;
            }

            switch ((SerialVolumeControl)pin)
            {
                case SerialVolumeControl.PreviousSession:
                    _selectedIndex = CircularNavigationByStep(_selectedIndex, -1, _sessions.Count);
                    PrintSelected();
                    break;

                case SerialVolumeControl.NextSession:
                    _selectedIndex = CircularNavigationByStep(_selectedIndex, 1, _sessions.Count);
                    PrintSelected();
                    break;

                case SerialVolumeControl.VolumeDown:
                    AdjustSelectedVolume(-VolumeStep);
                    break;

                case SerialVolumeControl.VolumeUp:
                    AdjustSelectedVolume(+VolumeStep);
                    break;
            }
        }
    }

    static int CircularNavigationByStep(int currentIndex, int step, int count)
    {
        if (count <= 0)
            return 0;

        int result = (currentIndex + step) % count;

        if (result < 0)
            result += count;

        return result;
    }

    static void RefreshSessions(bool print)
    {
        try
        {
            var sessions = GetActiveAudioSessions();

            lock (_lock)
            {
                _sessions = sessions;

                if (_sessions.Count == 0)
                {
                    _selectedIndex = 0;
                }
                else
                {
                    _selectedIndex = Math.Clamp(_selectedIndex, 0, _sessions.Count - 1);
                }
            }

            if (print)
                PrintAll();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RefreshSessions error] {ex.Message}");
        }
    }

    static void PrintAll()
    {
        lock (_lock)
        {
            Console.WriteLine();
            Console.WriteLine($"Active sessions: {_sessions.Count}");
            for (int i = 0; i < _sessions.Count; i++)
            {
                var s = _sessions[i];
                Console.WriteLine($"{(i == _selectedIndex ? ">" : " ")} [{i}] {s.DisplayName}  (Vol {Math.Round(s.Volume * 100)}%)");
            }
            Console.WriteLine();
        }
    }

    static void PrintSelected()
    {
        if (_sessions.Count == 0) return;
        var s = _sessions[_selectedIndex];
        Console.WriteLine($"> Selected: {s.DisplayName} | Vol {Math.Round(s.Volume * 100)}%");
    }

    static void AdjustSelectedVolume(float delta)
    {
        if (_sessions.Count == 0) return;

        var s = _sessions[_selectedIndex];

        float newVol = Math.Clamp(s.SimpleAudioVolume.Volume + delta, 0f, 1f);
        s.SimpleAudioVolume.Volume = newVol;

        s.Volume = newVol;

        Console.WriteLine($"> {s.DisplayName} volume -> {Math.Round(newVol * 100)}%");
    }

    static List<SessionItem> GetActiveAudioSessions()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        var mgr = device.AudioSessionManager;
        var sc = mgr.Sessions;
        var sessions = new List<SessionItem>();

        for (int i = 0; i < sc.Count; i++)
        {
            var session = sc[i];

            string name = GetNiceName(session);

            if (ExcludedSessions.Contains(name) || session.IsSystemSoundsSession)
                continue;

            var vol = session.SimpleAudioVolume.Volume;
            var existingUniqueProcess = sessions.FirstOrDefault(s => s.DisplayName == name);
            
            if (existingUniqueProcess != null && ProcessesUsingLastSessionOnly.Contains(existingUniqueProcess.DisplayName))
            {
                // Replace existing session with the latest matching process name
                existingUniqueProcess = new SessionItem
                {
                    DisplayName = name,
                    SimpleAudioVolume = session.SimpleAudioVolume,
                    Volume = vol
                };
            } else
            {
                sessions.Add(new SessionItem
                {
                    DisplayName = name,
                    SimpleAudioVolume = session.SimpleAudioVolume,
                    Volume = vol
                });
            }
        }

        return sessions.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string GetNiceName(AudioSessionControl session)
    {
        string? display = session.DisplayName;
        if (!string.IsNullOrWhiteSpace(display))
            return display;

        try
        {
            uint pid = session.GetProcessID;
            if (pid > 0)
            {
                var p = Process.GetProcessById((int)pid);
                return p.ProcessName;
            }
        }
        catch { }

        return "Unknown Session";
    }
}
