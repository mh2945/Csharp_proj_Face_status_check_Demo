# Etoos 자습실 좌석 모니터링 데모 (FASMH-94)

Alchera **FaceSDK 1.17.7** 기반 Windows 데모. 자습실 좌석에 앉은 학생을 카메라로 보며
**졸음**과 **이석**을 감지하고, 안면인식만으로 어떤 데이터를 뽑을 수 있는지를 한 화면에서 보여준다.

```
시작 → 카메라 연동 → SDK 기능 사용
```

조작은 셋뿐이다 — **카메라 선택 / 시작 / 정지**.

---

## 1. 무엇을 감지하나

| 상태 | 조건 | 표시 |
|---|---|---|
| 공부 중 | 얼굴 검출 + 눈 뜸 | 초록 |
| 졸음 의심 | 눈 감김 **2초** | 주황 + 경고 |
| **졸음** | 눈 감김 **5초** | 빨강 + Alert · "학생이 졸고 있습니다" |
| 이석 의심 | 얼굴 미검출 **2초** | 주황 + 경고 |
| **이석** | 얼굴 미검출 **5초** | 빨강 + Alert · "학생이 좌석을 옮겼습니다" |
| 판정 불가 | 눈 가림 / 마스크 / 고개 각도 초과 등 | 회색 + 사유 표시 |

확정 5초는 FASMH-94 문서 명시값이다. 전부 `App.config` 로 조정 가능하다.

### 산출 데이터
- **착석시간 / 공부시간(비졸음) / 졸음시간 / 이석시간 / 판정불가시간**
- 졸음·이석 발생 횟수, PERCLOS
- 프레임 단위 원시 로그 (CSV) — 눈꺼풀 거리, occlusion, head pose, landmark 신뢰도, Face tracking ID
- 알림 payload (JSONL) — 중앙 인포데스크로 그대로 전달 가능한 형태

---

## 2. 빌드 · 실행 (Windows)

**필요 환경**: Windows x64, Visual Studio 2022, .NET Framework 4.8

