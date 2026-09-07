# RefData를 사용하는 WPF MVVM 테스트

.NET Framework **4.8**, C# 7.3, WPF. 외부 NuGet 패키지 없이 빌드하는 예제입니다.

## 핵심: 참조를 가지고 있는 것과 원본을 소유하는 것은 다릅니다

ViewModel에 `RefData<int>` 필드가 있어도 MVVM입니다. 그 참조가 Model의 원본을 가리키고, ViewModel이 화면용 속성과 명령을 제공하면 됩니다. `INotifyPropertyChanged`를 구현한다는 사실도 Model과 ViewModel을 구분하는 기준이 아닙니다. **데이터의 소유권과 업무 규칙의 위치**가 기준입니다.

```text
App (조립 및 수명 관리)
 ├─ CounterModel                     ← 앱 수명 동안 존재하는 Model
 │   ├─ ObservableData<int>           ← 횟수의 유일한 원본
 │   ├─ ObservableData<string>        ← 마지막 동작 정보의 원본
 │   ├─ ObservableData<DateTime?>     ← 마지막 클릭 시간의 원본
 │   └─ ObservableData<bool>          ← 스피너 활성 상태의 원본
 └─ MainViewModel
     ├─ CounterViewModel (왼쪽) ─ RefData<T> ─┐
     └─ CounterViewModel (오른쪽) ─ RefData<T> ┴─ 같은 원본 참조
          ↓ INotifyPropertyChanged / ICommand
        CounterView (XAML 바인딩)
```

ViewModel은 횟수를 별도 `int` 필드에 복사하지 않습니다.

```csharp
private readonly RefData<int> _clickCount;

// 생성자: 이미 존재하는 Model에서 참조를 얻습니다.
_clickCount = model.GetClickCountReference();
_clickCount.Changed += OnCountChanged;

// 화면이 요청할 때마다 참조를 통해 원본을 읽습니다.
public int ClickCount { get { return _clickCount.value; } }

private void OnCountChanged(object sender, EventArgs e)
{
    OnPropertyChanged(nameof(ClickCount));
}
```

`ClickCount`는 화면 바인딩을 위한 속성이고, `_clickCount.value`가 가리키는 값이 실제 원본입니다. 화면에서 보여주는 형식(`LastClickedAtText`), 패널 이름, 명령 활성 상태는 ViewModel의 책임입니다.

## 사용자가 설명한 RefData API

```csharp
RefData<int> a = model.GetClickCountReference();
a.value = 1;
a.changed();

// 또는
a.value = 2;
a.changedflag = true;
```

이 예제는 다음 계약으로 구현했습니다.

- `value`를 대입하면 **원본이 즉시 변경**됩니다. 모든 참조의 getter가 바로 새 값을 읽습니다.
- `changed()`는 원본을 관찰하는 모든 구독자에게 변경 알림을 보냅니다. WPF 화면은 이 알림을 ViewModel의 `PropertyChanged`로 전달받아 갱신합니다.
- `changedflag = true`는 `changed()`와 같은 동작입니다. 샘플에서는 쓰기 전용 트리거이며, `false` 대입은 아무 일도 하지 않습니다.
- 같은 값이어도 명시적으로 `changed()`를 호출하면 알림이 발생합니다.
- `value`만 변경하고 알림을 생략하면 화면이 즉시 갱신되지 않을 수 있습니다. 이 분리는 사용자의 API를 재현한 것입니다.

실제 RefData 구현이 제공되지 않아 `ObservableData<T>`와 `RefData<T>`는 이 계약을 재현한 샘플입니다. 실제 시스템이 `changed()` 시점에 값을 커밋하거나 flag를 나중에 처리한다면 Data 계층을 그 계약에 맞춰 교체하세요. **외부 변경을 알려주는 구독 지점**은 필요합니다. 그것이 없으면 WPF는 외부 변경을 자동으로 알 수 없습니다.

## 버튼을 눌렀을 때 흐름

1. XAML의 `Button.Command`가 `CounterViewModel.ClickCommand`를 실행합니다.
2. ViewModel은 `CounterModel.RecordClick(PanelName)`을 호출합니다.
3. Model은 `RefData<T>.value`로 횟수·정보·시간을 수정하고 bool을 토글한 뒤 `changed()` / `changedflag = true`로 알립니다.
4. 같은 원본을 보는 두 ViewModel 모두 알림을 받습니다.
5. 각 ViewModel이 관련 속성의 `PropertyChanged`를 보내면 WPF가 getter를 다시 읽습니다.

