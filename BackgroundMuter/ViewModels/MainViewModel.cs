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

public class MainViewModel : ViewModelBase
{
    WinEventDelegate eventDelegate;
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

    public ReactiveCommand<Unit, Unit> UnMuteAllCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshListCommand { get; }

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

    // Remove mutes from everything when shutting down the application
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

    // Called when the Checked status changes on the process in the collection
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

    // Unmutes all processes in the Processes collection.
    public void PerformUnMuteAll()
    {
        foreach (var process in Processes)
        {
            SetMuteForProcesses(process.Process, false);
        }
    }

    private void PerformRefreshList()
    {
        PopulateProcessList();
    }

    // Method called when the current window focus has been changed
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

    private ObservableCollection<ProcessItemModel> processes = [];
    public ObservableCollection<ProcessItemModel> Processes
    { 
        get => processes; 
        set 
        {
            processes = value;
        } 
    }

    // Some processes that can be safely excluded every time
    private List<string> ExcludedProcesses = ["SystemSettings", "TextInputHost"];

    /// <summary>
    /// Populate the Processess collection with open windows.
    /// </summary>
    private void PopulateProcessList()
    {
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
    /// Set the mute status for the given process
    /// and all processes with the same name.
    /// </summary>
    /// <param name="process"></param>
    /// <param name="mute"></param>
    private void SetMuteForProcesses(Process process, bool mute)
    {
        var processName = process.ProcessName;
        var ProcessList = Process.GetProcessesByName(processName);
        foreach (var proc in ProcessList)
        {
            Task.Run(() => AudioHelper.SetMute(proc, mute));
        }
    }
}
