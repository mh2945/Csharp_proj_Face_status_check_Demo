using System;
using System.Globalization;

namespace Etus.DetectSample.Config
{
    /// <summary>
    /// 판정 임계값 전체. App.config(appSettings)에서 읽고, 없으면 아래 기본값을 쓴다.
    /// 배포된 EtusDetectSample.exe.config 를 편집하면 재빌드 없이 현장 재보정이 가능하다.
    ///
    /// 이 타입은 Analysis 계층이 참조하므로 외부 의존을 두지 않는다.
    /// (App.config 읽기는 FromConfiguration() 한 곳에서만 System.Configuration 을 쓴다)
    /// </summary>
    public sealed class AppSettings
    {
        // --- 좌석 ---
        public string SeatId = "S-01";

        // --- 눈 개폐 판정 ---
        /// <summary>eyelid distance &lt;= 이 값이면 Closed. 근거리 셀피 기준 잠정치 — 현장 재보정 필수.</summary>
        public double EyelidClosedThreshold = 2.4;

        // --- 게이트 ---
        public double LandmarkConfMin = 0.90;
        public double OcclusionMax = 0.50;
        public double FineOcclusionMax = 0.50;
        /// <summary>
        /// FineOcclusion 을 게이트에 반영할지.
        /// 값의 방향은 확인됨 — <b>가려질수록 값이 높아진다</b>(Occlusion 과 동일).
        /// 다만 해당 모델이 현재 model package 에 포함되어 있는지는 런타임에만 알 수 있으므로,
        /// FaceEngine 이 FineOcclusionAvailable=false 로 내리면 이 설정과 무관하게 무시된다.
        /// </summary>
        public bool UseFineOcclusionGate = true;
        public double PoseYawMaxDeg = 35.0;
        public double PosePitchMaxDeg = 25.0;

        // --- 시간 임계 (초) ---
        public double ClosedEyeSuspectSec = 2.0;
        public double ClosedEyeConfirmSec = 5.0;   // FASMH-94 명시
        public double NoFaceSuspectSec = 2.0;
        public double NoFaceConfirmSec = 5.0;      // FASMH-94 명시
        public double UnknownGraceSec = 1.5;
        public double AlertCooldownSec = 15.0;

        // --- PERCLOS ---
        public double PerclosWindowSec = 60.0;
        public double PerclosSuspectRatio = 0.35;

        // --- Blink ---
        public double BlinkMinMs = 60.0;
        public double BlinkMaxMs = 500.0;

        // --- 엎드림 휴리스틱 ---
        public bool EnableSlumpHeuristic = true;

        // --- 프레임 유실 ---
        /// <summary>직전 프레임과의 간격이 이 값을 넘으면 카운터를 리셋한다.</summary>
        public double FrameGapResetSec = 1.0;

        // --- tracking ---
        /// <summary>Face.id 변경 시 상태를 리셋할지. id 가 불안정하면 false 로 내린다.</summary>
        public bool ResetOnTrackIdChange = true;

        // --- 카메라 / 성능 ---
        public int CameraIndex = 0;
        public int CaptureWidth = 1280;
        public int CaptureHeight = 720;
        public string CaptureCodec = "MJPG";
        public bool FlipHorizontal = true;

        // --- 분석 입력 이미지 (성능/정확도 튜닝의 핵심) ---
        // SDK 에 넘길 이미지는 720x1280 세로 기준으로 진행한다.
        // 대부분의 웹캠은 1280x720 가로만 지원하므로, 캡처 후
        //   [회전] → [종횡비 중앙 크롭] → [리사이즈] 순으로 맞춘다.
        /// <summary>SDK 에 넘길 이미지 폭.</summary>
        public int AnalyzeWidth = 720;
        /// <summary>SDK 에 넘길 이미지 높이.</summary>
        public int AnalyzeHeight = 1280;
        /// <summary>캡처 직후 적용할 회전각. 0 / 90 / 180 / 270 만 허용. 카메라를 세로로 설치했으면 0.</summary>
        public int SrcRotateDegrees = 0;
        /// <summary>회전 후 AnalyzeWidth:AnalyzeHeight 종횡비로 중앙 크롭할지.</summary>
        public bool EnableAspectCrop = true;
        /// <summary>크롭 후 AnalyzeWidth x AnalyzeHeight 로 리사이즈할지. false 면 크롭 결과를 그대로 넘긴다.</summary>
        public bool EnableAnalyzeResize = true;
        /// <summary>CheckMask / DetectFineOcclusion 호출 주기(프레임). 1이면 매 프레임.</summary>
        public int AttrIntervalFrames = 5;
        /// <summary>목표 분석 FPS. 0 이면 제한 없음(카메라 속도에 맡김).</summary>
        public double TargetAnalyzeFps = 10.0;