### 2-1. 런타임 배치
```bat
cd etus-detect-sample\tools
setup-runtime.bat
```
이 스크립트가 하는 일:
1. FaceSDK zip 에서 native DLL 4종을 `natives\` 로 추출
   (`AlcheraFaceSDK.dll`, `AlcheraFaceSDKCS.dll`, `AlcheraEncryptCS.dll`, `opencv_world455.dll`)
2. `models\` 폴더 전체를 빌드 출력 폴더로 복사 (`0.f` 하나가 260MB라 csproj 에 넣지 않고 여기서 처리)

> **라이선스**: `models\license.cer` 파일만 있으면 된다. `LicenseGen.exe` 를 실행하는
> 노드락 활성화 절차는 **없다**.

### 2-2. 빌드
`EtusDetectSample.sln` 을 VS2022 로 열고 **x64** 로 빌드한다.

> native 3종이 x64 전용이라 csproj 가 `PlatformTarget=x64` 를 무조건 고정한다.
> AnyCPU 로 빌드되면 `BadImageFormatException` 이 난다.

### 2-3. 실행 후 출력 폴더 (`bin\x64\Debug\`)
```
EtusDetectSample.exe
EtusDetectSample.exe.config     ← 임계값. 재빌드 없이 편집 가능
AlcheraFaceSDK.dll  AlcheraFaceSDKCS.dll  AlcheraEncryptCS.dll  opencv_world455.dll
OpenCvSharp.dll  (+ OpenCvSharpExtern.dll)
models\          ← 0.1o … 0.f, license.cer
logs\            ← 실행 시 자동 생성
```

DllImport 가 전부 경로 없는 모듈명이라 **native DLL 은 반드시 exe 와 같은 폴더**에 있어야 한다.

---

## 3. 설정 (`EtusDetectSample.exe.config`)

재빌드 없이 현장에서 조정한다. **기본값은 대부분 잠정치다.**

### 재보정이 필요한 것
| 키 | 기본값 | 비고 |
|---|---|---|
| `EyelidClosedThreshold` | `2.4` | 눈꺼풀 거리 임계. **근거리 셀피 기준값이라 자습실 거리에서는 안 맞을 가능성이 높다.** 4절 참조 |
| `LandmarkConfMin` | `0.90` | 이 값 미만이면 판정 불가 |
| `OcclusionMax` | `0.50` | 눈 가림 임계. **가려질수록 값이 높아진다** |

### 시간 임계 (초)
`ClosedEyeSuspectSec` 2.0 · `ClosedEyeConfirmSec` **5.0** · `NoFaceSuspectSec` 2.0 ·
`NoFaceConfirmSec` **5.0** · `UnknownGraceSec` 1.5 · `AlertCooldownSec` 15.0

### 카메라 · 성능
| 키 | 기본값 | 비고 |
|---|---|---|
| `CameraIndex` | `0` | |
| `CaptureWidth` / `CaptureHeight` | `1280` / `720` | 카메라에서 받는 크기 |
| `AnalyzeWidth` / `AnalyzeHeight` | `720` / `1280` | **SDK 에 넘기는 크기(세로)** |
| `SrcRotateDegrees` | `0` | 0/90/180/270. 프리뷰가 누워 보이면 여기를 바꾼다 |
| `AttrIntervalFrames` | `5` | Mask·FineOcclusion 호출 주기. FPS 가 낮으면 올린다 |
| `TargetAnalyzeFps` | `10.0` | |
| `CaptureApi` | `ANY` | 카메라가 안 열리면 `MSMF` / `DSHOW` 시도 |

**영상 파이프라인**:
```
캡처(1280x720) → 좌우반전 → 회전 → 720:1280 중앙크롭 → 720x1280 리사이즈 → SDK
```
`FrameObservation` 의 얼굴 box·landmark 좌표는 **분석 이미지(720x1280) 기준**이다.

---

## 4. 임계값 재보정 절차

`EyelidClosedThreshold` 2.4 는 SDK 샘플의 근거리 셀피 기준값이다. 실제 자습실은 카메라
거리·각도·조명이 달라 눈 크기가 다르게 잡힌다. **시연 전에 반드시 재보정할 것.**

1. 앱을 실행하고 **평소 자세로 눈을 뜬 채** 30초 → `logs\frames-*.csv` 의 `eyelidL`/`eyelidR` 분포 확인
2. 같은 자세로 **눈을 감은 채** 10초 → 같은 컬럼 확인
3. 두 분포 사이의 골짜기를 임계값으로 잡는다
4. `EtusDetectSample.exe.config` 의 `EyelidClosedThreshold` 수정 → 앱 재시작 (재빌드 불필요)

---

## 5. 산출 로그 (`logs\`)

| 파일 | 내용 |
|---|---|
| `frames-yyyyMMdd-HHmmss.csv` | 프레임 단위 원시 데이터 37컬럼. 재보정·분석용 |
| `alerts-yyyyMMdd.jsonl` | 알림 1건당 1줄. **인포데스크 연동 payload 그대로** |
| `session-yyyyMMdd-HHmmss.csv` | 세션 종료 시 요약 1행 |

알림 payload 예:
```json
{"schemaVersion":"1.0","eventId":"5f3c…","occurredAt":"2026-08-21T14:02:11.482+09:00",
 "seatId":"S-01","eventType":"DROWSY","level":"ALERT","message":"학생이 졸고 있습니다",
 "durationSec":5.2,
 "evidence":{"eyelidLeft":1.83,"eyelidRight":1.91,"closedEyeSec":5.2,"perclos":0.61,
   "landmarkConfidence":0.96,"occlusion":{"left":0.03,"right":0.02,"mouth":0.01},
   "fineOcclusion":0.05,"mask":false,"pose":{"yaw":-1.2,"pitch":18.4,"roll":0.8},
   "faceTrackId":7,"slumpSuspected":false,"unknownReason":null},
 "session":{"seatedSec":754.0,"studySec":662.0,"drowsySec":48.0,"awaySec":44.0,
   "unknownSec":0.0,"drowsyCount":2,"awayCount":1,"blinkCount":37}}
