using System.Windows;
using System.Windows.Controls;

namespace RefDataMvvm.Wpf.Views
{
    public partial class CounterView : UserControl
    {
        // 표시 방식 선택은 View의 설정이며 Model 상태에 추가하지 않는다.
        public static readonly DependencyProperty UseOpacitySpinnerProperty = DependencyProperty.Register(
            nameof(UseOpacitySpinner), typeof(bool), typeof(CounterView), new PropertyMetadata(false));

        public bool UseOpacitySpinner
        {
            get { return (bool)GetValue(UseOpacitySpinnerProperty); }
            set { SetValue(UseOpacitySpinnerProperty, value); }
        }

        public CounterView() { InitializeComponent(); }
    }
}
