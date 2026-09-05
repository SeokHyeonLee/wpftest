using System;
using RefDataMvvm.Core.Models;
using RefDataMvvm.Core.Mvvm;

namespace RefDataMvvm.Core.ViewModels
{
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        private readonly CounterModel _model;
        private bool _disposed;

        public MainViewModel(CounterModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Left = new CounterViewModel(model, "왼쪽 패널");
            Right = new CounterViewModel(model, "오른쪽 패널", "#7C3AED");
            RecreateRightCommand = new RelayCommand(RecreateRight, () => !_disposed);
        }

        public CounterViewModel Left { get; }
        public CounterViewModel Right { get; private set; }
        public RelayCommand RecreateRightCommand { get; }

        private void RecreateRight()
        {
            Right.Dispose();
            Right = new CounterViewModel(_model, "오른쪽 패널", "#7C3AED");
            OnPropertyChanged(nameof(Right));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Left.Dispose();
            Right.Dispose();
            RecreateRightCommand.RaiseCanExecuteChanged();
        }
    }
}
