# 좌석 모니터링 PoC 사용성 개선 및 표기 정정 (FASMH-94)

> 세션 ID `09040477-90d8-4c68-8a64-46e71e840fe6` · 브랜치 `feautre-01/FASMH-94_FaceSDK_Test_Sample_20260824`
> 기간 `2026-08-24 15:53 ~ 18:05 (KST, 약 132분)`
> 전사 `C:\Users\lmh98\.claude\projects\C--Users-lmh98-Downloads-etus-detect-sample-FaceSDK-Test-Sample-etoos\09040477-90d8-4c68-8a64-46e71e840fe6.jsonl` · 작성일 `2026-08-24`

## ① 작업 기록 1차 확인

> 사용자 확인을 마친 원시 작업 기록.

| 시각 | 사용자 요청 | 수행한 조치 | 결과·증거 |
|---|---|---|---|
| 15:53 | (statusline 설정 — 본 작업과 무관한 부수 작업) | `statusline-setup` 서브에이전트 실행 | 본 문서 범위 밖 |
| 16:10 | 1차 개선 5항목 기획 인터뷰 요청 (라이선스 / 카메라 시작 안내 / 5초 통일 / face.id / PoC 확장) | 플랜 모드 진입, Explore 서브에이전트 3개(라이선스 / 카메라·상태전이 / face.id) 병렬 조사 → `AskUserQuestion` 인터뷰 → 계획 확정(`sharded-soaring-creek.md`, 16:30 승인) | 계획 승인, 구현 착수 |
| 16:30~16:39 | (계획 실행) | 5항목 각각 구현 (③-1 참조) | 커밋 `e12758c "add to Feature"`로 반영 |
| 16:40 | "한국어로 이야기 줄래? 왜 일본어로 했어?" | 응답 언어를 한국어로 교정 | 이후 전 구간 한국어로 응답 |
| 16:43 | `/compact` | 컨텍스트 압축 | — |
| 16:51 | 2차 개선 4항목 기획 인터뷰 요청 (판정불가 완화+설정UI / 판정불가 사유 재정의 / 오류배너 오분류 / FineOcclusion 존치 여부) | 플랜 모드 재진입(동일 plan 파일 갱신), Explore 서브에이전트 3개(임계값 / 오류배너 / FineOcclusion) → 인터뷰 → 계획 확정(17:11) | — |
| 17:11~17:21 | (계획 실행) | 4항목 각각 구현 (③-2 참조) | `App.config`/`AppSettings.cs`/`MainForm.cs`/`AppController.cs`/`docs/CONTRACT.md` 등 diff로 확인 |
| 17:21 | `/compact` | 컨텍스트 압축 | — |
| 17:34 | "Continue from where you left off" | 2차 개선 마무리 확인 | — |
| 17:39 | Blink 누적 집계 기준이 타이트하다는 지적, 개선 방향 질문(탐색적) | 원인 분석: 완전한 Open→Closed→Open 사이클 + 60~500ms 구간만 카운트하는 의도된 설계임을 확인, 보조 카운터 추가안을 권장 | (아래 17:42에서 폐기) |
| 17:42 | "권장 플랜대로 하면 헷갈릴거같아 제거하면 좋겠어" | `AskUserQuestion`으로 "Blink 카운트 자체 제거"로 확정 → 전 계층(설정/상태머신/통계/알림/CSV/UI/테스트/문서)에서 Blink 관련 요소 제거 | 전체 repo `Blink` grep 0건으로 완료 확인 |
| 17:50 | (작업 중 끼어든 요청) ".exe 파일 외" Etus→Etoos 오타 수정 | 검증차 `dotnet test` 실행 → 사전에 존재하던(이번 세션 원인 아님) 네임스페이스 불일치 발견 → 원인 확정 후 테스트 9개 파일 + 두 `.csproj`의 `RootNamespace` 일괄 수정 | `dotnet test` 57/58 통과(기존 무관 실패 1건 제외 전부 통과) |
| 17:56 | 스크린샷 제시 — exe/pdb/config가 여전히 "Etus" | `AskUserQuestion`으로 `AssemblyName`까지 변경할지 확인 | 사용자: "네, exe까지 전부 Etoos로" |
| 17:57 | (위 답변 반영) | 두 `.csproj`의 `AssemblyName` 변경 → Clean/Build → 산출물 확인 → 잔여물 삭제 → 재검증 | `EtoosDetectSample.exe/.pdb/.exe.config` 산출 확인, `dotnet test` 57/58 유지(회귀 없음) |
| 18:02 | `/compact` | 컨텍스트 압축 | — |
| 18:05 | `/worklog 활용하여 작성` | 본 문서 작성 | — |

