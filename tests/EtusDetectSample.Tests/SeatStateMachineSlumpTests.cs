using System;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// 엎드림(slump) 휴리스틱.
    /// 안면 기반이라 "엎드려 잠"과 "자리를 뜸"은 둘 다 '얼굴이 안 보임'으로 들어온다.
    /// 직전에 눈을 감고 있었으면 이석 대신 졸음을 유지한다는 규칙 하나로 완화한다.
    /// </summary>
    public class SeatStateMachineSlumpTests
    {
        [Fact(DisplayName = "눈 감김 2초 후 얼굴 소실 → Away 가 아니라 Drowsy 유지 + SlumpSuspected")]
        public void ClosedThenFaceLost_KeepsDrowsy()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(2.0).NoFace(3.0));

            Assert.Equal(SeatState.Drowsy, h.State);
            Assert.True(h.Machine.SlumpSuspected, "SlumpSuspected 가 서지 않았다");
            Assert.False(h.EverEntered(SeatState.Away), "이석으로 오분류되면 안 된다");
            Assert.False(h.EverEntered(SeatState.AwaySuspect), "이석 의심으로도 가면 안 된다");
            Assert.Equal(0, h.CountOf(AlertType.Away, AlertLevel.Alert));
            Assert.Equal(0, h.Stats.AwayCount);
        }

        [Fact(DisplayName = "엎드림 상한(NoFaceConfirmSec x 1 = 5초)을 넘기면 결국 Away 로 내려간다")]
        public void Slump_ExceedingFallbackLimit_BecomesAway()
        {
            Harness h = new Harness();
            Assert.Equal(5.0, h.Machine.SlumpAwayFallbackSec, 9);

            h.Run(Seq.Start().Open(1.0).Closed(2.0).NoFace(20.0));

            Assert.Equal(SeatState.Away, h.State);
            Assert.False(h.Machine.SlumpSuspected, "상한 초과 후에는 엎드림 가정을 버려야 한다");
            Assert.Equal(1, h.Stats.AwayCount);
            Assert.Equal(1, h.CountOf(AlertType.Away, AlertLevel.Alert));
        }

        [Fact(DisplayName = "EnableSlumpHeuristic = false 면 평소대로 Away 로 간다")]
        public void SlumpDisabled_GoesToAway()
        {
            AppSettings s = Harness.NewSettings();
            s.EnableSlumpHeuristic = false;
            Harness h = new Harness(s);

            h.Run(Seq.Start().Open(1.0).Closed(2.0).NoFace(5.0));

            Assert.Equal(SeatState.Away, h.State);
            Assert.False(h.Machine.SlumpSuspected);
            Assert.Equal(1, h.CountOf(AlertType.Away, AlertLevel.Alert));
            Assert.Equal("학생이 좌석을 옮겼습니다",
                         h.AlertsOf(AlertType.Away, AlertLevel.Alert)[0].Message);
        }

        [Fact(DisplayName = "눈을 뜬 채로 얼굴이 사라지면 엎드림이 아니라 이석이다")]
        public void OpenThenFaceLost_IsAwayNotSlump()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(3.0).NoFace(5.0));

            Assert.Equal(SeatState.Away, h.State);
            Assert.False(h.Machine.SlumpSuspected);
        }
    }
}