        // --- 로깅 ---
        public bool EnableFrameCsv = true;
        public string LogDirectory = "logs";

        /// <summary>범위를 벗어난 값을 기본값 쪽으로 되돌린다. 잘못된 config 로 앱이 이상 동작하는 것을 막는다.</summary>
        public void Clamp()
        {
            EyelidClosedThreshold = ClampD(EyelidClosedThreshold, 0.1, 50.0, 2.4);
            LandmarkConfMin = ClampD(LandmarkConfMin, 0.0, 1.0, 0.90);
            OcclusionMax = ClampD(OcclusionMax, 0.0, 1.0, 0.50);
            FineOcclusionMax = ClampD(FineOcclusionMax, 0.0, 1.0, 0.50);
            PoseYawMaxDeg = ClampD(PoseYawMaxDeg, 5.0, 90.0, 35.0);
            PosePitchMaxDeg = ClampD(PosePitchMaxDeg, 5.0, 90.0, 25.0);

            ClosedEyeSuspectSec = ClampD(ClosedEyeSuspectSec, 0.2, 60.0, 2.0);
            ClosedEyeConfirmSec = ClampD(ClosedEyeConfirmSec, 0.3, 120.0, 5.0);
            if (ClosedEyeConfirmSec <= ClosedEyeSuspectSec)
                ClosedEyeConfirmSec = ClosedEyeSuspectSec + 0.1;

            NoFaceSuspectSec = ClampD(NoFaceSuspectSec, 0.2, 60.0, 2.0);
            NoFaceConfirmSec = ClampD(NoFaceConfirmSec, 0.3, 120.0, 5.0);
            if (NoFaceConfirmSec <= NoFaceSuspectSec)
                NoFaceConfirmSec = NoFaceSuspectSec + 0.1;

            UnknownGraceSec = ClampD(UnknownGraceSec, 0.0, 30.0, 1.5);
            AlertCooldownSec = ClampD(AlertCooldownSec, 0.0, 600.0, 15.0);
            PerclosWindowSec = ClampD(PerclosWindowSec, 5.0, 600.0, 60.0);
            PerclosSuspectRatio = ClampD(PerclosSuspectRatio, 0.0, 1.0, 0.35);
            BlinkMinMs = ClampD(BlinkMinMs, 10.0, 2000.0, 60.0);
            BlinkMaxMs = ClampD(BlinkMaxMs, 20.0, 5000.0, 500.0);
            if (BlinkMaxMs <= BlinkMinMs) BlinkMaxMs = BlinkMinMs + 10.0;

            FrameGapResetSec = ClampD(FrameGapResetSec, 0.1, 30.0, 1.0);

            if (AttrIntervalFrames < 1) AttrIntervalFrames = 1;
            if (AttrIntervalFrames > 60) AttrIntervalFrames = 60;
            if (TargetAnalyzeFps < 0) TargetAnalyzeFps = 0;
            if (TargetAnalyzeFps > 120) TargetAnalyzeFps = 120;
            if (CameraIndex < 0) CameraIndex = 0;
            if (CaptureWidth < 160) CaptureWidth = 1280;
            if (CaptureHeight < 120) CaptureHeight = 720;

            if (AnalyzeWidth < 160 || AnalyzeWidth > 4096) AnalyzeWidth = 720;
            if (AnalyzeHeight < 120 || AnalyzeHeight > 4096) AnalyzeHeight = 1280;
            // 회전각은 90 배수만. 그 외 값은 회전 없음으로 되돌린다.
            SrcRotateDegrees = ((SrcRotateDegrees % 360) + 360) % 360;
            if (SrcRotateDegrees != 0 && SrcRotateDegrees != 90 &&
                SrcRotateDegrees != 180 && SrcRotateDegrees != 270)
                SrcRotateDegrees = 0;
            if (string.IsNullOrEmpty(SeatId)) SeatId = "S-01";
            if (string.IsNullOrEmpty(LogDirectory)) LogDirectory = "logs";
        }

        static double ClampD(double v, double min, double max, double fallback)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "eyelid<={0} lmConf>={1} occl<={2} drowsy {3}/{4}s away {5}/{6}s perclos {7}s/{8} attrEvery={9}",
                EyelidClosedThreshold, LandmarkConfMin, OcclusionMax,
                ClosedEyeSuspectSec, ClosedEyeConfirmSec,
                NoFaceSuspectSec, NoFaceConfirmSec,
                PerclosWindowSec, PerclosSuspectRatio, AttrIntervalFrames);
        }
    }
}
