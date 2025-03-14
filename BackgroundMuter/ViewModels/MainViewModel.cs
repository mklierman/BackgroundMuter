using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using BackgroundMuter.Helpers;
using BackgroundMuter.Models;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using HWND = System.IntPtr;

namespace BackgroundMuter.ViewModels;

public class MainViewModel : ViewModelBase
{
    WinEventDelegate eventDelegate = null;
    delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

    [DllImport("kernel32.dll")]
    static extern int GetProcessId(IntPtr handle);

    [DllImport("USER32.DLL")]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

    [DllImport("USER32.DLL")]
    private static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("USER32.DLL")]
    private static extern int GetWindowTextLength(HWND hWnd);

    [DllImport("USER32.DLL")]
    private static extern bool IsWindowVisible(HWND hWnd);

    [DllImport("USER32.DLL")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetForegroundWindow();

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
                    processItem.DisplayName = process.ProcessName + " | " + process.MainWindowTitle;
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