클릭 횟수 증가와 클릭 기록 생성은 업무 동작이므로 Model에 모았습니다. ViewModel 안에 원본 Data를 새로 만들거나, 자신의 `_count++`를 한 후 Model에 복사하는 로직은 없습니다. `MainWindow`와 `CounterView`에는 클릭 이벤트 핸들러가 없습니다. `CounterView.UseOpacitySpinner`는 사용할 스피너의 표시 방식을 선택하는 View 전용 DependencyProperty입니다. 두 스피너의 코드 비하인드는 점 배치와 애니메이션 수명만 담당합니다. `App.xaml.cs`는 Model과 ViewModel을 생성·연결하고 종료 시 구독을 정리합니다.

## ViewModel에서 직접 value를 쓰면 안 되나요?

**써도 됩니다.** 단순 입력을 Model에 전달하는 setter는 ViewModel의 정상적인 역할입니다. 예를 들어 편집 가능한 정수 항목은 다음처럼 노출할 수 있습니다. 아래는 설명용 코드이며 이 카운터 화면에는 횟수 직접 편집 UI가 없습니다.

```csharp
public int EditableValue
{
    get { return _editableValue.value; }
    set
    {
        if (_editableValue.value == value) return;
        _editableValue.value = value;
        _editableValue.changed();
        // 위에서 발생한 Changed 구독 핸들러에서 PropertyChanged를 보냅니다.
    }
}
```

이 경우에도 `_editableValue`는 기존 Model Data에 대한 `RefData<int>`입니다. 검증, 여러 필드의 일관성 유지, 업무 계산이 필요하면 Model 메서드가 맡도록 합니다. 이 샘플의 `RecordClick`이 그 예입니다. 외부 코드가 원시 참조를 직접 쓰면 Model의 업무 규칙도 우회할 수 있으므로, 업무 동작에는 Model 메서드를 사용하세요.

## 프로젝트

| 프로젝트 | 역할 |
| --- | --- |
| `RefDataMvvm.Core` | 원본 Data, 참조 핸들, Model, ViewModel, ICommand 및 INotifyPropertyChanged 기반 코드. WPF View/Dispatcher 의존성 없음 |
| `RefDataMvvm.Wpf` | App에서 의존성 조립, MainWindow, CounterView, LoadingSpinner, LoadingSpinner2 |
| `RefDataMvvm.Tests` | NuGet 없이 실행하는 콘솔 테스트. 실제 WPF XAML/바인딩도 STA 스레드에서 검증 |

ViewModel 재생성 시 `Dispose()`로 오래된 구독을 해제합니다. 원본은 앱이 소유하므로 그대로 유지됩니다. 여기서 유지된다는 뜻은 **실행 중 메모리 유지**이며 앱 종료 후 디스크 저장 기능은 없습니다.

읽기·쓰기·변경 알림은 WPF UI 스레드 하나에서 수행하는 예제입니다. `changedflag`는 비동기 처리를 예약하지 않습니다. 실제 Data가 백그라운드 스레드에서 갱신된다면 Data의 동시 접근 정책을 먼저 정하고, ViewModel에 주입하는 Dispatcher 어댑터 등으로 `PropertyChanged`와 `CanExecuteChanged`를 UI 스레드에 전달해야 합니다.

## 실행

Visual Studio 2022의 **.NET 데스크톱 개발** 워크로드와 **.NET Framework 4.8 개발자 팩/타기팅 팩**이 필요합니다.

1. `RefDataMvvm.sln`을 엽니다.
2. `RefDataMvvm.Wpf`를 시작 프로젝트로 설정합니다.
3. F5로 실행합니다.
4. 왼쪽·오른쪽의 **눌러서 +1**을 누릅니다. 두 화면에 같은 횟수, 마지막 버튼 출처와 시간이 표시됩니다. 버튼 우측 상단 스피너는 클릭할 때마다 켜짐/꺼짐이 토글됩니다.
5. **오른쪽 VM 다시 만들기**를 누릅니다. 숫자와 스피너 활성 상태가 유지되고 이후 알림도 계속 전달됩니다.
6. **초기화**를 누르면 두 화면이 함께 초기화되고 스피너도 꺼집니다.

Visual Studio Developer PowerShell에서 빌드와 테스트:

```powershell
msbuild .\RefDataMvvm.sln /t:Build /p:Configuration=Debug /m
.\RefDataMvvm.Tests\bin\Debug\RefDataMvvm.Tests.exe
.\RefDataMvvm.Wpf\bin\Debug\RefDataMvvm.Wpf.exe
```

