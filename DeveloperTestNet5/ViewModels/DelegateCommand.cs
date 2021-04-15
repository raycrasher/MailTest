using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace DeveloperTestNet5.ViewModels
{
    public class DelegateCommand : ICommand
    {
        private Action<object> _execute;
        private Func<object, bool> _canExecute;

        public DelegateCommand(Action execute, Func<bool> canExecute = default)
        {
            _execute = o => execute();
            if (canExecute != null)
                _canExecute = o => canExecute();
        }

        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = default)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public void FireExecuteChanged() => Application.Current?.Dispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, default));

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object parameter)
        {
            _execute?.Invoke(parameter);
        }

    }
}
