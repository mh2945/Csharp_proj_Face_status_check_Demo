# 팀 공용 계약 — 읽고 시작할 것

FASMH-94 PoC. 이 문서의 규칙은 협상 대상이 아니다. 세 에이전트가 동시에 작업하므로
**소유 파일 밖은 절대 건드리지 않는다.**

## 0. 절대 규칙

1. **`src/EtusDetectSample/Sdk/FaceSDK.cs` 와 `FaceSDKUtil.cs` 는 SDK zip 원본이다. 한 글자도 고치지 말 것.**
   (SDK Public API 하위 호환성 보장을 위해 원본 유지)
2. **C# 7.3 문법만 사용.** net48 + 소스 링크된 net8.0 테스트 프로젝트 양쪽에서 컴파일되어야 한다.
   금지: record, init-only, target-typed `new()`, switch expression, nullable reference types,
   범위 연산자, `using` 선언문(declaration form). `var`/식 본문 멤버/보간 문자열은 OK.
3. **`Analysis/`, `Alerts/`, `Logging/`, `Config/` 는 외부 의존 0.** WinForms, OpenCvSharp,
   `Alchera.FaceSDK` 를 `using` 하지 말 것. 이 코드는 macOS 에서 net8.0 으로 단위 테스트된다.
   (`System`, `System.Collections.Generic`, `System.Globalization`, `System.IO`, `System.Text`,
   `System.Threading` 만 허용)
4. **판정 시간축은 `FrameObservation.MonotonicSec`(Stopwatch 기반)만 쓴다.** `DateTime.Now`/`UtcNow`
   는 로그 표기 외 금지 — 시스템 시각 변경에 판정이 흔들리면 안 된다.
5. 이미 존재하는 계약 파일(`Analysis/FrameObservation.cs`, `Alerts/AlertModels.cs`,
   `Config/AppSettings.cs`)은 **읽기 전용**이다. 필드 추가가 꼭 필요하면 코드를 고치지 말고
   보고만 할 것.

## 1. SDK 사용 시 반드시 지킬 것 (실측 확인 완료)

```csharp
// FaceSDK.cs:840-901 — 모든 NewRst 오버로드는 IsOk() 일 때만 값 필드를 채운다.
// 에러 시 eyelid/occlusion 은 0 으로 남는다 → 0 을 "눈 감김"으로 오독하기 쉽다.
if (rst.IsOk()) { /* 그제서야 값을 읽는다 */ }
```

```csharp
// FaceSDK.cs:777-780 — Mask 는 반전되어 있다.
// face_mask_confidence 가 "낮을수록" 마스크 착용.
bool masked = rst.IsFaceMasked();   // 직접 비교 금지, 반드시 이 메서드를 쓸 것
```

- `Initialize(modelPath, "")` — `modelPath` 는 `AppDomain.CurrentDomain.BaseDirectory` 기준
  **절대경로**. `sdk_path` 는 빈 문자열. `license.cer` 는 models 폴더 안에 있다.
- `DetectFace(bgr, w, h, use_continuous_img_detect: true)` — 카메라 연속 입력이므로 `true`.
- 속성 함수들은 `DetectFace` 가 준 `Face` 를 `ref` 로 재사용한다. 다시 검출하지 않는다.
- `DetectClosedEyes` 는 `DetectFace` 와 **같은 버퍼**에 대해 호출해야 한다.
- C# wrapper 에는 `SetDetectableSize` / `ResetTrackState` / MultiFace 가 **없다**. 찾지 말 것.

## 2. 데이터 흐름

```
CameraCapture(Mat) → byte[] BGR
      ↓
FaceEngine.Analyze(bgr,w,h) → FrameObservation        [WinDev-Sdk 소유]
      ↓
EyeStateGate.Evaluate(obs)  → EyeDecision             [WinDev-Logic 소유]
      ↓
SeatStateMachine.Push(obs, eye) → SeatSnapshot + AlertEvent[]
      ↓
AlertDispatcher / FrameCsvLogger / MainForm(BeginInvoke)
```

## 3. 스레딩 계약

