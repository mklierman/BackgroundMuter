using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BackgroundMuter.Models
{
    public class ProcessItemModel : INotifyPropertyChanged
    {
        private string processName;
        private string? processWindowTitle;
        private string displayName;
        private nint processHandle;
        private Process process;
        private bool isBeingWatched;

        public string ProcessName 
        { 
            get => processName;
            set => SetProperty(ref processName, value);
        }

        public string? ProcessWindowTitle 
        { 
            get => processWindowTitle;
            set => SetProperty(ref processWindowTitle, value);
        }

        public string DisplayName 
        { 
            get => displayName;
            set => SetProperty(ref displayName, value);
        }

        public nint ProcessHandle 
        { 
            get => processHandle;
            set => SetProperty(ref processHandle, value);
        }

        public Process Process 
        { 
            get => process;
            set => SetProperty(ref process, value);
        }

        public bool IsBeingWatched 
        { 
            get => isBeingWatched;
            set => SetProperty(ref isBeingWatched, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