기본 PowerShell에서 이 PC의 MSBuild 경로를 직접 사용할 수도 있습니다.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' .\RefDataMvvm.sln /t:Build /p:Configuration=Debug /m
```

테스트는 실제 Loaded/Unloaded 및 회전 clock을 확인하기 위해 화면 밖에 WPF 창을 만들고 종료 시 닫습니다. 실행 파일에 PNG 경로를 인자로 주면 화면 이미지도 저장합니다.

```powershell
.\RefDataMvvm.Tests\bin\Debug\RefDataMvvm.Tests.exe .\artifacts\preview.png
```

테스트 12개는 공유 참조, 명시적 알림, 일관된 Model 상태, 외부 Data 변경, 두 ViewModel 동기화, 초기화/명령 활성화, ViewModel 재생성, 구독 해제, 오버플로 보호, bool 토글, 실제 XAML 바인딩을 검증합니다. WPF 테스트 안에서 좌우에 서로 다른 스피너가 하나씩 생성되는지, 대칭/고정 중심/비선형 opacity, 크기 변경, 버튼 우측 상단 배치, 색상 바인딩 갱신, 활성화/숨김/Unloaded/재로드 시 애니메이션 시작·중단도 확인합니다. LoadingSpinner2는 시간 경과에 따라 불투명도가 바뀌는 동안 점의 화면 좌표와 크기가 그대로인지 추가 검증합니다. 실패 시 종료 코드 1을 반환합니다. 콘솔 실행 방식이므로 Visual Studio Test Explorer나 `dotnet test`에 자동 등록되지는 않습니다.

## LoadingSpinner: IsActivate와 DotBrush 바인딩

`Controls/LoadingSpinner.xaml`과 `.xaml.cs`가 재사용 가능한 UserControl입니다. 공개 속성은 C# 관례에 따라 **`IsActivate`**, **`DotBrush`**로 표기합니다. XAML 속성 이름은 대소문자를 구분합니다.

```xml
<controls:LoadingSpinner
    IsActivate="{Binding IsActivate, Mode=OneWay}"
    DotBrush="{Binding SpinnerColor, Mode=OneWay}"
    Width="32" Height="32" Background="White" />
```

| 속성 | 타입 | 동작 |
| --- | --- | --- |
| `IsActivate` | `bool` DependencyProperty | 기본 false. true면 Visible 및 회전, false면 Collapsed 및 회전 clock 제거 |
| `DotBrush` | `Brush` DependencyProperty | 8개 점의 공통 색상. 기본 DimGray. 회전 중 바인딩 갱신 가능 |
| `Background` | 상속받은 Brush 속성 | 고정된 원형 배경. 미지정 시 투명 |
| `Width` / `Height` | 상속받은 크기 속성 | 기본 32 DIP. Viewbox의 Uniform 배율로 원형 유지 |

현재 화면은 ViewModel의 `SpinnerColor` 문자열을 Brush로 변환해 바인딩합니다. 왼쪽은 파랑, 오른쪽은 보라입니다. 이 색은 화면 표현 설정이며 Core 프로젝트는 WPF Brush 타입을 참조하지 않습니다. WPF 타입을 사용하는 별도 ViewModel이라면 Brush 타입 속성 자체를 `DotBrush`에 바인딩해도 됩니다. 컨트롤은 자신의 DataContext를 설정하지 않으므로 부모의 바인딩을 그대로 받습니다.

bool의 경로는 기존 MVVM/RefData 구조와 같습니다.

```csharp
// Model.RecordClick: 원본 bool을 변경하고 알린다.
RefData<bool> active = GetIsActivateReference();
active.value = !active.value;
active.changed();

