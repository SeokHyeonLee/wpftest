using System.Collections.Generic;
using RefDataMvvm.Core.Mvvm;

namespace RefDataMvvm.Core.ViewModels
{
    public sealed class PopupMenuViewModel : ObservableObject
    {
        private bool _isOpen;
        private string _lastAction = "팝업을 열고 각 영역을 눌러 보세요.";

        public PopupMenuViewModel()
        {
            CloseCommand = new RelayCommand(() => IsOpen = false);
            FirstCommand = new RelayCommand(() => Select("버튼 1"));
            SecondCommand = new RelayCommand(() => Select("버튼 2"));
            Items = new List<PopupMenuItemViewModel>
            {
                new PopupMenuItemViewModel("목록 버튼 1", true, () => Select("목록 버튼 1")),
                new PopupMenuItemViewModel("사용 불가", false, () => Select("사용 불가")),
                new PopupMenuItemViewModel("목록 버튼 2", true, () => Select("목록 버튼 2"))
            }.AsReadOnly();
        }

        public bool IsOpen
        {
            get { return _isOpen; }
            set
            {
                if (_isOpen == value) return;
                _isOpen = value;
                OnPropertyChanged();
            }
        }

        public string LastAction
        {
            get { return _lastAction; }
            private set { _lastAction = value; OnPropertyChanged(); }
        }

        public IReadOnlyList<PopupMenuItemViewModel> Items { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand FirstCommand { get; }
        public RelayCommand SecondCommand { get; }

        private void Select(string title)
        {
            LastAction = title + " 실행";
            IsOpen = false;
        }
    }

    public sealed class PopupMenuItemViewModel
    {
        public PopupMenuItemViewModel(string title, bool isEnabled, System.Action execute)
        {
            Title = title;
            IsEnabled = isEnabled;
            Command = new RelayCommand(execute, () => IsEnabled);
        }

        public string Title { get; }
        public bool IsEnabled { get; }
        public RelayCommand Command { get; }
    }
}