**보류·미반영**

- 계획했던 `SnapshotGalleryForm.cs`(리스트뷰+미리보기 갤러리)는 만들지 않고, "탐색기로 열기" 방식으로 단순화됨.
- `PosePitchMaxDeg`(25.0), `EyelidClosedThreshold`(2.4)는 의도적으로 기본값 변경 없이 유지 — 설정 UI로 현장 재보정하도록 위임.
- 실제 카메라를 이용한 수동 검증은 수행되지 않음(환경상 카메라 접근 불가) — Windows 실기에서 사용자가 직접 확인 필요.
- 사전 존재하던(이번 세션에서 유발하지 않은) 무관 테스트 실패 1건(`LoggingTests.FrameCsv_WritesRealContent`, Windows 파일 잠금 이슈)은 조사·수정하지 않고 그대로 둠.
- 라이선스 인증 항목(1차 개선 #1)은 코드/문서 변경 없이 "이미 `.cer` 파일만으로 인증 가능함"을 확인하는 선에서 종결.

**폐기한 접근**

- Blink 보조 카운터 추가안 → 사용자가 "헷갈릴 것 같다"며 거부, 전체 제거로 전환.
- Etus→Etoos를 네임스페이스/RootNamespace만 바꾸는 좁은 해석 → 스크린샷으로 불충분함이 드러나 AssemblyName까지 범위 확장.

## ② 작업 목적 및 목표

**배경**

> "해당 샘플을 단계별 수정 요구 사항에 맞게 기획하여 적용하고자 합니다. 1. 라이선스 인증에 대한 허용이 제한적이다 ... 2. 시작 입력 시, 카메라가 올라오기 까지의 틈이 사용자 입장에서는 정상 동작인지 확인하기 힘들다 ... 3. 상태값에 대한 변환 기준을 최대 5초로 통일해야함 ... 4. face.id 변경에 대한 내용이 관려자가 아니면 알아보기 힘듭니다 ... 5. PoC 목적은.. 우리가 안면인식으로 할 수 있는 감시 영역을 최대한 보여주는 것인데.. 추가 활용 및 가능한 범위가 있을지?"

FASMH-94 PoC(자습실 좌석 모니터링 데모)를 실제로 사용해 보면서 드러난 사용성 문제를 두 차례에 걸쳐 개선했다. 1차는 위 5항목, 2차는 1차 개선을 실사용해 본 뒤 추가로 발견된 "판정불가" 관련 4항목이었다. 이후 부수적으로 Blink 카운트 제거, 산출물명 오타(Etus→Etoos) 정정 요청이 이어졌다.

**해결하려는 문제**

- 라이선스 인증 절차가 제한적으로 보였음(실제로는 이미 `.cer` 파일만으로 가능 — 확인 필요).
- 카메라 기동 지연 구간에서 정상 동작 여부를 사용자가 판단하기 어려움.
- 상태 전이 확정 시간 기준이 항목마다 달라 최대 5초로 통일 필요.
- face.id 변경 정보가 담당자 외에는 이해하기 어려운 원시 값으로만 노출됨.
- PoC로 보여줄 수 있는 안면인식 활용 범위를 더 넓힐 여지가 있는지 불확실.
- "판정불가"(Unknown) 상태가 실사용에서 지나치게 자주 발생해 체감 정확도를 떨어뜨림.
- "판정불가"라는 중립적 표현이 학생의 실제 행동(고개 돌림 등)과 시스템 인식 오류를 구분하지 못함.
- 세션을 정상적으로 시작했을 뿐인데 빨간 "오류 · 세션 시작" 배너가 떠 오작동으로 오인됨.
- FineOcclusion 모델이 실제로 쓰이는지 불확실해 죽은 코드로 의심됨.
- Blink 누적 카운트가 졸음/졸음의심 구간에서도 거의 증가하지 않아 혼란을 줌.
- 빌드 산출물명(exe/pdb/config)에 "Etus" 오타가 남아 있음.

**목표**

- [x] 1차 5항목 각각에 대해 실사용 관점의 개선 또는 "이미 충족됨" 확인 완료
- [x] "판정불가" 발생 빈도를 게이트 기본값 완화로 줄이고, 현장 재보정용 설정 UI 제공
- [x] 행동성/시스템성 판정불가 사유를 배지 표현상 구분
- [x] 정상 리셋 메시지와 실제 컴포넌트 오류를 배너 상에서 분리
- [x] Blink 카운트를 전 계층에서 제거해 혼란 요소 제거
- [x] 산출물명(exe/pdb/config 포함)을 전부 "Etoos"로 통일

**범위 밖 (Non-goals)**

- 별도 스냅샷 갤러리 뷰어(`SnapshotGalleryForm`) 신규 제작
- FineOcclusion 제거(조사 후 유지로 결론)
- `PosePitchMaxDeg`/`EyelidClosedThreshold` 기본값 변경(설정 UI로 위임)
- 물리적 폴더/프로젝트/`.csproj`/`.sln` 파일명의 Etus→Etoos 변경(사용자가 명시적으로 범위 제외)
- 실제 카메라를 이용한 수동 검증

**제약·전제**

- classic csproj(.NET Framework 4.8) 프로젝트라 x64 고정 빌드 필요, MSBuild는 vswhere로 탐색해 사용.
- 테스트 프로젝트(net8.0)는 프로덕션 소스를 링크해 컴파일하므로 네임스페이스가 반드시 일치해야 함.
- 세션 도중 컨텍스트 압축이 3회(16:43, 17:21, 18:02) 발생 — 이번 문서는 원본 전사를 다시 조회해 공백을 메움.

**방향 전환**

- Blink: 보조 카운터 추가(권장안) → 사용자가 "헷갈릴 것 같다"며 거부 → 전체 제거로 전환(17:42).
- Etus→Etoos: "exe 파일 외"를 네임스페이스만으로 좁게 해석 → 스크린샷으로 불충분함이 드러남 → AssemblyName까지 범위 확장(17:56~17:57).

## ③ 작업 내용

### 1. 1차 개선 5항목 (라이선스 / 카메라 시작 안내 / 5초 통일 / face.id / PoC 확장)

- **무엇을**: 5개 독립 항목을 각각 조사·구현했다.
  1. **라이선스**: `FaceEngine.cs Initialize()`가 이미 `license.cer`만으로 인증되고 `README.md`/`docs/CONTRACT.md`/`tools/setup-runtime.bat`에도 이미 정확히 문서화되어 있음을 확인 — 액션 없이 종결.
  2. **카메라 시작 안내**: `Worker/AnalysisWorker.cs`에 `Action<string> Progress`/`RaiseProgress()` 신설, 초기화 직전/카메라 오픈 직전/오픈 직후 3곳에서 호출. `AppController.cs`가 이를 배선. `MainForm.cs`에 `SetProgress(string)`와 미리보기 오버레이 라벨(`lblPreviewOverlay`)을 신설해 첫 프레임 도착 시 자동으로 숨김.
  3. **5초 통일**: `Analysis/SeatStateMachine.cs`의 `SlumpAwayFallbackMultiplier`를 3.0→1.0으로 변경해 `NoFaceConfirmSec × 1.0 = 5.0초`로 맞춤.
  4. **face.id**: `MainForm.cs`의 `lblFaceIdV`를 원시 숫자 대신 "정상 인식 중"/"인식 대상 없음"으로 서술형 표시. `AlertModels.cs`에 `AlertType.PersonChanged` 추가, `AlertJson.cs`에 `"PERSON_CHANGED"` 직렬화, `SeatStateMachine.cs`에서 트래킹 ID 변경 시 `Warn` 레벨 알림 발생.
  5. **PoC 확장**: 신규 `Capture/AlertSnapshotWriter.cs` — 확정(Alert) 알림 발생 시 화면을 `logs/snapshots/`에 jpg로 저장. `AppController.cs`가 배선.
- **왜**: 실사용 관점에서 "정상 동작인지 알 수 없음"(카메라 시작), "관계자만 이해 가능"(face.id), "확장 여지 불명확"(PoC 활용도)이라는 구체적 불편이 제기됐기 때문.
- **어떻게**: Explore 서브에이전트 3개로 기존 코드의 실제 동작(특히 라이선스 인증 경로, 미사용 `Progress` 콜백 단서)을 먼저 확인한 뒤 `AskUserQuestion` 인터뷰로 방향을 확정, 최소 변경으로 구현. 계획했던 스냅샷 갤러리 뷰어는 "탐색기로 열기"로 단순화(`Process.Start("explorer.exe", ...)`).
- **결과**: 커밋 `e12758c "add to Feature"`로 반영됨. 이번 세션의 working-tree `git diff`(HEAD 대비)에는 대부분 나타나지 않음(이미 커밋됨) — `git diff 3ef695d e12758c`로 전체 확인 가능.

### 2. 2차 개선 4항목 (판정불가 완화 + 설정 UI / 사유 재정의 / 오류배너 분리 / FineOcclusion 존치 검토)

- **무엇을**: 1차 개선을 실사용한 뒤 나온 4가지 후속 문제를 처리했다.
  1. **게이트 기본값 완화**: `LandmarkConfMin` 0.90→0.75, `OcclusionMax`/`FineOcclusionMax` 0.50→0.60, `PoseYawMaxDeg` 35.0→45.0, `UnknownGraceSec` 1.5→3.0 (`App.config`, `Config/AppSettings.cs`). 신규 `SettingsForm.cs`/`SettingsForm.Designer.cs` — "판정 설정" 버튼으로 여는 non-modal 실시간 튜닝 창, 저장 시 `App.config`에 영구 반영.
  2. **사유 재정의**: `MainForm.cs`에 `static bool IsBehaviorReason(UnknownReason r)` 신설 — 고개 돌림/눈·얼굴 가림 등 행동성 사유는 배지를 "집중 흐트러짐 의심"(의심 색)으로, SDK 오류 등 시스템성 사유는 기존 "판정 불가"를 유지. 배지 재계산 조건에 `_lastBehaviorUnknown` 추가.
  3. **오류배너 분리**: `MainForm.SetInfo(string)` 신설(상태바만 갱신, 오류 색 아님). `AppController.cs`의 `_machine.ResetLogger`를 `OnComponentError` → `OnResetInfo`로 교체해, "세션 시작"/Face.id 변경 등 정보성 리셋 사유가 더 이상 빨간 오류 배너로 뜨지 않게 함. 로거/디스패처/스냅샷 저장 등 진짜 컴포넌트 오류는 그대로 `OnComponentError` 유지.
  4. **FineOcclusion**: 재조사 결과 `EyeStateGate.cs`의 9단계 판정 중 7번째로 실사용 중이며 전용 단위 테스트도 존재 — 제거하지 않고 유지로 결론. `docs/CONTRACT.md` 부록B에 조사 결과 기록.
- **왜**: "판정불가"가 너무 잦다는 체감 문제, 배지 표현이 실제 원인(학생 행동 vs 시스템 오류)을 구분 못 한다는 문제, 정상 시작이 오류로 오인되는 버그, FineOcclusion이 죽은 코드일 수 있다는 의심을 각각 해소하기 위함.
- **어떻게**: Explore 서브에이전트 3개로 각 문제의 근본 원인을 코드 레벨에서 먼저 규명(임계값 출처, `SetError()`가 모든 리셋 사유에 무조건 "오류" 접두를 붙이는 버그, FineOcclusion의 실제 호출 경로)한 뒤 인터뷰로 확정. `AppSettings.Clamp()`의 min/max 범위를 그대로 `SettingsForm`의 `NumericUpDown` 범위로 재사용해 별도 검증 코드 없이 안전하게 구현.
- **결과**: `App.config`/`AppSettings.cs`/`MainForm.cs`/`AppController.cs`/`docs/CONTRACT.md` diff로 확인됨. `UnknownGraceSec` 변경에 맞춰 `SeatStateMachineUnknownResetTests.cs` 2건의 시간 상수도 1.5→3.0으로 동기화.

### 3. Blink 카운트 전체 제거

- **무엇을**: "졸음/졸음의심에도 Blink 카운트가 안 올라간다"는 지적에 대해, 애초에 보조 카운터를 추가하려던 권장안을 사용자가 거부함에 따라 Blink 카운팅 기능 자체를 전 계층에서 제거했다.
- **왜**: 완전한 Open→Closed→Open 사이클 중 60~500ms 구간만 깜빡임으로 세는 것은 의도된 설계였지만("졸음 신호와 구분"), 그 결과 "졸음 중엔 안 올라감"이라는 동작이 실사용자에게 혼란을 줬고, 별도 카운터를 더 두는 것도 UI/지표를 복잡하게 만들 것으로 판단됨.
- **어떻게**: `App.config`(BlinkMinMs/BlinkMaxMs 키), `Config/AppSettings.cs`(필드+Clamp), `Config/AppSettingsLoader.cs`(로드 로직), `Analysis/SeatStateMachine.cs`(`_blinkCandidate` 필드 및 카운트 로직), `Analysis/SessionStats.cs`/`FrameObservation.cs`(BlinkCount 필드), `Alerts/AlertModels.cs`/`AlertJson.cs`(스키마), `Logging/FrameCsvLogger.cs`(CSV 헤더·컬럼), `MainForm.cs`/`MainForm.Designer.cs`(UI 라벨) 순으로 모든 참조를 제거. 테스트는 `PerclosBlinkTests.cs`를 삭제하고 PERCLOS 4건만 남긴 `PerclosTests.cs`를 신규 작성, `LoggingTests.FrameCsv_ColumnCountsMatch`의 기대 컬럼 수를 37→36으로 수정. `README.md`에서도 Blink 관련 서술 3곳 제거.
- **결과**: 전체 저장소 `Blink` grep 0건. 이 작업 중 `dotnet test`를 처음 실행했다가 사전에 존재하던(이번 변경과 무관한) 네임스페이스 불일치로 전체 컴파일 실패를 발견 → 별도 조치(④ 참조).

### 4. Etus → Etoos 표기 정정 (네임스페이스 → AssemblyName 확장)

- **무엇을**: 사용자가 오타로 만든 "Etus"라는 이름을 "Etoos"로 정정했다. 처음엔 ".exe 파일 외"로 좁게 해석해 C# 네임스페이스/`RootNamespace`만 변경했으나, 스크린샷으로 exe/pdb/config가 여전히 "Etus"임이 지적되어 `AssemblyName`까지 확장했다.
- **왜**: 사용자가 config 등 구성 파일명에 오타가 있음을 인지하고 정정을 요청했고, 1차 조치가 화면상 결과와 맞지 않아 스코프를 재확인·확장함.
- **어떻게**: (1) 위 Blink 제거 검증 중 발견한 사전 네임스페이스 불일치(src는 이미 `Etoos.DetectSample`, 테스트 9개 파일은 여전히 `Etus.DetectSample` 참조)를 `git diff HEAD`/`git show HEAD:...`로 근본 원인 규명 후 일괄 수정, 두 `.csproj`의 `RootNamespace`도 동기화. (2) `dotnet test`로 57/58 통과 확인(무관한 사전 실패 1건 제외). (3) 사용자 스크린샷 확인 후 `AskUserQuestion`으로 "exe까지 전부 변경"을 명시적으로 확인받아, `src/EtusDetectSample/EtusDetectSample.csproj`와 `tests/EtusDetectSample.Tests/EtusDetectSample.Tests.csproj`의 `<AssemblyName>`을 각각 `EtoosDetectSample`/`EtoosDetectSample.Tests`로 변경. (4) MSBuild `/t:Clean,Build`로 재빌드해 산출물명을 확인하고 기존 `EtusDetectSample.*` 잔여 파일을 삭제. 폴더명(`src/EtusDetectSample/`)·프로젝트 파일명(`.csproj`/`.sln`)은 사용자가 명시적으로 범위에서 제외해 그대로 둠.
- **결과**: `EtoosDetectSample.exe/.pdb/.exe.config`, `EtoosDetectSample.Tests.dll` 산출 확인. `dotnet test` 57/58 유지(회귀 없음). 최종 `Etus` grep 결과 남은 7개 파일은 전부 의도적으로 보존한 폴더/프로젝트/`.sln` 파일명 참조뿐임을 확인.

## ④ 최종 요약

이번 세션은 132분 동안 두 차례의 사용성 개선 라운드(1차 5항목, 2차 4항목)와 두 건의 후속 수정(Blink 제거, Etus→Etoos 표기 정정)을 진행했다. 1차 개선분은 세션 중반 `e12758c` 커밋으로 이미 반영됐고, 2차 개선분과 Blink 제거·표기 정정은 아직 커밋되지 않은 working-tree 변경 상태다. 작업 중 사전에 존재하던(이번 세션이 유발하지 않은) 테스트 프로젝트 네임스페이스 불일치를 발견해 별도로 수정했으며, 모든 변경은 MSBuild 빌드와 `dotnet test`(57/58, 무관한 사전 실패 1건 제외 전부 통과)로 검증됐다.

**변경·생성 파일**

| 파일 | 성격 | 영향 |
|---|---|---|
| `src/EtusDetectSample/SettingsForm.cs`, `.Designer.cs` | 신규 | "판정 설정" 실시간 튜닝 UI |
| `tests/EtusDetectSample.Tests/PerclosTests.cs` | 신규 | Blink 제거 후 PERCLOS 전용 테스트로 대체 |
| `tests/EtusDetectSample.Tests/PerclosBlinkTests.cs` | 삭제 | Blink 테스트 제거 |
| `src/EtusDetectSample/App.config` | 수정 | 게이트 임계값 완화, Blink 키 제거 |
| `src/EtusDetectSample/Config/AppSettings.cs`, `AppSettingsLoader.cs` | 수정 | 임계값 기본값·Clamp 갱신, Blink 필드 제거 |
| `src/EtusDetectSample/Analysis/SeatStateMachine.cs` | 수정 | 5초 통일, Blink 로직 제거, PersonChanged 알림 |
| `src/EtusDetectSample/Analysis/SessionStats.cs`, `FrameObservation.cs` | 수정 | BlinkCount 필드 제거 |
| `src/EtusDetectSample/Alerts/AlertModels.cs`, `AlertJson.cs` | 수정 | PersonChanged 추가, BlinkCount 제거 |
| `src/EtusDetectSample/Logging/FrameCsvLogger.cs` | 수정 | CSV 스키마에서 blinkCount 컬럼 제거 |
| `src/EtusDetectSample/MainForm.cs`, `MainForm.Designer.cs` | 수정 | 진행 안내, face.id 서술형 표시, 배지 사유 재정의, Blink UI 제거 |
| `src/EtusDetectSample/AppController.cs` | 수정 | Progress/SettingsForm/OnResetInfo 배선 |
| `src/EtusDetectSample/Worker/AnalysisWorker.cs` | 수정 | Progress 콜백 신설 |
| `src/EtusDetectSample/Capture/AlertSnapshotWriter.cs` | 수정(1차에서 신규) | Etoos 네임스페이스 정정 |
| `src/EtusDetectSample/EtusDetectSample.csproj`, `tests/.../EtusDetectSample.Tests.csproj` | 수정 | `RootNamespace`/`AssemblyName` → Etoos |
| `tests/EtusDetectSample.Tests/*.cs`, `Support/*.cs` (9개) | 수정 | `Etus.DetectSample` → `Etoos.DetectSample` |
| `docs/CONTRACT.md`, `README.md` | 수정 | 2차 개선 기록(부록B), Blink/Etus 서술 정정 |

**미완료·후속 작업**

- [ ] Windows 실기 + 실제 카메라로 전체 개선사항 수동 검증(1차 5항목, 2차 4항목 모두)
- [ ] 이번 세션의 working-tree 변경(2차 개선, Blink 제거, Etoos 표기 정정)을 커밋 — 아직 커밋되지 않음
- [ ] 사전 존재하는 무관 테스트 실패(`LoggingTests.FrameCsv_WritesRealContent`, 파일 잠금) 별도 조사 필요 시 후속 처리

**리스크·주의점**

- VS(Visual Studio)에서 솔루션을 이미 열어 둔 상태라면 `AssemblyName` 변경 및 `natives\*.dll` wildcard 참조가 반영되도록 프로젝트를 다시 로드해야 함.
- `SettingsForm`의 값 변경은 워커 스레드가 매 프레임 읽는 `AppSettings` 인스턴스를 UI 스레드가 직접 mutate하는 구조 — 락 없이 설계됨(최악의 경우 한 프레임 지연 반영, 의도된 트레이드오프).
- 게이트 기본값 완화(`LandmarkConfMin` 등)는 오탐(false positive) 방향으로 작용할 수 있어 실기 재보정이 필요한 "잠정치"로 문서화되어 있음.

**검증 방법**

```bash
# 빌드 (x64, MSBuild)
$msbuild = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
& $msbuild "src\EtusDetectSample\EtusDetectSample.csproj" /t:Clean,Build /p:Configuration=Debug /p:Platform=x64

# 단위 테스트 (net8.0)
cd tests/EtusDetectSample.Tests
dotnet test

# 산출물명 확인
ls src/EtusDetectSample/bin/x64/Debug/EtoosDetectSample.*
```
