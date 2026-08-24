# Windows 실기 검증 런북 (FASMH-94)

개발은 macOS, 실제 검증은 Windows 에서 한다. 이 문서만 보고 그대로 따라 할 수 있게 썼다.

**macOS 에서 이미 끝난 것** — 판정 로직 단위 테스트 60건 전부 통과, 앱 전체 타입 체크 통과.
**여기서만 확인 가능한 것** — 라이선스, 모델 로딩, 카메라, 실제 SDK 수치, 성능(FPS).

---

## 0. 준비물

| 항목 | 비고 |
|---|---|
| Windows 10/11 **x64** | native DLL 이 x64 전용 |
| Visual Studio 2022 | .NET Framework 4.8 개발 도구 포함 |
| USB 웹캠 또는 내장 카메라 | |
| `FaceSDK-1.17.7_880.20260514174316.zip` | repo 루트 |
| `models/` 폴더 (`license.cer` 포함) | repo 루트 |

---

## 1. 런타임 배치

```bat
cd etus-detect-sample\tools
setup-runtime.bat
```

**Expected**
- `natives\` 에 DLL 4종: `AlcheraFaceSDK.dll`, `AlcheraFaceSDKCS.dll`, `AlcheraEncryptCS.dll`, `opencv_world455.dll`
- `src\EtusDetectSample\bin\x64\Debug\models\` 에 모델 파일 + `license.cer`
- 마지막 줄에 성공 메시지

> **라이선스는 `license.cer` 파일만 있으면 된다.** `LicenseGen.exe` 를 실행하는 노드락 활성화
> 절차는 없다. 스크립트가 `license.cer` 부재를 감지하면 즉시 실패한다.

**실패 시**
| 증상 | 원인 |
|---|---|
| `tar` 를 찾을 수 없음 | Windows 10 1803 미만. 수동으로 zip 을 풀어 `natives\` 에 넣는다 |
| `license.cer 이 없습니다` | `models\` 폴더에 `license.cer` 이 함께 있어야 한다 |

---

## 2. 빌드

`EtusDetectSample.sln` 을 VS2022 로 열고 **구성: Debug / 플랫폼: x64** 로 빌드.

**Expected** — 오류 0. 첫 빌드는 NuGet 복원(OpenCvSharp4.Windows) 때문에 시간이 걸린다.

### ⚠️ 빌드 직후 반드시 확인할 것 — `OpenCvSharpExtern.dll`

`bin\x64\Debug\` 에 `OpenCvSharpExtern.dll` 이 있는지 본다.
.NET Core 의 `runtimes/` 자동 복사 규칙이 .NET Framework 4.8 에는 그대로 적용되지 않아
**패키지 버전에 따라 복사되지 않을 수 있다.**

없으면 csproj 에 아래를 추가한다 (`<경로>` 는 실제 NuGet 캐시 경로로):
```xml
<Content Include="$(NuGetPackageRoot)opencvsharp4.runtime.win\4.11.0.20250507\runtimes\win-x64\native\*.dll">
  <Link>%(Filename)%(Extension)</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

### 출력 폴더에 있어야 할 것
```
EtusDetectSample.exe
EtusDetectSample.exe.config
AlcheraFaceSDK.dll  AlcheraFaceSDKCS.dll  AlcheraEncryptCS.dll  opencv_world455.dll
OpenCvSharp.dll  OpenCvSharpExtern.dll
models\   (license.cer 포함)
```
DllImport 가 경로 없는 모듈명이라 **native DLL 은 exe 와 같은 폴더**여야 한다.

---

## 3. SDK 초기화 — 가장 먼저 통과시킬 것

앱을 실행한다. 이후 전부가 이 단계 통과를 전제로 한다.

**Expected** — 하단 상태바에 `초기화 OK (xxxx ms)` 와 모델 경로 표시.

### 실패 시 `Error` 별 대처

