using System;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// EyeStateGate — "믿을 수 없는 프레임은 Open/Closed 로 단정하지 않는다"가 유일한 책임.
    /// 잘못된 Closed 하나가 5초 뒤 오알림이 되므로 여기가 오탐 방어선이다.
    /// </summary>
    public class EyeStateGateTests
    {
        private static AppSettings Settings()
        {
            return new AppSettings();
        }

        /// <summary>기본값이 전부 정상인 프레임 1장.</summary>
        private static FrameObservation NormalFrame()
        {
            return Seq.Start().Open(0.125).Build()[0];
        }

        [Fact(DisplayName = "정상 프레임 — eyelid 임계 초과면 Open, 이하면 Closed")]
        public void NormalFrame_ResolvesOpenAndClosed()
        {
            EyeStateGate gate = new EyeStateGate(Settings());

            FrameObservation open = NormalFrame();
            EyeDecision d1 = gate.Evaluate(open);
            Assert.Equal(EyeState.Open, d1.State);
            Assert.Equal(UnknownReason.None, d1.Reason);
            Assert.Equal(Presence.Present, d1.Presence);

            FrameObservation closed = NormalFrame();
            closed.EyelidLeft = 1.8f;
            closed.EyelidRight = 1.9f;
            EyeDecision d2 = gate.Evaluate(closed);
            Assert.Equal(EyeState.Closed, d2.State);
            Assert.Equal(Presence.Present, d2.Presence);
        }

        [Fact(DisplayName = "경계값 — eyelid 가 정확히 임계값(2.4)이면 Closed")]
        public void EyelidExactlyAtThreshold_IsClosed()
        {
            // AppSettings 문서: "eyelid distance <= 이 값이면 Closed".
            // float/double 정밀도 차이로 이 경계가 뒤집히면 현장 재보정 값이 의도대로 안 먹는다.
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.EyelidLeft = 2.4f;
            o.EyelidRight = 2.4f;

            Assert.Equal(EyeState.Closed, gate.Evaluate(o).State);
        }

        [Fact(DisplayName = "EyelidValid = false 면 값이 0 이어도 Closed 로 읽지 않는다")]
        public void EyelidInvalid_NeverReadsZeroAsClosed()
        {
            // 가장 중요한 안전장치.
            // SDK 는 IsOk() 가 아닐 때 eyelid 를 0 으로 남긴다(CONTRACT.md 1절).
            // 0 은 임계값 2.4 이하라 그대로 두면 "양쪽 눈 감김"으로 읽혀 5초 뒤 오알림이 된다.
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.EyelidLeft = 0f;
            o.EyelidRight = 0f;
            o.EyelidValid = false;

            EyeDecision d = gate.Evaluate(o);
            Assert.Equal(EyeState.Unknown, d.State);
            Assert.Equal(UnknownReason.EyelidUnavailable, d.Reason);
            Assert.NotEqual(EyeState.Closed, d.State);
            // 눈은 못 봐도 얼굴은 있다 → 착석은 유지되어야 SeatedSec 이 정확하다.
            Assert.Equal(Presence.Present, d.Presence);
        }

        [Fact(DisplayName = "EyelidValid = true 인데 값이 NaN 이면 EyelidUnavailable")]
        public void EyelidNaN_IsUnavailableNotAsymmetric()
        {
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.EyelidLeft = float.NaN;
            o.EyelidRight = float.NaN;
            o.EyelidValid = true;

            EyeDecision d = gate.Evaluate(o);
            Assert.Equal(UnknownReason.EyelidUnavailable, d.Reason);
        }

        [Fact(DisplayName = "한쪽 눈만 감김 → AsymmetricEye (판정 제외)")]
        public void OneEyeClosed_IsAsymmetric()
        {
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.EyelidLeft = 1.8f;    // 감김
            o.EyelidRight = 3.9f;   // 뜸

            EyeDecision d = gate.Evaluate(o);
            Assert.Equal(EyeState.Unknown, d.State);
            Assert.Equal(UnknownReason.AsymmetricEye, d.Reason);
            Assert.Equal(Presence.Present, d.Presence);
        }

        [Fact(DisplayName = "게이트 우선순위 — 먼저 오는 조건이 이긴다")]
        public void GatePriority_FirstMatchWins()
        {
            // 순서: SdkError → NoFace → LandmarkConf → Pose → Occlusion → FineOcclusion → EyelidValid
            EyeStateGate gate = new EyeStateGate(Settings());

            // 모든 조건을 동시에 위반시킨 프레임에서 하나씩 걷어내며 사유가 순서대로 바뀌는지 본다.
            FrameObservation o = NormalFrame();
            o.SdkError = "DetectFace 실패";
            o.FaceDetected = false;
            o.LandmarkConfidence = 0.10f;
            o.Pitch = 60f;
            o.OcclusionLeftEye = 0.99f;
            o.FineOcclusion = 0.99f;
            o.EyelidValid = false;

            Assert.Equal(UnknownReason.SdkError, gate.Evaluate(o).Reason);
            // SdkError 는 "아무것도 믿지 않는다" → 부재로 본다.
            Assert.Equal(Presence.Absent, gate.Evaluate(o).Presence);

            o.SdkError = null;
            Assert.Equal(UnknownReason.NoFace, gate.Evaluate(o).Reason);
            Assert.Equal(Presence.Absent, gate.Evaluate(o).Presence);

            o.FaceDetected = true;
            Assert.Equal(UnknownReason.LowLandmarkConfidence, gate.Evaluate(o).Reason);
            Assert.Equal(Presence.Present, gate.Evaluate(o).Presence);

            o.LandmarkConfidence = 0.97f;
            Assert.Equal(UnknownReason.PoseOutOfRange, gate.Evaluate(o).Reason);

            o.Pitch = 4f;
            Assert.Equal(UnknownReason.EyeOccluded, gate.Evaluate(o).Reason);

            o.OcclusionLeftEye = 0.02f;
            Assert.Equal(UnknownReason.FineOccluded, gate.Evaluate(o).Reason);

            o.FineOcclusion = 0.04f;
            Assert.Equal(UnknownReason.EyelidUnavailable, gate.Evaluate(o).Reason);

            o.EyelidValid = true;
            Assert.Equal(EyeState.Open, gate.Evaluate(o).State);
        }

        [Fact(DisplayName = "NaN(미측정) 속성값은 게이트를 통과시킨다 — 코드 주석대로의 의도된 동작")]
        public void NaNAttributes_PassTheGate()
        {
            // CheckMask / DetectFaceOcclusion / DetectFineOcclusion 은 AttrIntervalFrames(기본 5)
            // 주기로만 호출되므로 대부분의 프레임에서 NaN 이다. NaN 을 Unknown 으로 처리하면
            // 5프레임 중 4프레임이 Unknown 이 되어 시간 판정 자체가 불가능해진다.
            // → "미측정 프레임은 가림 게이트를 건너뛴다"가 EyeStateGate.IsOccluded 의 명시된 설계다.
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.LandmarkConfidence = float.NaN;
            o.OcclusionLeftEye = float.NaN;
            o.OcclusionRightEye = float.NaN;
            o.OcclusionMouth = float.NaN;
            o.FineOcclusion = float.NaN;

            EyeDecision d = gate.Evaluate(o);
            Assert.Equal(EyeState.Open, d.State);
            Assert.Equal(UnknownReason.None, d.Reason);
        }

        [Fact(DisplayName = "FineOcclusionAvailable = false 면 설정과 무관하게 정밀 가림 게이트를 건너뛴다")]
        public void FineOcclusionUnavailable_SkipsGate()
        {
            // 모델 미포함으로 앱이 죽거나 전 프레임이 Unknown 이 되면 안 된다(CONTRACT.md 5절).
            AppSettings s = Settings();
            Assert.True(s.UseFineOcclusionGate, "부록 A 4번에 따라 기본값은 true 여야 한다");

            EyeStateGate gate = new EyeStateGate(s);
            FrameObservation o = NormalFrame();
            o.FineOcclusion = 0.99f;
            o.FineOcclusionAvailable = false;

            Assert.Equal(EyeState.Open, gate.Evaluate(o).State);
        }

        [Fact(DisplayName = "마스크 착용은 눈 판정 게이트에 걸리지 않는다 (현재 구현의 계약)")]
        public void MaskedFace_DoesNotBlockEyeDecision()
        {
            // IsMasked 는 Rst.IsFaceMasked() 결과를 그대로 옮긴 값이고(반전 재구현 금지),
            // EyeStateGate 는 이 값을 판정에 쓰지 않는다. 마스크는 입을 가리지 눈을 가리지 않기 때문.
            // → 시연 시나리오에서 "마스크 착용 = 판정불가"를 기대하면 안 된다(WINDOWS_TEST_GUIDE 참조).
            EyeStateGate gate = new EyeStateGate(Settings());
            FrameObservation o = NormalFrame();
            o.IsMasked = true;
            o.MaskConfidence = 0.12f;   // 낮을수록 착용 — 이 값으로 재계산하는 코드가 있으면 안 된다

            EyeDecision d = gate.Evaluate(o);
            Assert.Equal(EyeState.Open, d.State);
            Assert.Equal(UnknownReason.None, d.Reason);
        }

        [Fact(DisplayName = "obs == null 이면 예외 대신 Unknown/Absent")]
        public void NullObservation_IsHandled()
        {
            EyeStateGate gate = new EyeStateGate(Settings());
            EyeDecision d = gate.Evaluate(null);

            Assert.Equal(EyeState.Unknown, d.State);
            Assert.Equal(UnknownReason.SdkError, d.Reason);
            Assert.Equal(Presence.Absent, d.Presence);
        }

        [Fact(DisplayName = "settings 가 null 이면 생성자에서 거부한다")]
        public void NullSettings_Throws()
        {
            Assert.Throws<ArgumentNullException>(delegate { new EyeStateGate(null); });
        }
    }
}
