using System;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>PERCLOS(눈 감김 비율) 보조 신호와 Blink 카운트.</summary>
    public class PerclosBlinkTests
    {
        [Fact(DisplayName = "PERCLOS 가 PerclosSuspectRatio 를 넘으면 보조 의심 신호를 낸다")]
        public void HighPerclos_RaisesSuspectSignal()
        {
            AppSettings s = Harness.NewSettings();
            s.PerclosWindowSec = 5.0;    // 테스트 시간을 줄인다(기본 60초)
            Harness h = new Harness(s);

            // 0.5초 감김 / 0.5초 뜸을 반복 → 비율 약 0.5 (> 0.35).
            // 연속 감김은 0.5초라 ClosedEyeSuspectSec(2.0) 에는 절대 닿지 않는다
            // → 여기서 나오는 Drowsy/Warn 은 PERCLOS 경로에서만 나올 수 있다.
            Seq seq = Seq.Start();
            for (int i = 0; i < 16; i++) seq.Closed(0.5).Open(0.5);
            h.Run(seq);

            Assert.True(h.Machine.Perclos > s.PerclosSuspectRatio,
                        "perclos=" + h.Machine.Perclos.ToString());
            Assert.False(h.EverEntered(SeatState.DrowsySuspect), "연속 감김으로는 의심에 닿지 않아야 한다");

            var perclosAlerts = h.AlertsWithMessage(SeatStateMachine.MsgPerclos);
            Assert.NotEmpty(perclosAlerts);
            Assert.Equal(AlertType.Drowsy, perclosAlerts[0].Type);
            Assert.Equal(AlertLevel.Warn, perclosAlerts[0].Level);
        }

        [Fact(DisplayName = "PERCLOS 는 window 를 채우기 전에는 알림을 내지 않는다")]
        public void Perclos_SilentBeforeWindowIsFull()
        {
            AppSettings s = Harness.NewSettings();
            s.PerclosWindowSec = 30.0;
            Harness h = new Harness(s);

            // 관측 시간 8초 < window 30초. 비율은 높지만 표본이 부족하다.
            Seq seq = Seq.Start();
            for (int i = 0; i < 8; i++) seq.Closed(0.5).Open(0.5);
            h.Run(seq);

            Assert.Empty(h.AlertsWithMessage(SeatStateMachine.MsgPerclos));
        }

        [Fact(DisplayName = "Unknown 구간은 PERCLOS 분모에서 제외된다")]
        public void Unknown_ExcludedFromPerclosDenominator()
        {
            AppSettings s = Harness.NewSettings();
            s.PerclosWindowSec = 60.0;   // 이 테스트 길이 안에서는 아무것도 밀려나지 않는다
            Harness h = new Harness(s);

            Seq seq = Seq.Start().Open(1.0).Closed(1.0).Open(1.0);
            int mark = seq.Count;
            seq.Unknown(UnknownReason.EyeOccluded, 4.0);
            h.Run(seq);

            double before = h.Snapshots[mark - 1].Perclos;
            double after = h.Snapshots[h.Snapshots.Count - 1].Perclos;

            Assert.True(before > 0.0, "감김 구간이 있었는데 perclos 가 0 이다");
            Assert.Equal(before, after, 12);   // Unknown 4초가 분모에 들어갔다면 값이 내려간다
        }

        [Fact(DisplayName = "window 밖 표본은 버려져 PERCLOS 버퍼가 무한히 늘지 않는다")]
        public void PerclosWindow_IsBounded()
        {
            AppSettings s = Harness.NewSettings();
            s.PerclosWindowSec = 5.0;
            Harness h = new Harness(s);

            // 300초(2400 프레임)를 흘린다. window 5초 x 8 FPS = 40 표본 근처에서 유지되어야 한다.
            Seq seq = Seq.Start();
            for (int i = 0; i < 300; i++) seq.Closed(0.5).Open(0.5);
            h.Run(seq);

            int count = h.PerclosSampleCount();
            Assert.True(count <= 48, "PERCLOS 표본이 " + count.ToString() + "개 남아 있다 (상한 48 기대)");
            Assert.True(count > 0, "전부 버려지면 PERCLOS 를 계산할 수 없다");
        }

        // ==================================================================
        // Blink
        // ==================================================================

        [Fact(DisplayName = "Blink — 60~500ms 범위의 Open→Closed→Open 만 센다")]
        public void Blink_CountedOnlyWithinRange()
        {
            Harness inRange = new Harness();
            inRange.Run(Seq.Start().Open(1.0).Closed(0.375).Open(1.0));   // 375ms
            Assert.Equal(1, inRange.Stats.BlinkCount);

            Harness tooLong = new Harness();
            tooLong.Run(Seq.Start().Open(1.0).Closed(0.625).Open(1.0));   // 625ms > 500ms
            Assert.Equal(0, tooLong.Stats.BlinkCount);
        }

        [Fact(DisplayName = "Blink — 졸음 수준의 긴 감김은 깜빡임으로 세지 않는다")]
        public void LongClosure_IsNotBlink()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(5.0).Open(1.0));

            Assert.Equal(0, h.Stats.BlinkCount);
            Assert.Equal(1, h.Stats.DrowsyCount);
        }

        [Fact(DisplayName = "Blink — 중간에 Unknown 이 끼면 깜빡임 시퀀스는 무효다")]
        public void UnknownInside_InvalidatesBlink()
        {
            Harness h = new Harness();
            h.Run(Seq.Start()
                     .Open(1.0)
                     .Closed(0.25)
                     .Unknown(UnknownReason.AsymmetricEye, 0.25)
                     .Open(1.0));

            Assert.Equal(0, h.Stats.BlinkCount);
        }
    }
}
