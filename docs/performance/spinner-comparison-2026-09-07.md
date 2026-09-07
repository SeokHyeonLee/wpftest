# 세 LoadingSpinner의 CPU·메모리 성능 비교

측정일: 2026-09-07 (KST). 제품 코드 기준: `af3199bf3f20dca54f291e98ce084851ad152754`.

**64개 동시 실행에서는 LoadingSpinner3의 CPU 사용량이 가장 낮았다.** 전체 CPU 중앙값은 LoadingSpinner 4.146%, LoadingSpinner2 5.412%, LoadingSpinner3 0.763%였다. LoadingSpinner3는 각각보다 약 81.6%, 85.9% 낮았다. 이는 정지 기준값을 차감하지 않은 프로세스 CPU 중앙값 비교다.

메모리는 LoadingSpinner와 LoadingSpinner3가 비슷했다. LoadingSpinner2는 64개 활성 조건에서 Private Bytes가 약 3MiB 더 컸으며, UI 스레드의 지속 할당량과 Gen0 GC 횟수도 가장 많았다. **1개 실행의 CPU는 반복 편차가 커서 세 구현의 일반적인 우열을 확정하지 않는다.**

## 측정 환경과 방법

| 항목 | 조건 |
| --- | --- |
| CPU | AMD Ryzen 7 5700X3D, 물리 8코어 / 논리 16개 |
| OS / 메모리 | Windows 11 Pro, 빌드 26200 / OS 가용 물리 메모리 약 31.9GiB |
| 전원 구성 | 고성능 |
| 빌드 / 런타임 | Release, .NET Framework 4.8 대상, CLR 4.0.30319.42000 |
| 프로세스 / 렌더링 | 실제 32비트 프로세스, WPF Render Tier 2, 96 DPI; 39회 모두 동일 |
| 화면 | 중앙에 표시하는 420×450 DIP 창, 흰 배경, 고정 8×8 UniformGrid |
| 컨트롤 | 32×32 DIP, 기본 DimGray 점, 각 종류별 1개 또는 64개 |
| 시나리오 | 세 종류 × 두 개수 × 활성/정지 + 빈 창 = 13개 |
| 반복 | 매번 새 프로세스, 조건별 3회, 총 39회; 시드 20260907로 순서를 섞음 |
| 시간 | 준비 3초 후 최소 6초 측정; 실제 경과 시간으로 CPU·할당률 계산 |

정지 조건도 먼저 2초간 활성화한 뒤 `IsActivate=false`로 바꾸고 1초간 안정화했다. `Visibility=Visible`을 로컬 값으로 유지하여 점과 레이아웃은 남기고 애니메이션만 중단했다. 따라서 실제 앱의 기본 `Collapsed` 상태를 측정한 결과는 아니다. 활성 조건은 준비 3초 내내 실행했다.

측정 전후에 모든 컨트롤의 Loaded/IsVisible/32 DIP 크기와 애니메이션 속성 연결 상태를 검사했다. 측정 전에 강제 GC를 수행했고, 측정 종료 후 관리 힙을 읽기 위해 다시 수행했다. 이 강제 GC는 측정 구간 CPU 및 GC 횟수에 포함하지 않았다. 메모리는 250ms마다 샘플링했다. 이 타이머는 측정용이며 스피너 회전을 구동하지 않는다.

## 지표 해석

- **전체 CPU %** = 프로세스 CPU 시간 증가량 / 실제 경과 시간 / 논리 프로세서 16개 × 100. 작업 관리자의 순간 표시와 직접 일치하는 값은 아니다.
- **1코어 환산 %** = 프로세스 CPU 시간 / 경과 시간 × 100. 100%는 논리 코어 하나를 계속 사용하는 양이다.
- **Private Bytes** = 프로세스의 전용 커밋 메모리. **Working Set** = 현재 물리 메모리에 올라온 페이지이며 공유 페이지도 포함한다. 단위는 MiB(2²⁰바이트)다.
- **관리 힙** = 측정 후 강제 GC 뒤 `GC.GetTotalMemory(false)`. **UI 할당률** = UI 스레드의 할당 바이트 증가량 / 경과 시간. 네이티브·GPU·다른 스레드 할당은 포함하지 않는다.
- 표의 값은 **3회 중앙값**이며 괄호는 **3회 최솟값–최댓값**이다. 메모리는 각 실행의 샘플 중앙값을 구한 뒤 실행 간 중앙값/범위를 구했다. 신뢰구간을 의미하지 않는다.

메모리는 WPF·CLR·측정 도구를 포함한 **프로세스 전체** 수치다. 한 스피너가 표의 47~54MiB를 독점한다는 의미가 아니다. `Process.Refresh`와 메모리 샘플링 자체도 할당과 CPU 비용을 발생시킨다.

