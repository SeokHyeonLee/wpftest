using System;

namespace RefDataMvvm.Core.Data
{
    /// <summary>
    /// 원본 Data를 가리키는 핸들. 별도의 값 사본을 보관하지 않는다.
    /// 기존 시스템의 API 모양을 재현하기 위해 소문자 멤버를 사용한다.
    /// </summary>
    public sealed class RefData<T>
    {
        private readonly ObservableData<T> _source;

        internal RefData(ObservableData<T> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        // 원본을 즉시 변경한다. 관찰자 알림은 changed()에서 별도로 보낸다.
        public T value
        {
            get { return _source.Value; }
            set { _source.Value = value; }
        }

        public void changed()
        {
            _source.NotifyChanged();
        }

        // 이 샘플에서는 저장되는 상태가 아닌 쓰기 전용 알림 트리거다.
        public bool changedflag
        {
            set
            {
                if (value)
                    changed();
            }
        }

        // 같은 원본을 가리키는 모든 RefData에 변경 알림이 전달된다.
        // 핸들 자체는 중계 구독을 만들지 않는다. 구독자가 직접 해제한다.
        public event EventHandler Changed
        {
            add { _source.Changed += value; }
            remove { _source.Changed -= value; }
        }
    }
}
