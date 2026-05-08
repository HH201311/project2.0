using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class ValueListener
    {
        private string? _name;
        private string? _value;
        public string? Name
        {
            get => _name;
            set => _name = value;
        }

        public string? Value
        {
            get { return _value; }
            set
            {
                if (value != _value)
                {
                    var args = new EventArgs<string>();
                    args.OldValue = _value;
                    args.NewValue = value;
                    _value = value;
                    OnValueChangedEven?.Invoke(this, args);
                }
            }
        }

        public delegate void OnValueChangedEvnentHandle(object? sender, EventArgs<string> args);
        public event OnValueChangedEvnentHandle? OnValueChangedEven;
    }

    public class EventArgs<T> : EventArgs
    {
        private T? _oldValue;
        private T? _newValue;
        public T? OldValue
        {
            get => _oldValue;
            set => _oldValue = value;
        }
        public T? NewValue
        {
            get => _newValue;
            set => _newValue = value;
        }
    }
}