## 활성 상태 CPU·메모리

| 구현 | 개수 | 전체 CPU % (범위) | 1코어 환산 % | Private Bytes MiB (범위) | Working Set MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| LoadingSpinner | 1 | 0.375 (0.110–0.422) | 6.006 | 47.875 (47.633–47.902) | 56.113 |
| LoadingSpinner2 | 1 | 0.016 (0.000–1.048) | 0.250 | 48.371 (47.984–48.410) | 56.621 |
| LoadingSpinner3 | 1 | 0.146 (0.114–0.162) | 2.335 | 46.813 (46.602–46.875) | 55.043 |
| LoadingSpinner | 64 | 4.146 (4.067–4.147) | 66.336 | 51.402 (51.285–51.434) | 59.734 |
| LoadingSpinner2 | 64 | 5.412 (5.387–5.548) | 86.596 | 54.500 (54.160–54.531) | 62.871 |
| LoadingSpinner3 | 64 | 0.763 (0.536–0.826) | 12.213 | 51.313 (51.277–51.484) | 59.699 |

LoadingSpinner2의 1개 실행은 CPU 누적 시간이 0ms인 반복부터 전체 CPU 1.048%인 반복까지 있었다. 짧은 구간의 CPU 시간 해상도와 실행 편차 때문에 중앙값 0.016%를 근거로 가장 효율적이라고 결론 내릴 수 없다. 0.000%도 실제 CPU 작업이 없다는 뜻은 아니다. 정지나 빈 창보다 낮은 값도 같은 이유로 순수 애니메이션 비용으로 해석하지 않는다.

64개 실행은 세 반복 모두 CPU 범위가 분리됐다. 이 조건에서는 LoadingSpinner3의 낮은 CPU 비용이 일관되게 관찰됐다. 반면 LoadingSpinner와 LoadingSpinner3의 Private Bytes 범위는 겹치므로, 메모리 절감 효과는 확인했다고 보기 어렵다.

## 활성 상태 할당·GC

| 구현 | 개수 | 관리 힙 MiB | UI 할당 KiB/s | Gen0 GC / 측정 구간 |
| --- | ---: | ---: | ---: | ---: |
| LoadingSpinner | 1 | 1.167 | 322.280 | 0 |
| LoadingSpinner2 | 1 | 1.167 | 513.733 | 0 |
| LoadingSpinner3 | 1 | 1.167 | 270.797 | 0 |
| LoadingSpinner | 64 | 2.550 | 958.107 | 1 |
| LoadingSpinner2 | 64 | 2.758 | 6847.204 | 12 |
| LoadingSpinner3 | 64 | 2.549 | 738.092 | 1 |

LoadingSpinner2 64개는 약 **6.69MiB/s**를 UI 스레드에서 할당했다. LoadingSpinner3의 약 **9.3배**이며, 약 6초 구간마다 Gen0 GC가 12회 발생했다. 모든 실행의 측정 구간에서 Gen1·Gen2 GC는 0회였다. 관리 힙 크기 차이가 작아도 지속 할당과 GC 비용은 클 수 있다.

빈 창의 UI 할당률도 171.020KiB/s였고, 정지 조건들은 약 169~172KiB/s 중앙값을 보였다. 표에는 이 도구 비용을 차감하지 않은 원본 수치를 사용했다. 측정 전후 관리 힙 차이에는 진단용 캐시와 샘플 저장소가 포함되므로 이를 누수량으로 해석하지 않는다.

## 정지 상태와 빈 창

| 구현 | 개수 | 전체 CPU % (범위) | Private Bytes MiB | Working Set MiB |
| --- | ---: | ---: | ---: | ---: |
| 빈 창 | 0 | 0.065 (0.032–0.081) | 45.883 | 52.504 |
| LoadingSpinner | 1 | 0.032 (0.032–0.098) | 47.609 | 55.930 |
| LoadingSpinner2 | 1 | 0.016 (0.016–0.032) | 48.008 | 56.289 |
| LoadingSpinner3 | 1 | 0.064 (0.032–0.065) | 46.859 | 55.191 |
| LoadingSpinner | 64 | 0.065 (0.016–0.081) | 50.648 | 59.105 |
| LoadingSpinner2 | 64 | 0.049 (0.049–0.049) | 54.270 | 62.598 |
| LoadingSpinner3 | 64 | 0.114 (0.081–0.130) | 50.867 | 59.246 |