// ViewModel: 참조를 통해 읽는다. 별도 bool 사본 없음.
public bool IsActivate { get { return _isActivate.value; } }
// _isActivate.Changed → PropertyChanged(nameof(IsActivate))
```

현재는 요청한 클릭 토글 데모이므로 스피너가 실제 비동기 작업의 진행률이나 완료 상태를 뜻하지는 않습니다.

### 회전과 불투명도

- 64×64의 고정 좌표계에서 중심 `(32,32)`, 반지름 `22`, 점 지름 `8`로 구성합니다. 점 중심은 정확히 45° 간격이며 반대편 점과 대칭입니다.
- 개별 점의 Left/Top을 매 프레임 수정하지 않고 점 그룹 전체에 **하나의 RenderTransform**을 적용합니다. 1초당 360°를 등속 회전하며 360°와 0°는 같은 위치여서 반복 경계가 이어집니다.
- 소수 좌표를 그대로 쓰고 내부 `UseLayoutRounding` / `SnapsToDevicePixels`를 끕니다. 회전 중 정수 좌표 반올림으로 생기는 위치 튐을 피합니다. 벡터 원과 WPF 기본 안티앨리어싱을 사용합니다.
- 점별 불투명도는 `0.12 + 0.88 × (i / 7)^2.2`입니다. 대략 `0.120, 0.132, 0.176, 0.256, 0.377, 0.540, 0.747, 1.000` 순으로, 동일한 차이로 증가하지 않습니다. 이는 꼬리의 밝기 대비를 조절하는 감마 곡선의 근사이며, 모든 배경·색상에서 정확히 같은 지각 명도 간격을 보장하는 색도 보정은 아닙니다.
- 컨트롤이 숨겨지거나 제거되면 animation clock을 해제하고 다시 나타나면 재시작합니다. 타이머나 정적 Rendering 이벤트를 사용하지 않습니다.
- 버튼과 같은 Grid에 overlay로 배치해 스피너가 꺼져도 버튼 크기와 위치가 바뀌지 않습니다. `IsHitTestVisible=false`이므로 스피너가 버튼 클릭을 막지 않습니다.

점의 위치 계산과 회전은 View 자체의 표시 동작입니다. 따라서 UserControl 코드 비하인드에 있어도 MVVM을 위반하지 않으며 Model/명령 로직과 분리되어 있습니다.

## LoadingSpinner2: 점은 고정하고 불투명도만 순환

왼쪽 패널은 기존 `LoadingSpinner`, 오른쪽 패널은 새 `LoadingSpinner2`를 사용합니다. 두 컨트롤은 같은 `RefData<bool>`에서 읽은 활성 상태를 공유하므로 버튼을 누르면 함께 켜지거나 꺼집니다.

```xml
<!-- MainWindow: 표시 방식은 View에서 선택 -->
<views:CounterView DataContext="{Binding Left}" />
<views:CounterView DataContext="{Binding Right}" UseOpacitySpinner="True" />

<!-- 새 컨트롤을 다른 화면에서 직접 사용하는 방법 -->
<controls:LoadingSpinner2
    IsActivate="{Binding IsActivate, Mode=OneWay}"
    DotBrush="{Binding SpinnerColor, Mode=OneWay}"
    Width="32" Height="32" Background="White" />
```

`Controls/LoadingSpinner2.xaml`과 `.xaml.cs`는 독립적인 UserControl입니다. `IsActivate`, `DotBrush`, 원형 `Background`, 크기 조절의 계약은 기존 스피너와 같습니다.

- 점 8개는 같은 반지름과 45° 간격을 사용하지만 **RenderTransform, Left/Top, 크기를 애니메이션하지 않습니다**. 좌표는 생성 시 한 번 설정합니다.
- 점별 **Opacity만** 애니메이션합니다. 이웃한 점의 밝기 변화가 1/8주기씩 늦게 나타나므로 밝은 부분이 시계 방향으로 이동합니다. 한 바퀴는 1초입니다.
- 각 점은 주기의 7/8 동안 서서히 어두워지고, 나머지 1/8 동안 다시 밝아집니다. SmoothStep과 감마 2.2 곡선을 적용해 최소 0.12, 최대 1.0 사이의 비선형 밝기 변화를 만듭니다.
- 곡선을 한 주기당 64구간으로 샘플링하고 키프레임 사이를 보간합니다. 주기 양 끝의 불투명도가 같으므로 반복 경계에서 값이 갑자기 바뀌지 않습니다.
- 8개 애니메이션은 공유된 frozen ParallelTimeline에서 만든 하나의 ClockGroup으로 함께 시작합니다. 숨김·Unloaded·비활성화 시 clock과 대상 속성의 애니메이션 연결을 제거하고 재표시 시 다시 시작합니다.
- `CounterView`는 DataTemplate으로 선택한 컨트롤 하나만 생성합니다. 두 종류를 겹쳐 두고 동시에 실행하는 구조가 아닙니다. 표시 방식 선택은 Model이나 ViewModel의 업무 상태를 변경하지 않습니다.

## LoadingSpinner3: 1초에 한 바퀴, 45도씩 단계 회전

`Controls/LoadingSpinner3.xaml`과 `.xaml.cs`는 세 번째 재사용 UserControl입니다. 기존 `LoadingSpinner`와 같은 점 배치와 밝기를 사용하며, 점 그룹 전체를 시계 방향으로 **0.125초마다 2π/8 라디안(45도)** 회전합니다. 각 단계 사이에는 각도를 유지하며 보간하지 않습니다. 1초에 8단계 이동하여 한 바퀴는 **1초**입니다.

```xml
<controls:LoadingSpinner3
    IsActivate="{Binding IsActivate, Mode=OneWay}"
    DotBrush="{Binding SpinnerColor, Mode=OneWay}"
    Background="Transparent"
    Width="32" Height="32" />
