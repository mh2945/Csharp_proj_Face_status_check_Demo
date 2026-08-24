using System;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// 졸음 / 이석 판정. FASMH-94 가 명시한 값(의심 2초, 확정 5초)이 요구사항 원본이다.
    /// </summary>
    public class SeatStateMachineDrowsyTests
    {
        // ==================================================================
        // 졸음
        // ==================================================================

        [Fact(DisplayName = "눈 감김 2.0초 → DrowsySuspect + Warn 1건")]
        public void ClosedEye_2sec_RaisesSuspectWarnOnce()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(2.0));

            Assert.Equal(SeatState.DrowsySuspect, h.State);
            Assert.Equal(2.0, h.Machine.ClosedEyeSec, 9);

            Assert.Equal(1, h.CountOf(AlertType.Drowsy, AlertLevel.Warn));
            Assert.Equal(SeatStateMachine.MsgDrowsySuspect,
                         h.AlertsOf(AlertType.Drowsy, AlertLevel.Warn)[0].Message);

            // 아직 확정은 아니다.
            Assert.Equal(0, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
        }

        [Fact(DisplayName = "눈 감김 5.0초 → Drowsy + Alert + \"학생이 졸고 있습니다\"")]
        public void ClosedEye_5sec_RaisesConfirmedAlert()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(5.0));

            Assert.Equal(SeatState.Drowsy, h.State);
            Assert.Equal(5.0, h.Machine.ClosedEyeSec, 9);

            Assert.Equal(1, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
            AlertEvent a = h.AlertsOf(AlertType.Drowsy, AlertLevel.Alert)[0];
            Assert.Equal("학생이 졸고 있습니다", a.Message);
            Assert.Equal(SeatStateMachine.MsgDrowsyConfirmed, a.Message);

            // 확정 근거 시간이 임계값 이상이어야 한다(표본 간격만큼의 오차는 허용).
            Assert.True(a.DurationSec >= h.Settings.ClosedEyeConfirmSec,
                        "durationSec=" + a.DurationSec.ToString() + " < 5.0");
            Assert.True(a.DurationSec <= h.Settings.ClosedEyeConfirmSec + Seq.Start().Dt,
                        "durationSec=" + a.DurationSec.ToString() + " 가 표본 간격보다 크게 늦었다");
        }

        [Fact(DisplayName = "경계값 — 4.875초(5초 직전 표본)에서 눈을 뜨면 Alert 없음")]
        public void ClosedEye_JustBelowConfirm_DoesNotAlert()
        {
            // 8 FPS 에서 39 프레임 = 4.875초. 5.0 임계를 넘기지 못하는 마지막 표본이다.
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(4.875).Open(1.0));

            Assert.Equal(0, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
            Assert.False(h.EverEntered(SeatState.Drowsy), "Drowsy 로 전이되면 안 된다");
            Assert.Equal(0, h.Stats.DrowsyCount);
            Assert.Equal(SeatState.Awake, h.State);
        }

        [Fact(DisplayName = "Drowsy → 눈 뜸 → Awake + Recovered/Clear 1건")]
        public void Drowsy_ThenEyesOpen_RaisesRecoveredOnce()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(5.0).Open(2.0));

            Assert.Equal(SeatState.Awake, h.State);
            Assert.Equal(1, h.CountOf(AlertType.Recovered, AlertLevel.Clear));
            Assert.Equal(SeatStateMachine.MsgRecovered,
                         h.AlertsOf(AlertType.Recovered, AlertLevel.Clear)[0].Message);
        }

        [Fact(DisplayName = "DrowsyCount 는 확정(Alert) 시에만 증가한다 — 의심만으로는 안 오른다")]
        public void DrowsyCount_IncrementsOnlyOnConfirm()
        {
            Harness suspectOnly = new Harness();
            suspectOnly.Run(Seq.Start().Open(1.0).Closed(2.0).Open(1.0));
            Assert.Equal(1, suspectOnly.CountOf(AlertType.Drowsy, AlertLevel.Warn));
            Assert.Equal(0, suspectOnly.Stats.DrowsyCount);

            Harness confirmed = new Harness();
            confirmed.Run(Seq.Start().Open(1.0).Closed(5.0).Open(1.0));
            Assert.Equal(1, confirmed.Stats.DrowsyCount);
        }

        // ==================================================================
        // 이석
        // ==================================================================

        [Fact(DisplayName = "얼굴 미검출 2.0초 → AwaySuspect + Warn")]
        public void NoFace_2sec_RaisesSuspectWarn()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).NoFace(2.0));

            Assert.Equal(SeatState.AwaySuspect, h.State);
            Assert.Equal(2.0, h.Machine.NoFaceSec, 9);
            Assert.Equal(1, h.CountOf(AlertType.Away, AlertLevel.Warn));
            Assert.Equal(SeatStateMachine.MsgAwaySuspect,
                         h.AlertsOf(AlertType.Away, AlertLevel.Warn)[0].Message);
            Assert.Equal(0, h.CountOf(AlertType.Away, AlertLevel.Alert));
        }

        [Fact(DisplayName = "얼굴 미검출 5.0초 → Away + Alert + \"학생이 좌석을 옮겼습니다\"")]
        public void NoFace_5sec_RaisesConfirmedAlert()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).NoFace(5.0));

            Assert.Equal(SeatState.Away, h.State);
            Assert.Equal(1, h.CountOf(AlertType.Away, AlertLevel.Alert));
            Assert.Equal("학생이 좌석을 옮겼습니다",
                         h.AlertsOf(AlertType.Away, AlertLevel.Alert)[0].Message);
            Assert.Equal(1, h.Stats.AwayCount);
        }

        [Fact(DisplayName = "이석 후 복귀 → Awake + Recovered")]
        public void Away_ThenReturn_RaisesRecovered()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).NoFace(5.0).Open(2.0));

            Assert.Equal(SeatState.Awake, h.State);
            Assert.Equal(1, h.CountOf(AlertType.Recovered, AlertLevel.Clear));
        }

        // ==================================================================
        // cooldown
        // ==================================================================

        [Fact(DisplayName = "cooldown — 같은 (Type, Level) 은 AlertCooldownSec 안에 재발행되지 않는다")]
        public void SameTypeAndLevel_SuppressedWithinCooldown()
        {
            Harness h = new Harness();
            // 의심 → 복귀 → 다시 의심. 두 번째 의심은 15초 cooldown 안이라 억제되어야 한다.
            h.Run(Seq.Start().Open(1.0).Closed(2.0).Open(0.5).Closed(2.0));

            Assert.Equal(1, h.CountOf(AlertType.Drowsy, AlertLevel.Warn));
            // 알림은 억제됐지만 상태 전이 자체는 두 번 일어났다.
            Assert.Equal(SeatState.DrowsySuspect, h.State);
        }

        [Fact(DisplayName = "cooldown — 확정(Alert)은 의심(Warn)과 별개 키로 카운트된다")]
        public void ConfirmAlert_NotSuppressedByRecentWarn()
        {
            Harness h = new Harness();
            // Warn(2.0초) 3초 뒤에 Alert(5.0초). cooldown 15초 안이지만 키가 달라 둘 다 나가야 한다.
            h.Run(Seq.Start().Open(1.0).Closed(5.0));

            Assert.Equal(1, h.CountOf(AlertType.Drowsy, AlertLevel.Warn));
            Assert.Equal(1, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
        }
    }
}