```
UI 의 Alert 목록에서 **행을 더블클릭하면 전체 JSON** 을 볼 수 있다.

---

## 6. 알려진 한계 — 고객 설명 시 반드시 함께 말할 것

- **안면 인식이지 객체 인식이 아니다.** 얼굴이 보이는지까지만 알 수 있다.
- **엎드림과 이석을 원리적으로 구분할 수 없다.** 둘 다 "얼굴이 안 보임"이다.
  완화책으로, 눈을 감고 있다가 얼굴이 사라지면 이석이 아니라 **졸음(엎드림 의심)** 으로 유지한다
  (`EnableSlumpHeuristic`). 규칙 기반이라 완벽하지 않다.
- **"공부시간"은 깨어 있는 시간이다.** 휴대폰·멍함·대화를 구분하지 못한다.
  실제 공부시간 산출은 안면인식 위에 별도 Activity Recognition 이 필요하다.
- **C# wrapper 는 MultiFace 를 지원하지 않는다.** C bridge export 가 `fsdkc_detect_one_face`
  하나뿐이라 프레임당 대표 얼굴 1개만 온다. 이 데모는 **단일 좌석 1인** 기준이다.
  다좌석은 좌석별 ROI 크롭 후 반복 호출 또는 C++ MultiFace API 가 필요하다.
- **졸음 / 이석 판정은 SDK 기능이 아니다.** SDK 는 프레임 단위 눈꺼풀 거리까지만 준다.
  시간축 판정(`Analysis/`)은 전부 이 데모에서 신규 구현한 것이다.
- **`DetectFineOcclusion`** 은 model package 에 해당 모델이 없으면 런타임에 자동 비활성된다
  (앱은 정상 동작하고 상태바에 표시된다).

---

## 7. 코드 구조

```
src/EtusDetectSample/
├─ Program.cs            진입점 + 미처리 예외 훅
├─ AppController.cs      ★ 계층 배선의 유일한 지점
├─ MainForm.cs           순수 뷰 (SDK/OpenCvSharp 를 모른다)
├─ Sdk/
│  ├─ FaceSDK.cs         SDK zip 원본 — 수정 금지
│  └─ FaceEngine.cs      초기화 / 프레임 1장 → FrameObservation
├─ Capture/
│  ├─ CameraCapture.cs   VideoCapture + 720x1280 전처리
│  └─ BgrBitmap.cs       BGR → Bitmap (버퍼 재사용)
├─ Worker/AnalysisWorker.cs   워커 스레드 1개가 카메라와 SDK 를 독점 소유
├─ Analysis/             ★ 외부 의존 0 — macOS 에서 단위 테스트됨
│  ├─ FrameObservation.cs  계층 간 DTO 계약
│  ├─ EyeStateGate.cs      Open / Closed / Unknown(사유) 판정
│  ├─ SeatStateMachine.cs  타이머·PERCLOS·상태 전이
│  └─ SessionStats.cs      누적 집계
├─ Alerts/               payload 스키마 · JSON 직렬화 · fan-out
├─ Logging/              CSV · JSONL (백그라운드 flush)
└─ Config/               App.config 로드 + 범위 보정

tests/EtusDetectSample.Tests/   net8.0 xunit — Analysis 계층을 소스 링크로 검증
docs/CONTRACT.md                계층 간 계약 · SDK 사용 시 주의점
docs/WINDOWS_TEST_GUIDE.md      실기 검증 런북
```

**스레드 모델**: 워커 스레드 1개가 `VideoCapture` 와 `FaceSDK` 를 독점 소유한다.
SDK 는 스레드 안전이 문서화되어 있지 않고, continuous tracking 캐시가 프레임 순서에 의존한다.
UI 갱신은 `BeginInvoke` 만 사용한다(`Invoke` 는 종료 시 데드락).

### 단위 테스트 (macOS/Linux 에서도 실행됨)
```bash
dotnet test tests/EtusDetectSample.Tests/EtusDetectSample.Tests.csproj
```
`Analysis/` 는 WinForms·OpenCvSharp·FaceSDK 의존이 없어 플랫폼과 무관하게 검증된다.
앱 프로젝트(.NET Framework 4.8)는 Windows 에서만 빌드된다.

---

## 8. SDK 사용 시 주의점 (하드코딩된 함정 2가지)

```csharp
// (1) Mask 는 반전되어 있다. face_mask_confidence 가 "낮을수록" 착용.
bool masked = rst.IsFaceMasked();   // 직접 비교하지 말고 반드시 이 메서드

// (2) 모든 Rst 는 IsOk() 일 때만 값 필드가 채워진다. 에러 시 눈꺼풀 거리는 0 으로 남는다.
//     그 0 을 "눈 감김"으로 읽으면 SDK 오류가 곧바로 졸음 Alert 이 된다.
if (rst.IsOk()) { /* 그제서야 값을 읽는다 */ }
```
이 데모는 못 잰 값을 `0` 이 아니라 **`float.NaN`** 으로 남긴다.

---

*근거: FASMH-94 (2026-08-21) / FaceSDK-1.17.7_880.20260514174316*