| Error | 화면 문구 | 대처 |
|---|---|---|
| `CanNotReadModel` | 모델 파일 누락/경로 오류 | `bin\x64\Debug\models\` 존재 확인. `setup-runtime.bat` 재실행 |
| `InvalidLicense` / `CanNotReadLicense` | license.cer 확인 | `models\license.cer` 존재 확인. 파일이 손상되지 않았는지 |
| `LicenseExpired` / `LicenseNotStarted` | 라이선스 기간 | 라이선스 유효기간 확인. 담당자 문의 |
| `SystemTimeTampered` | 시스템 시각 | PC 시각을 현재로 맞춘다 |
| `BadImageFormatException` (크래시) | — | AnyCPU 로 빌드된 것. **x64** 로 다시 빌드 |
| `DllNotFoundException` (크래시) | — | native DLL 이 출력 폴더에 없음 |

**기록할 것**: 초기화 소요 ms. 모델이 260MB라 수 초 걸릴 수 있다.

---

## 4. 카메라 연결

상단 콤보에서 카메라를 고르고 **[시작]**.

**Expected** — 프리뷰에 영상, 얼굴에 사각형 + 106점 landmark, 우측 상단에 FPS.

### 4-1. 화면 방향 — `SrcRotateDegrees`

이 데모는 SDK 에 **720x1280 세로** 이미지를 넘긴다. 프리뷰가 누워 보이거나 얼굴이 잘리면
`EtusDetectSample.exe.config` 의 `SrcRotateDegrees` 를 바꾸고 앱을 재시작한다.

| 카메라 설치 | 값 |
|---|---|
| 세로로 설치(회전됨) | `0` |
| 일반 가로 웹캠 | `90` 또는 `270` — 얼굴이 똑바로 보이는 쪽 |
| 상하 반전 | `180` |

> **성능 참고**: 가로 웹캠(1280x720) + `SrcRotateDegrees=0` 이면 중앙 크롭 결과가 405x720 이라
> 720x1280 으로 가는 것이 **업스케일**이 된다. 없는 해상도를 만들어내는 것이라 검출 정확도에는
> 이득이 없고 CPU 만 쓴다. **`90` 으로 회전하는 쪽이 원본 픽셀을 살린다.** 두 값으로 각각
> 돌려 보고 FPS 와 검출 안정성을 비교한 뒤 정한다.

### 4-2. 카메라가 안 열릴 때
1. `CameraIndex` 를 `0` → `1` → `2` 로 바꿔 본다
2. `CaptureApi` 를 `ANY` → `MSMF` → `DSHOW` 로 바꿔 본다
3. 다른 앱(Teams, Zoom)이 카메라를 점유하고 있지 않은지 확인

### 4-3. 성능
**Expected FPS ≥ 8.**
미달이면 순서대로: `AttrIntervalFrames` 를 `5` → `10` 으로 → `TargetAnalyzeFps` 를 낮춤 →
`AnalyzeWidth/Height` 를 `540x960` 으로 축소.

---

## 5. ⭐ 임계값 재보정 — 시연 전 필수

`EyelidClosedThreshold` 기본값 `2.4` 는 **SDK 샘플의 근거리 셀피 기준값**이다. 자습실은 카메라
거리·각도·조명이 달라 눈꺼풀 거리가 다른 범위로 잡힌다. **이 값이 안 맞으면 시연이 통째로 실패한다.**

1. 시연할 자리에 **실제 카메라 위치 그대로** 앉는다
2. 앱 [시작] → **평소처럼 눈을 뜬 채 30초** (책 보는 자세 포함)
3. **눈을 감은 채 10초**
4. [정지] → `logs\frames-*.csv` 를 엑셀로 연다
5. `eyelidL` / `eyelidR` 컬럼을 본다. 두 구간의 값 분포가 갈릴 것이다

   | 구간 | eyelid 값 예 |
   |---|---|
   | 눈 뜸 | 3.5 ~ 5.0 |
   | 눈 감음 | 0.8 ~ 1.8 |

6. **두 분포 사이 골짜기**를 임계값으로 잡는다 (위 예라면 `2.5` 근처)
7. `EtusDetectSample.exe.config` 의 `EyelidClosedThreshold` 수정 → **앱 재시작** (재빌드 불필요)
8. 눈을 뜬 상태에서 `졸음 의심` 이 뜨지 않고, 감으면 2초 안에 뜨는지 확인

> 값이 겹쳐서 골짜기가 안 보이면 조명이나 카메라 거리 문제다. 얼굴이 프레임에서 너무 작지 않은지
> (`boxW`/`boxH` 컬럼) 확인한다.

---

## 6. 시연 시나리오

각 항목의 **Expected** 가 나오지 않으면 "실패 시 의심 지점" 순서로 확인한다.

### 시나리오 A — 졸음 (문서 명시 5초)
**조작**: 정면을 보고 앉은 상태에서 눈을 **6초 이상** 감는다.

**Expected**
- 2초: 배지 `졸음 의심` (주황) + Alert 목록에 `WARN` 1행
- 5초: 배지 `졸음` (빨강) + `학생이 졸고 있습니다` + `ALERT` 1행 + 경고음 1회
- 눈을 뜨면: `공부 중` (초록) + `RECOVERED` / `CLEAR` 1행
- 우측 `졸음` 누적 시간과 횟수(1회) 증가

**실패 시 의심 지점**
| 증상 | 확인 |
|---|---|
| 눈을 감아도 반응 없음 | 우측 `눈 L/R` 값이 임계값 아래로 내려가는가 → 5절 재보정 |
| 눈을 떠도 계속 졸음 | 임계값이 너무 높다 → 5절 재보정 |
| `판정 불가` 로 빠짐 | 배지 아래 사유 확인 (아래 시나리오 C) |
| 2초/5초가 아닌 다른 타이밍 | config 의 `ClosedEyeSuspectSec` / `ClosedEyeConfirmSec` |

### 시나리오 B — 이석 (문서 명시 5초)
**조작**: 카메라 화면 밖으로 완전히 벗어나 **6초 이상** 머문다.

**Expected**
- 2초: `이석 의심` (주황) + `WARN`
- 5초: `이석` (빨강) + `학생이 좌석을 옮겼습니다` + `ALERT`
- 복귀: `공부 중` + `RECOVERED`
- `이석` 누적 시간·횟수 증가, 그 시간이 `착석`에는 더해지지 않음

### 시나리오 C — 게이트 (오탐 방지)
판정할 수 없는 상황에서 **졸음으로 오판하지 않는지** 보는 것이 목적이다.

| 조작 | Expected 사유 표시 |
|---|---|
| 손으로 한쪽 눈만 가리기 | `한쪽 눈만 감김(판정 제외)` 또는 `눈 가려짐` |
| 손으로 양쪽 눈 가리기 | `눈 가려짐` |
| 마스크 착용 | 우측 `Mask` 가 `착용` 으로 바뀜 (눈 판정은 계속되어야 정상) |
| 고개를 크게 숙이기/돌리기 | `고개 각도 벗어남` |

**공통 Expected**: 배지가 `판정 불가`(회색)이고, **졸음 Alert 이 뜨지 않는다.**
`판정불가` 누적 시간만 증가한다.

> 눈을 가린 3초가 눈 감김 시간에 누적되면 안 된다. 단위 테스트로 검증했지만 실기에서도 확인한다.

### 시나리오 D — 엎드림
**조작**: 눈을 감은 채 그대로 책상에 엎드려 얼굴이 안 보이게 한다.

**Expected** — `이석` 이 아니라 **`졸음 (엎드림 의심)`** 유지.

안면 기반이라 엎드림과 이석은 원리적으로 구분되지 않는다. "눈을 감고 있다가 얼굴이 사라졌으면
자리를 뜬 게 아니라 엎드린 것" 이라는 규칙으로 완화한 것이다. 아주 오래(기본 15초 = `NoFaceConfirmSec`×3)
지나면 결국 `이석` 으로 내려간다.

`EnableSlumpHeuristic=false` 로 두면 이 완화 없이 `이석` 으로 간다.

---

## 7. 산출 로그 확인

[정지] 후 `bin\x64\Debug\logs\` 를 연다.

| 파일 | 확인할 것 |
|---|---|
| `frames-*.csv` | 헤더 37컬럼. `closedSec`/`noFaceSec` 곡선이 실제 동작과 일치하는가 |
| `alerts-*.jsonl` | 시나리오에서 띄운 Alert 수만큼 줄이 있는가. 각 줄이 유효한 JSON 인가 |
| `session-*.csv` | 요약 1행. 착석 = 공부 + 졸음 + 판정불가 가 대략 맞는가 |

**인포데스크 연동 시연**: UI Alert 목록에서 **행을 더블클릭**하면 전체 JSON payload 가 뜬다.
"이 값들을 그대로 중앙 서버로 보낼 수 있다" 를 보여주는 자리다.

---

## 8. ⚠️ 이번 실기에서 반드시 데이터를 남길 것

계획 단계에서 확인하지 못해 **실기로만 답이 나오는** 항목들이다.

### 8-1. `Face.id` 안정성 (가장 중요)
C# wrapper 에는 `ResetTrackState()` 가 없어 tracking 이 언제 리셋되는지 제어할 수 없다.
`Face.id` 가 프레임마다 튀면 상태머신이 계속 리셋되어 **졸음이 영영 확정되지 않는다.**

**확인 방법**: `frames-*.csv` 의 `faceId` 컬럼. 한 사람이 계속 앉아 있는 구간에서 값이 유지되어야 한다.
자주 바뀌면 → config 에서 `ResetOnTrackIdChange=false` 로 내리고 재확인.

**기록**: 60초 동안 `faceId` 가 몇 번 바뀌었는지.

### 8-2. `DetectFineOcclusion` 가용 여부
현재 model package 에 해당 모델이 포함되어 있는지는 런타임에만 알 수 있다.
없으면 자동 비활성되고 상태바에 `FineOcclusion 사용 불가` 가 뜬다 (앱은 정상 동작).

**기록**: 상태바에 그 문구가 뜨는가.

### 8-3. Occlusion 값의 실제 범위
"가려질수록 값이 높아진다" 는 확인되었으나, `0.5` 임계가 적절한지는 실측이 필요하다.

**확인 방법**: 손으로 눈을 가리며 우측 `Occlusion L/R` 값을 본다.
가리지 않았을 때와 가렸을 때의 값을 기록하고, 그 사이에 `OcclusionMax` 가 오도록 조정.

### 8-4. 성능 실측
**기록**: FPS, `SrcRotateDegrees` 0 과 90 각각에서.

---

## 9. 결과 보고 양식

```
[환경] Windows __ / CPU __ / 카메라 __
[1] setup-runtime.bat        : OK / 실패(사유)
[2] 빌드                     : OK / 실패(사유)
    OpenCvSharpExtern.dll 자동복사 : 됨 / 안 됨(수동 추가)
[3] SDK 초기화               : OK (____ms) / 실패(Error=____)
[4] 카메라                   : OK (index=_, api=____, rotate=___) / 실패
    FPS                      : ____
[5] 재보정 후 EyelidClosedThreshold : ____ (눈뜸 ___~___, 눈감음 ___~___)
[6] 시나리오 A 졸음          : OK / NG(증상)
    시나리오 B 이석          : OK / NG
    시나리오 C 게이트        : OK / NG
    시나리오 D 엎드림        : OK / NG
[7] 로그 3종 생성            : OK / NG
[8-1] faceId 변경 횟수/60초  : ____
[8-2] FineOcclusion          : 사용 가능 / 사용 불가
[8-3] Occlusion 값 (미가림 / 가림) : ____ / ____
[8-4] FPS (rotate=0 / rotate=90)   : ____ / ____
```

---

*근거: FASMH-94 (2026-08-21) / FaceSDK-1.17.7_880.20260514174316 / docs/CONTRACT.md 부록 A*
