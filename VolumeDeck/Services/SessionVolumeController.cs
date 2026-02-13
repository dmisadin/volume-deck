using Microsoft.Extensions.Hosting;
﻿using NAudio.CoreAudioApi;
using System.Diagnostics;
using VolumeDeck.Models;
using VolumeDeck.Models.Enums;
using VolumeDeck.Services.Serial;
using VolumeDeck.Utilities;

namespace VolumeDeck.Services;

public class SessionVolumeController : BackgroundService
{
    private readonly SerialConnection serialConnection;

    private readonly object lockObj = new();
    private readonly float VolumeStep = 0.02f;
    private List<SessionItem> Sessions = new();
    private int SelectedIndex = 0;
    private Timer? RefreshTimer;

    private HashSet<string> ProcessesUsingLastSessionOnly = ["Discord"];
    private HashSet<string> ExcludedSessions = ["steam", "steamwebhelper"];

    public SessionVolumeController(SerialConnection serialConnection)
    {
        this.serialConnection = serialConnection;
        this.serialConnection.SerialLineReceived += HandleSerialLine;

        this.RefreshTimer = new Timer(_ => RefreshSessions(print: false), null, 2000, 2000);
    }

    private void RefreshSessions(bool print)
    {
        try
        {
            var sessions = GetActiveAudioSessions();

            lock (lockObj)
            {
                this.Sessions = sessions;

                if (this.Sessions.Count == 0)
                {
                    this.SelectedIndex = 0;
                }
                else
                {
                    this.SelectedIndex = Math.Clamp(this.SelectedIndex, 0, this.Sessions.Count - 1);
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

    private void HandleSerialLine(string line)
    {
        if (!int.TryParse(line, out int pin))
            return;

        if (!Enum.IsDefined(typeof(SerialVolumeControl), pin))
            return;

        switch ((SerialVolumeControl)pin)
        {
            case SerialVolumeControl.PreviousSession:
                this.SessionNavigationByStep(-1);
                break;

            case SerialVolumeControl.NextSession:
                this.SessionNavigationByStep(1);
                break;

            case SerialVolumeControl.VolumeDown:
                this.AdjustSelectedVolume(-VolumeStep);
                break;

            case SerialVolumeControl.VolumeUp:
                this.AdjustSelectedVolume(+VolumeStep);
                break;

            case SerialVolumeControl.MuteToggle:
                this.ToggleMuteSession();
                break;

            default:
                return;
        }
    }

    private void PrintAll()
    {
        lock (lockObj)
        {
            Console.WriteLine();
            Console.WriteLine($"Active sessions: {this.Sessions.Count}");
            for (int i = 0; i < this.Sessions.Count; i++)
            {
                var s = this.Sessions[i];
                Console.WriteLine($"{(i == this.SelectedIndex ? ">" : " ")} [{i}] {s.DisplayName}  (Vol {Math.Round(s.Volume * 100)}%)");
            }
            Console.WriteLine();
        }
    }

    private List<SessionItem> GetActiveAudioSessions()
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
            }
            else
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

    private void PrintSelected()
    {
        if (this.Sessions.Count == 0) return;

        var s = this.Sessions.ElementAtOrDefault(this.SelectedIndex);

        if (s == null) return;

        Console.WriteLine($"> Selected: {s.DisplayName} | Vol {Math.Round(s.Volume * 100)}%");
    }

    private void AdjustSelectedVolume(float delta)
    {
        if (this.Sessions.Count == 0) return;

        var s = this.Sessions[this.SelectedIndex];

        float newVol = Math.Clamp(s.SimpleAudioVolume.Volume + delta, 0f, 1f);
        s.SimpleAudioVolume.Volume = newVol;

        s.Volume = newVol;

        Console.WriteLine($"> {s.DisplayName} volume -> {Math.Round(newVol * 100)}%");
    }

    private void ToggleMuteSession()
    {
        if (this.Sessions.Count == 0) return;

        var s = this.Sessions[this.SelectedIndex];

        s.SimpleAudioVolume.Mute = !s.SimpleAudioVolume.Mute;
    }

    private string GetNiceName(AudioSessionControl session)
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

    private void SessionNavigationByStep(int step)
    {
        lock (lockObj)
        {
            this.SelectedIndex = CircularNavigationByStep(step);
        }
        this.PrintSelected();
    }

    private int CircularNavigationByStep(int step)
    {
        int count = this.Sessions.Count;
        if (count  <= 0)
            return 0;

        int result = (this.SelectedIndex + step) % count;

        if (result < 0)
            result += count;

        return result;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