```

`DiscreteDoubleKeyFrame`으로 0, 45, 90, …, 315도 상태를 각각 0.125초간 유지합니다. 애니메이션 정의는 Freeze하여 공유합니다. `IsActivate`, `DotBrush`, 원형 `Background`, 크기 조절과 표시 상태에 따른 중단 처리는 기존 컨트롤과 같습니다. 재시작 시 0도부터 시작합니다. 기존 좌우 데모는 계속 `LoadingSpinner`와 `LoadingSpinner2`를 사용합니다.

WPF 테스트에서 한 바퀴와 반복 경계의 각도 유지, 색상 바인딩 변경, 비활성화/숨김/Unloaded/재로드에 따른 애니메이션 중단과 재시작을 확인합니다.

## 성능 및 보이지 않을 때의 처리

세 컨트롤은 `SpinnerActivityMonitor`를 사용하여 다음 조건에서는 애니메이션 clock을 제거합니다. `IsActivate` 원본 값은 바꾸지 않으므로 다시 표시되면 원래 활성 상태에 따라 재시작합니다.

| 상황 | 애니메이션 |
| --- | --- |
| `IsActivate=false` | 중단 |
| 자신 또는 부모가 Hidden / Collapsed | 중단 |
| Unloaded / 화면에서 제거 | 중단, Window/ScrollViewer 이벤트 구독 해제 |
| 호스트 창 숨김 / 최소화 | 중단 |
| ScrollViewer의 표시 영역에서 완전히 벗어남 | 중단 |
| 스크롤 영역에 일부라도 표시됨 | 활성 상태이면 실행 |
| 복원 / 재표시 / 다시 스크롤해서 들어옴 | 활성 상태이면 재시작 |

표시 상태는 Loaded/Unloaded, IsVisibleChanged, SizeChanged, Window.StateChanged, ScrollChanged 이벤트에서 확인합니다. 중첩된 ScrollViewer에서는 각 ScrollContentPresenter의 표시 범위를 확인합니다. 타이머 폴링, CompositionTarget.Rendering, 전역 LayoutUpdated 감시는 사용하지 않습니다.

`LoadingSpinner`의 회전 애니메이션 정의는 한 번 생성한 뒤 Freeze하여 공유합니다. `LoadingSpinner2`의 8×65개 키프레임도 한 번 계산하여 Freeze한 공통 정의를 사용합니다. 실행 중인 컨트롤별 clock은 독립적이며, 정지 시 clock 연결을 제거합니다. 따라서 재사용되는 정의에 컨트롤 인스턴스가 묶이지 않고, 꺼진 컨트롤에 실행 clock이 유지되지 않습니다. CPU 사용률 개선 폭을 벤치마크한 것은 아니며 불필요한 실행과 정의의 중복 생성을 줄인 변경입니다.

**감지 범위:** 다른 애플리케이션 창에 가려짐, 창 전체가 모니터 밖으로 이동함, `Opacity=0`, 임의의 Clip/겹친 요소에 의한 가림까지 픽셀 단위로 판정하지는 않습니다. 이런 정책이 필요한 화면은 해당 표시 상태에 맞춰 `IsActivate` 또는 `Visibility`를 연결해야 합니다. 일반적인 Visibility 숨김, 탭에서 제거, 최소화, 스크롤 영역 이탈은 위 처리에 포함됩니다.

실제 WPF 테스트에서 최소화/복원, 창 Hide/Show, 스크롤 영역 이탈/진입 반복, Unloaded/재로드 후 애니메이션 속성 연결이 제거·복구되는지 확인합니다. 기존 bool/색상 바인딩과 점 좌표 고정 검증도 유지합니다.

## 세 스피너 CPU·메모리 실측 비교

2026-09-07 현재 구현의 Release 빌드에서 각 스피너 1개·64개의 활성/정지 상태를 새 프로세스로 3회씩 측정했습니다. [성능 비교 보고서](docs/performance/spinner-comparison-2026-09-07.md)에 측정 조건, CPU·메모리 결과와 해석을 정리했습니다. [재현용 도구](tools/SpinnerPerf/README.md)와 [원본 결과](docs/performance/2026-09-07/raw.csv)도 포함합니다. 수명 관리 변경 전후의 개선율을 측정한 실험은 아닙니다.
