using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SUNWODA_SEVB.Tool.Helper.CCTBHelper
{
    public class DebounceWatcher : FileSystemWatcher
    {
        public DebounceWatcher(string path, string filter) : base(path, filter)
        {
            _deletedDebounceTimer.AutoReset = false; // 只执行一次
            _deletedDebounceTimer.Elapsed += (_, _) => OnDeletedElapsed();

            _createdDebounceTimer.AutoReset = false; // 只执行一次
            _createdDebounceTimer.Elapsed += (_, _) => OnCreatedElapsed();

            _changedebounceTimer.AutoReset = false; // 只执行一次
            _changedebounceTimer.Elapsed += (_, _) => OnChangedElapsed();

            _renamedDebounceTimer.AutoReset = false; // 只执行一次
            _renamedDebounceTimer.Elapsed += (_, _) => OnRenamedElapsed();

            _errorDebounceTimer.AutoReset = false; // 只执行一次
            _errorDebounceTimer.Elapsed += (_, _) => OnErrorElapsed();

            Deleted += OnDeletedChanged;
            Created += OnCreatedChanged;
            Changed += OnChangedChanged;
            Renamed += OnRenamedChanged;
            Error += OnErrorChanged;
        }

        private double interval = 100;
        public double Interval
        {
            get { return interval; }
            set
            {
                _deletedDebounceTimer.Interval = value;
                _createdDebounceTimer.Interval = value;
                _changedebounceTimer.Interval = value;
                _renamedDebounceTimer.Interval = value;
                _errorDebounceTimer.Interval = value;
                interval = value;
            }
        }

        private System.Timers.Timer _deletedDebounceTimer = new();
        private System.Timers.Timer _createdDebounceTimer = new();
        private System.Timers.Timer _changedebounceTimer = new();
        private System.Timers.Timer _renamedDebounceTimer = new();
        private System.Timers.Timer _errorDebounceTimer = new();

        private void OnDeletedChanged(object sender, FileSystemEventArgs e)
        {
            _deletedArgs = (sender, e);
            _deletedDebounceTimer!.Stop();
            _deletedDebounceTimer!.Start();
        }
        private void OnCreatedChanged(object sender, FileSystemEventArgs e)
        {
            _createdArgs = (sender, e);
            _createdDebounceTimer!.Stop();
            _createdDebounceTimer!.Start();
        }
        private void OnChangedChanged(object sender, FileSystemEventArgs e)
        {
            _changedArgs = (sender, e);
            _changedebounceTimer!.Stop();
            _changedebounceTimer!.Start();
        }
        private void OnRenamedChanged(object sender, RenamedEventArgs e)
        {
            _renamedArgs = (sender, e);
            _renamedDebounceTimer!.Stop();
            _renamedDebounceTimer!.Start();
        }
        private void OnErrorChanged(object sender, ErrorEventArgs e)
        {
            _errorArgs = (sender, e);
            _errorDebounceTimer!.Stop();
            _errorDebounceTimer!.Start();
        }

        private (object sender, FileSystemEventArgs e)? _deletedArgs;
        private (object sender, FileSystemEventArgs e)? _createdArgs;
        private (object sender, FileSystemEventArgs e)? _changedArgs;
        private (object sender, RenamedEventArgs e)? _renamedArgs;
        private (object sender, ErrorEventArgs e)? _errorArgs;

        public event FileSystemEventHandler? DeletedDebunce;
        public event FileSystemEventHandler? CreatedDebunce;
        public event FileSystemEventHandler? ChangedDebunce;
        public event RenamedEventHandler? RenamedDebunce;
        public event ErrorEventHandler? ErrorDebunce;

        private void OnDeletedElapsed()
        {
            if (_deletedArgs == null) return;
            if (_deletedArgs.Value.sender == null || _deletedArgs.Value.e == null) return;
            DeletedDebunce?.Invoke(_deletedArgs.Value.sender, _deletedArgs.Value.e);
        }

        private void OnCreatedElapsed()
        {
            if (_createdArgs == null) return;
            if (_createdArgs.Value.sender == null || _createdArgs.Value.e == null) return;
            CreatedDebunce?.Invoke(_createdArgs.Value.sender, _createdArgs.Value.e);
        }

        private void OnChangedElapsed()
        {
            if (_changedArgs == null) return;
            if (_changedArgs.Value.sender == null || _changedArgs.Value.e == null) return;
            ChangedDebunce?.Invoke(_changedArgs.Value.sender, _changedArgs.Value.e);
        }

        private void OnRenamedElapsed()
        {
            if (_renamedArgs == null) return;
            if (_renamedArgs.Value.sender == null || _renamedArgs.Value.e == null) return;
            RenamedDebunce?.Invoke(_renamedArgs.Value.sender, _renamedArgs.Value.e);
        }

        private void OnErrorElapsed()
        {
            if (_errorArgs == null) return;
            if (_errorArgs.Value.sender == null || _errorArgs.Value.e == null) return;
            ErrorDebunce?.Invoke(_errorArgs.Value.sender, _errorArgs.Value.e);
        }

        #region Dispose
        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                this.EnableRaisingEvents = false;

                this.Created -= OnCreatedChanged;
                this.Changed -= OnChangedChanged;
                this.Deleted -= OnDeletedChanged;
                this.Renamed -= OnRenamedChanged;
                this.Error -= OnErrorChanged;

                DisposeTimer(_createdDebounceTimer);
                DisposeTimer(_changedebounceTimer);
                DisposeTimer(_deletedDebounceTimer);
                DisposeTimer(_renamedDebounceTimer);
                DisposeTimer(_errorDebounceTimer);
            }

            base.Dispose(disposing);
            _disposed = true;
        }

        private void DisposeTimer(System.Timers.Timer? timer)
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
            }
        }
        #endregion
    }
}
