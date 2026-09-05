using System;
using RefDataMvvm.Core.Data;

namespace RefDataMvvm.Core.Models
{
    /// <summary>
    /// 원본 데이터의 소유자이며 카운터의 업무 규칙을 담당한다.
    /// 앱이 이 인스턴스를 소유하므로 ViewModel을 교체해도 데이터는 유지된다.
    /// </summary>
    public sealed class CounterModel
    {
        private readonly ObservableData<int> _clickCount = new ObservableData<int>(0);
        private readonly ObservableData<string> _information =
            new ObservableData<string>("아직 버튼을 누르지 않았습니다.");
        private readonly ObservableData<DateTime?> _lastClickedAt = new ObservableData<DateTime?>(null);
        private readonly ObservableData<bool> _isActivate = new ObservableData<bool>(false);
        private readonly Func<DateTime> _clock;

        public CounterModel() : this(() => DateTime.Now) { }

        public CounterModel(Func<DateTime> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public RefData<int> GetClickCountReference() { return _clickCount.GetReference(); }
        public RefData<string> GetInformationReference() { return _information.GetReference(); }
        public RefData<DateTime?> GetLastClickedAtReference() { return _lastClickedAt.GetReference(); }
        public RefData<bool> GetIsActivateReference() { return _isActivate.GetReference(); }

        public void RecordClick(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("버튼의 출처가 필요합니다.", nameof(source));

            RefData<int> count = GetClickCountReference();
            RefData<string> information = GetInformationReference();
            RefData<DateTime?> lastClickedAt = GetLastClickedAtReference();
            RefData<bool> isActivate = GetIsActivateReference();

            int nextCount = checked(count.value + 1);
            DateTime now = _clock();

            // 먼저 모든 원본을 변경한 뒤 알린다. 관찰자는 완성된 상태를 읽는다.
            count.value = nextCount;
            information.value = source + " 버튼을 눌렀습니다. (총 " + nextCount + "회)";
            lastClickedAt.value = now;
            isActivate.value = !isActivate.value;

            count.changed();
            information.changedflag = true;
            lastClickedAt.changed();
            isActivate.changed();
        }

        public void Reset()
        {
            RefData<int> count = GetClickCountReference();
            RefData<string> information = GetInformationReference();
            RefData<DateTime?> lastClickedAt = GetLastClickedAtReference();
            RefData<bool> isActivate = GetIsActivateReference();

            count.value = 0;
            information.value = "초기화했습니다. 버튼을 눌러 다시 시작하세요.";
            lastClickedAt.value = null;
            isActivate.value = false;

            count.changed();
            information.changedflag = true;
            lastClickedAt.changed();
            isActivate.changed();
        }
    }
}
