using System;
using RefDataMvvm.Core.Data;
using RefDataMvvm.Core.Models;
using RefDataMvvm.Core.Mvvm;

namespace RefDataMvvm.Core.ViewModels
{
    public sealed class CounterViewModel : ObservableObject, IDisposable
    {
        private readonly CounterModel _model;
        private readonly RefData<int> _clickCount;
        private readonly RefData<string> _information;
        private readonly RefData<DateTime?> _lastClickedAt;
        private readonly RefData<bool> _isActivate;
        private bool _disposed;

        public CounterViewModel(CounterModel model, string panelName, string spinnerColor = "#2463CE")
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(panelName))
                throw new ArgumentException("패널 이름이 필요합니다.", nameof(panelName));
            PanelName = panelName;
            SpinnerColor = spinnerColor;

            // 원본의 값 사본이 아니라 같은 원본을 가리키는 참조를 받는다.
            _clickCount = model.GetClickCountReference();
            _information = model.GetInformationReference();
            _lastClickedAt = model.GetLastClickedAtReference();
            _isActivate = model.GetIsActivateReference();

            ClickCommand = new RelayCommand(() => _model.RecordClick(PanelName),
                () => !_disposed && _clickCount.value < int.MaxValue);
            ResetCommand = new RelayCommand(() => _model.Reset(),
                () => !_disposed && (_clickCount.value > 0 || _isActivate.value));

            _clickCount.Changed += OnCountChanged;
            _information.Changed += OnInformationChanged;
            _lastClickedAt.Changed += OnLastClickedAtChanged;
            _isActivate.Changed += OnIsActivateChanged;
        }

        public string PanelName { get; }
        // 화면 표현용 색상. WPF Brush 타입은 Core로 가져오지 않는다.
        public string SpinnerColor { get; }

        // 바인딩용 투영. ViewModel에는 int _count 같은 중복 상태가 없다.
        public int ClickCount { get { return _clickCount.value; } }
        public bool IsActivate { get { return _isActivate.value; } }
        public string Information { get { return _information.value; } }
        public string LastClickedAtText
        {
            get
            {
                DateTime? time = _lastClickedAt.value;
                return time.HasValue ? time.Value.ToString("yyyy-MM-dd HH:mm:ss") : "기록 없음";
            }
        }

        public RelayCommand ClickCommand { get; }
        public RelayCommand ResetCommand { get; }

        private void OnCountChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            OnPropertyChanged(nameof(ClickCount));
            ClickCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
        }

        private void OnInformationChanged(object sender, EventArgs e)
        {
            if (!_disposed) OnPropertyChanged(nameof(Information));
        }

        private void OnLastClickedAtChanged(object sender, EventArgs e)
        {
            if (!_disposed) OnPropertyChanged(nameof(LastClickedAtText));
        }

        private void OnIsActivateChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            OnPropertyChanged(nameof(IsActivate));
            ResetCommand.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _clickCount.Changed -= OnCountChanged;
            _information.Changed -= OnInformationChanged;
            _lastClickedAt.Changed -= OnLastClickedAtChanged;
            _isActivate.Changed -= OnIsActivateChanged;
            ClickCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
        }
    }
}