- 워커 스레드 **1개**가 `VideoCapture` 와 `FaceSDK` 를 독점 소유한다. SDK 는 스레드 안전이
  문서화되어 있지 않고 continuous tracking 캐시가 프레임 순서에 의존한다.
- UI → 워커: `lock` + `Queue`.
- 워커 → UI: **`BeginInvoke` 만.** `Invoke` 는 종료 시 데드락.
- 종료 플래그는 `volatile bool`.
- UI 스레드에서 SDK 를 호출하지 않는다.

## 4. 임계값

전부 `Config/AppSettings.cs` 에 있다. **매직 넘버를 코드에 새로 박지 말 것.**
기본값 근거: 참고 샘플 `DemoClientServer.cs:390-394` (eyelid 2.4 / occlusion 0.5 / landmark 0.90)
+ FASMH-94 문서 (졸음·이석 확정 5초).

## 5. 알려진 한계 — 코드와 UI에 반드시 반영

- 안면 인식 기반이다. **객체 인식이 아니다.** 엎드림과 이석을 원리적으로 구분할 수 없다.
- `StudySec` 는 "깨어 있는 시간"이지 실제 공부시간이 아니다.
- `DetectFineOcclusion` 은 모델 미포함 가능성이 있어 **런타임 실패 시 자동 비활성**되어야 한다.
  실패했다고 앱이 죽으면 안 된다.
- 임계값은 전부 잠정치다. 현장 재보정 전제.

---

## 부록 A — 리스크 해소 (2026-08-21, 담당자 확인)

계획 단계에서 "미검증"으로 남겼던 항목에 대한 확정 답변. **아래는 추측이 아니라 확인된 사실이다.**

| # | 항목 | 확정 내용 | 코드 영향 |
|---|---|---|---|
| 1 | 라이선스 | **`license.cer` 파일만 있으면 된다.** `LicenseGen.exe` 실행(노드락 활성화) 불필요 | setup 스크립트/README 에서 LicenseGen 절차 삭제 |
| 2 | model package 구성 차이 | 참고 샘플과 파일 목록이 다른 것은 **이 패키지에 들어간 기능이 더 적기 때문**. 정상 | 별도 대응 불필요. 단 `FineOcclusionAvailable` 런타임 폴백은 안전장치로 유지 |
| 3 | Occlusion 방향 | **눈이 가려지면 값이 높아진다.** | `occl > OcclusionMax` → 가려짐. 부등호 이대로 확정 |
| 4 | FineOcclusion 방향 | **3번과 동일 — 가려질수록 높아진다.** | `fineOccl > FineOcclusionMax` → 가려짐. `UseFineOcclusionGate` 기본 **true** 로 변경됨 |
| 7 | 분석 입력 해상도 | **720 x 1280 (세로) 이미지 기준으로 진행한다.** 성능 목적 | 아래 파이프라인 참조 |

### 분석 입력 파이프라인 (신규)

SDK 에 넘기는 버퍼는 캡처 원본이 아니라 **720x1280 세로 이미지**다.
`AppSettings` 에 관련 키가 추가되었다.

```
VideoCapture (기본 1280x720 가로)
   ↓ SrcRotateDegrees   (0/90/180/270 — 카메라를 세로로 설치했으면 0)
   ↓ EnableAspectCrop   AnalyzeWidth:AnalyzeHeight(720:1280) 종횡비로 중앙 크롭
   ↓ EnableAnalyzeResize → AnalyzeWidth x AnalyzeHeight (720x1280)
FaceEngine.Analyze(bgr, 720, 1280)
```

- `AnalyzeWidth = 720`, `AnalyzeHeight = 1280`, `SrcRotateDegrees = 0`,
  `EnableAspectCrop = true`, `EnableAnalyzeResize = true`
- **좌표계 주의**: `FrameObservation` 의 box/landmark 는 **분석 이미지(720x1280) 좌표**다.
  프리뷰도 같은 분석 이미지를 그려야 좌표가 맞는다. 캡처 원본을 프리뷰로 쓰면 오버레이가 어긋난다.
- 크롭·리사이즈는 `Cv2.Resize` / `Mat` ROI 로 처리한다(`ImgUtil.CropImgByAspectRatio` 는 byte[] 기반이라 느리다).