정지하면 64개 구성도 전체 CPU 중앙값이 0.049~0.114%로 낮아졌다. 점과 컨트롤을 유지하므로 메모리가 빈 창 수준으로 돌아가지는 않는다. 특히 LoadingSpinner2의 정지 후 Private Bytes는 활성 상태와 비슷했다. 이는 컨트롤·런타임·캐시가 남은 상태의 단기 관찰이며 누수 증거는 아니다.

## 구현과 결과의 관계

| 구현 | 실행 중 변경하는 속성 | 애니메이션 정의 |
| --- | --- | --- |
| [LoadingSpinner](../../RefDataMvvm.Wpf/Controls/LoadingSpinner.xaml.cs) | 점 그룹의 RotateTransform.Angle 하나 | 0→360도 연속 회전, 1초 주기 |
| [LoadingSpinner2](../../RefDataMvvm.Wpf/Controls/LoadingSpinner2.xaml.cs) | 점 8개의 Opacity | ClockGroup과 자식 AnimationClock 8개; 공유 정의에 8×65개 선형 키프레임 |
| [LoadingSpinner3](../../RefDataMvvm.Wpf/Controls/LoadingSpinner3.xaml.cs) | 점 그룹의 RotateTransform.Angle 하나 | 9개 discrete 키프레임, 0.125초마다 45도, 1초에 한 바퀴 |

세 구현 모두 애니메이션 정의를 static으로 만들고 Freeze하여 공유한다. 따라서 정의 공유만으로 인스턴스별 실행 비용까지 없어지지는 않는다. LoadingSpinner2의 많은 애니메이션 대상은 높은 할당량과 일치하는 구조적 차이지만, 개별 WPF 내부 함수의 비용을 프로파일링한 것은 아니다.

LoadingSpinner3는 각도가 초당 8번만 바뀐다. 이것이 낮은 CPU 결과의 원인일 가능성은 있지만, WPF 전체 clock 평가가 초당 8회로 제한된다고 확인한 것은 아니다. 화면 FPS나 GPU 사용률도 이번 수치로 추정하지 않는다.

## 적용 판단과 한계

- 현재 PC에서 **64개를 동시에 표시하고 단계 회전 표현이 목적이라면 LoadingSpinner3가 CPU 측면에서 유리**했다. LoadingSpinner 대비 메모리 이점은 뚜렷하지 않았다.
- 연속 회전이 필요하면 LoadingSpinner를 사용한다. 불투명도 변화가 필요한 LoadingSpinner2는 다량 동시 실행 시 높은 할당률과 GC 비용을 고려한다.
- 한두 개의 작은 스피너에 대해서는 짧은 CPU 측정의 순위보다 필요한 표현 방식을 우선한다. 64개 결과를 개수로 나누어 단일 인스턴스 비용으로 환산하지 않는다.
- 한 PC, 한 크기, 짧은 3회 반복 결과다. CPU 전체 사용률에는 다른 앱이나 DWM의 CPU가 들어가지 않지만, 백그라운드 활동과 스케줄링·렌더링 상태는 결과에 영향을 줄 수 있다.
- 창을 표시하고 WPF 상태를 검사했으나 다른 앱의 창에 가려졌는지는 픽셀 단위로 기록하지 않았다. GPU 시간/메모리, 실제 표시 FPS, 전력, 시작 비용, 장시간 누수, x64·소프트웨어 렌더링·다른 DPI는 측정하지 않았다.
- 제품 스피너 구현을 최적화하거나 변경하지 않았다. 이 보고서는 현재 세 구현의 비교이며 이전 커밋 대비 개선율 실험은 아니다.

## 결과 파일과 재현

- [환경 기록](2026-09-07/environment.json)
- [전체 39회 원본 CSV](2026-09-07/raw.csv): CPU 시간, 실측 시간, 메모리 샘플 중앙값/최댓값, GC와 런타임 정보
- [시나리오별 중앙값·최솟값·최댓값 CSV](2026-09-07/summary.csv)
- [측정 소스와 상세 설명](../../tools/SpinnerPerf/README.md)

Visual Studio Developer PowerShell에서 저장소 루트 기준으로 실행한다. 새 출력 경로를 사용하면 이번 결과를 보존할 수 있다.

```powershell
msbuild .\tools\SpinnerPerf\SpinnerPerf.csproj /t:Build /p:Configuration=Release /m
.\tools\SpinnerPerf\run.ps1 -OutputDirectory .\artifacts\spinner-perf-new
.\tools\SpinnerPerf\summarize.ps1 -ResultDirectory .\artifacts\spinner-perf-new
```

검증: 측정 도구 Release 빌드 성공, 13개 시나리오 × 3회 완주, 실행 전후 표시/clock 상태 검사 통과, 원본의 반복 누락·중복 검사 통과. 별도로 솔루션 Release 빌드와 기존 WPF 포함 테스트 **12개 모두 통과**했다.
