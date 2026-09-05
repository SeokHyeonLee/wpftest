using System;

namespace RefDataMvvm.Core.Data
{
    /// <summary>
    /// Model이 소유하는 원본 저장소. UI나 ViewModel을 알지 못한다.
    /// 이 예제의 읽기/쓰기/알림은 모두 같은 스레드에서 수행한다.
    /// </summary>
    public sealed class ObservableData<T>
    {
        private T _value;

        public ObservableData(T initialValue)
        {
            _value = initialValue;
        }

        internal T Value
        {
            get { return _value; }
            set { _value = value; }
        }

        internal event EventHandler Changed;

        public RefData<T> GetReference()
        {
            return new RefData<T>(this);
        }

        internal void NotifyChanged()
        {
            // 명시적인 알림이므로 값이 같아도 호출된다.
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
