using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using BackgroundMuter.Helpers;
using BackgroundMuter.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HWND = System.IntPtr;

namespace BackgroundMuter.ViewModels;

/// <summary>
/// Main view model for the Background Muter application.
/// Handles process monitoring, audio muting, and UI state management.
/// </summary>
public class MainViewModel : ViewModelBase
{
    #region Win32 API Imports and Constants
    /// <summary>
    /// Delegate for handling Windows event callbacks.
    /// Used to monitor window focus changes.
    /// </summary>
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    
    /// <summary>
    /// Delegate for Windows enumeration callbacks.
    /// </summary>
    private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

    /// <summary>
    /// Sets up a Windows event hook to monitor system events.
    /// </summary>
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    #endregion

    #region Properties
    /// <summary>
    /// Collection of processes that can be monitored and muted.
    /// </summary>
    private ObservableCollection<ProcessItemModel> processes = [];
    public ObservableCollection<ProcessItemModel> Processes
    { 
        get => processes; 
        set => processes = value;
    }

    /// <summary>
    /// Command to unmute all currently muted processes.
    /// </summary>
    public ReactiveCommand<Unit, Unit> UnMuteAllCommand { get; }

    /// <summary>
    /// Command to refresh the list of available processes.
    /// </summary>
    public ReactiveCommand<Unit, Unit> RefreshListCommand { get; }
    #endregion

    #region Private Fields
    /// <summary>
    /// Delegate instance for handling window focus changes.
    /// </summary>
    private readonly WinEventDelegate eventDelegate;

    /// <summary>
    /// List of system processes that should be excluded from monitoring.
    /// </summary>
    private readonly List<string> ExcludedProcesses = ["SystemSettings", "TextInputHost"];
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of the MainViewModel.
    /// Sets up event hooks, commands, and initial process list.
    /// </summary>
    public MainViewModel()
    {
        UnMuteAllCommand = ReactiveCommand.Create(PerformUnMuteAll);
        RefreshListCommand = ReactiveCommand.Create(PerformRefreshList);
        
        eventDelegate = new WinEventDelegate(FocusChanged);
        IntPtr focusChangedHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, eventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        
        PopulateProcessList();
        
        if (Avalonia.Application.Current is App app)
        {
            app.ShutdownRequested += This_ShutdownRequested;
        }
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Handles application shutdown by unmuting all processes.
    /// </summary>
    private void This_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        foreach (var process in Processes)
        {
            if (process.IsBeingWatched)
            {
                SetMuteForProcesses(process.Process, false);
            }
        }
    }

    /// <summary>
    /// Handles checkbox state changes for process monitoring.
    /// Updates mute status for all processes based on their checked state.
    /// </summary>
    public void PerformCheckedChanged(RoutedEventArgs e)
    {
        foreach (var process in Processes)
        {
            if (process.IsBeingWatched)
            {
                SetMuteForProcesses(process.Process, true);
            }
            else
            {
                SetMuteForProcesses(process.Process, false);
            }
        }
    }

    /// <summary>
    /// Handles window focus changes.
    /// Mutes/unmutes processes based on whether they have focus.
    /// </summary>
    public void FocusChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        foreach (var process in Processes)
        {
            if (process.IsBeingWatched)
            {
                if (process.ProcessHandle == hwnd)
                {
                    SetMuteForProcesses(process.Process, false);
                }
                else
                {
                    SetMuteForProcesses(process.Process, true);
                }
            }
        }
    }
    #endregion

    #region Command Handlers
    /// <summary>
    /// Unmutes all processes and unchecks all checkboxes.
    /// </summary>
    public void PerformUnMuteAll()
    {
        foreach (var process in Processes)
        {
            process.IsBeingWatched = false;
            SetMuteForProcesses(process.Process, false);
        }
    }

    /// <summary>
    /// Refreshes the list of available processes.
    /// </summary>
    private void PerformRefreshList()
    {
        PopulateProcessList();
    }
    #endregion

    #region Process Management
    /// <summary>
    /// Populates the process list with all available windows.
    /// Preserves the watched state of existing processes.
    /// </summary>
    private void PopulateProcessList()
    {
        // Preserve watched items
        List<ProcessItemModel> watchedItems = new();
        foreach (var process in Processes)
        {
            if (process.IsBeingWatched)
            {
                watchedItems.Add(process);
            }
        }

        Process[] processList = Process.GetProcesses();
        List<ProcessItemModel> NewProcessList = new();

        foreach (var process in processList)
        {
            if (!ExcludedProcesses.Contains(process.ProcessName))
            {
                ProcessItemModel processItem = new();
                if (!string.IsNullOrEmpty(process.MainWindowTitle) && process.MainWindowTitle.Length > 0)
                {
                    processItem.Process = process;
                    processItem.ProcessName = process.ProcessName;
                    processItem.ProcessHandle = process.MainWindowHandle;

                    if (string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        processItem.DisplayName = process.ProcessName;
                    }
                    else
                    {
                        processItem.ProcessWindowTitle = process.MainWindowTitle;
                        if (process.ProcessName == process.MainWindowTitle)
                        {
                            processItem.DisplayName = process.ProcessName;
                        }
                        else
                        {
                            processItem.DisplayName = $"{process.MainWindowTitle} ({process.ProcessName})";
                        }
                    }

                    var match = Processes.FirstOrDefault(x => x.ProcessHandle == process.MainWindowHandle);
                    if (match != null && match.IsBeingWatched)
                    {
                        processItem.IsBeingWatched = true;
                    }

                    if (!NewProcessList.Any((ProcessItemModel item) => item.ProcessHandle.Equals(processItem.ProcessHandle)))
                    {
                        NewProcessList.Add(processItem);
                    }
                }
            }
        }

        var SortedProcessList = NewProcessList.OrderBy(x => x.DisplayName?.ToString());
        Processes.Clear();
        foreach (var item in SortedProcessList)
        {
            Processes.Add(item);
        }
    }

    /// <summary>
    /// Sets the mute status for a process and all processes with the same name.
    /// </summary>
    /// <param name="process">The process to mute/unmute</param>
    /// <param name="mute">True to mute, false to unmute</param>
    private void SetMuteForProcesses(Process process, bool mute)
    {
        var processName = process.ProcessName;
        var ProcessList = Process.GetProcessesByName(processName);
        foreach (var proc in ProcessList)
        {
            Task.Run(() => AudioHelper.SetMute(proc, mute));
        }
    }
    #endregion
}
