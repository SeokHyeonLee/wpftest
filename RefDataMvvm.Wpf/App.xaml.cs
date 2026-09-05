using System.Windows;
using RefDataMvvm.Core.Models;
using RefDataMvvm.Core.ViewModels;

namespace RefDataMvvm.Wpf
{
    public partial class App : Application
    {
        // Composition root: 실제 시스템에서는 이미 존재하는 Data 저장소를 주입한다.
        private CounterModel _model;
        private MainViewModel _viewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _model = new CounterModel();
            _viewModel = new MainViewModel(_model);
            MainWindow = new MainWindow { DataContext = _viewModel };
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _viewModel?.Dispose();
            base.OnExit(e);
        }
    }
}
