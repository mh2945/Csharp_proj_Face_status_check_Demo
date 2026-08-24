using System;
using Etus.DetectSample.Alerts;
using Etus.DetectSample.Analysis;
using Etus.DetectSample.Config;
using Etus.DetectSample.Tests.Support;
using Xunit;

namespace Etus.DetectSample.Tests
{
    /// <summary>
    /// Unknown 유예와 리셋. 오탐 방지의 핵심이라 졸음/이석 판정보다 먼저 깨진다.
    /// </summary>
    public class SeatStateMachineUnknownResetTests
    {
        // ==================================================================
        // Unknown 유예
        // ==================================================================

        [Fact(DisplayName = "Unknown 이 유예(1.5초) 이하면 직전 상태를 유지한다")]
        public void ShortUnknown_KeepsPreviousState()
        {
            Harness h = new Harness();
            // 8 FPS 에서 12 프레임 = 정확히 1.5초. UnknownGraceSec 와 같으므로 '초과'가 아니다.
            h.Run(Seq.Start().Open(3.0).Unknown(UnknownReason.PoseOutOfRange, 1.5));

            Assert.Equal(SeatState.Awake, h.State);
            Assert.Equal(1.5, h.Machine.UnknownSec, 9);
        }

        [Fact(DisplayName = "Unknown 이 유예 이하면 ClosedEyeSec 이 얼어붙는다(누적 금지)")]
        public void ShortUnknown_FreezesClosedEyeCounter()
        {
            Harness h = new Harness();
            Seq s = Seq.Start().Open(1.0).Closed(2.0);
            s.Unknown(UnknownReason.EyeOccluded, 1.5);
            h.Run(s);

            Assert.Equal(SeatState.DrowsySuspect, h.State);
            Assert.Equal(2.0, h.Machine.ClosedEyeSec, 9);   // 1.5초가 더해지면 3.5 가 된다
        }

        [Fact(DisplayName = "Unknown 이 유예를 초과하면 SeatState.Unknown 으로 전이한다")]
        public void LongUnknown_TransitionsToUnknown()
        {
            Harness h = new Harness();
            // 13 프레임 = 1.625초 > 1.5초
            h.Run(Seq.Start().Open(3.0).Unknown(UnknownReason.PoseOutOfRange, 1.625));

            Assert.Equal(SeatState.Unknown, h.State);
        }

        [Fact(DisplayName = "Unknown 구간은 눈 감김 시간에 누적되지 않는다 — 감김3초+Unknown3초+감김1초 = 4초")]
        public void UnknownGap_DoesNotAccumulateIntoClosedEyeSec()
        {
            // 이게 깨지면 손으로 눈을 비비기만 해도 졸음 Alert 이 뜬다.
            Harness h = new Harness();
            h.Run(Seq.Start()
                     .Open(1.0)
                     .Closed(3.0)
                     .Unknown(UnknownReason.EyeOccluded, 3.0)
                     .Closed(1.0));

            Assert.Equal(4.0, h.Machine.ClosedEyeSec, 9);        // 7.0 이 되면 안 된다
            Assert.Equal(0, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
            Assert.False(h.EverEntered(SeatState.Drowsy), "Unknown 3초가 감김에 더해져 확정되면 안 된다");
            Assert.Equal(SeatState.DrowsySuspect, h.State);
        }

        [Fact(DisplayName = "Unknown 구간은 UnknownSec 로 따로 집계된다")]
        public void UnknownGap_AccumulatesIntoUnknownSec()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(3.0).Unknown(UnknownReason.LowLandmarkConfidence, 4.0).Open(1.0));

            // 유예 1.5초를 넘긴 뒤부터 SeatState.Unknown 이므로 그 구간만 UnknownSec 에 적립된다.
            Assert.True(h.Stats.UnknownSec > 0.0, "UnknownSec 가 적립되지 않았다");
            Assert.True(h.Stats.UnknownSec < 4.0,
                        "유예 구간까지 UnknownSec 로 잡히면 안 된다: " + h.Stats.UnknownSec.ToString());
        }

        // ==================================================================
        // Face.id 리셋
        // ==================================================================

        [Fact(DisplayName = "Face.id 가 바뀌면 카운터를 리셋한다 (ResetOnTrackIdChange = true)")]
        public void TrackIdChange_ResetsCounters()
        {
            Harness h = new Harness();
            Assert.True(h.Settings.ResetOnTrackIdChange, "기본값이 true 여야 한다");

            h.Run(Seq.Start().TrackId(1).Open(1.0).Closed(3.0).TrackId(2).Closed(1.0));

            Assert.NotNull(h.Machine.LastResetReason);
            Assert.Contains("Face.id", h.Machine.LastResetReason);
            // 리셋 프레임 이후 7 프레임분(0.875초)만 남는다. 3.0 + 1.0 = 4.0 이 되면 안 된다.
            Assert.Equal(0.875, h.Machine.ClosedEyeSec, 9);
            Assert.Equal(SeatState.Awake, h.State);
        }

        [Fact(DisplayName = "Face.id 가 바뀌면 PersonChanged 알림이 1건 발생한다(알림 목록/사운드 재사용)")]
        public void TrackIdChange_EmitsPersonChangedAlert()
        {
            Harness h = new Harness();

            h.Run(Seq.Start().TrackId(1).Open(1.0).Closed(3.0).TrackId(2).Closed(1.0));

            Assert.Equal(1, h.CountOf(AlertType.PersonChanged, AlertLevel.Warn));
            Assert.Equal(SeatStateMachine.MsgPersonChanged,
                         h.AlertsOf(AlertType.PersonChanged, AlertLevel.Warn)[0].Message);
        }

        [Fact(DisplayName = "ResetOnTrackIdChange = false 면 id 가 바뀌어도 리셋하지 않는다")]
        public void TrackIdChange_Ignored_WhenDisabled()
        {
            AppSettings s = Harness.NewSettings();
            s.ResetOnTrackIdChange = false;
            Harness h = new Harness(s);

            h.Run(Seq.Start().TrackId(1).Open(1.0).Closed(3.0).TrackId(2).Closed(1.0));

            Assert.Null(h.Machine.LastResetReason);
            Assert.Equal(4.0, h.Machine.ClosedEyeSec, 9);
            Assert.Equal(SeatState.DrowsySuspect, h.State);
        }

        [Fact(DisplayName = "FaceTrackId == -1(미검출) 프레임은 id 비교 대상에서 제외된다")]
        public void MinusOneTrackId_DoesNotTriggerReset()
        {
            Harness h = new Harness();
            // id 1 → 미검출(-1) → 다시 id 1. 리셋이 일어나면 안 된다.
            h.Run(Seq.Start().TrackId(1).Open(1.0).NoFace(1.0).TrackId(1).Open(1.0));

            Assert.Null(h.Machine.LastResetReason);
            Assert.Empty(h.ResetReasons);
        }

        [Fact(DisplayName = "미검출을 사이에 두고 id 가 실제로 바뀌면 리셋한다")]
        public void TrackIdChange_AcrossNoFace_TriggersReset()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().TrackId(1).Open(1.0).NoFace(1.0).TrackId(2).Open(1.0));

            Assert.NotNull(h.Machine.LastResetReason);
            Assert.Contains("Face.id 변경 1 -> 2", h.Machine.LastResetReason);
        }

        // ==================================================================
        // 프레임 유실
        // ==================================================================

        [Fact(DisplayName = "프레임 유실(Δt > FrameGapResetSec) 시 리셋하고, 갭 시간은 누적 통계에 더하지 않는다")]
        public void FrameGap_ResetsAndDoesNotAccumulateGapTime()
        {
            Harness h = new Harness();
            // 3초 관측 → 2초 유실 → 3초 관측. 유실 구간의 2.125초는 관측하지 않은 시간이다.
            h.Run(Seq.Start().Open(3.0).Gap(2.0).Open(3.0));

            Assert.NotNull(h.Machine.LastResetReason);
            Assert.Contains("프레임 유실", h.Machine.LastResetReason);

            // 각 구간 24 프레임 중 첫 프레임은 dt=0 → 23 x 0.125 = 2.875 씩 적립된다.
            Assert.Equal(5.75, h.Stats.StudySec, 9);
            Assert.Equal(5.75, h.Stats.SeatedSec, 9);
            // 갭이 더해졌다면 7.875 가 된다.
            Assert.True(h.Stats.SeatedSec < 6.0, "갭 시간이 누적되었다: " + h.Stats.SeatedSec.ToString());
        }

        [Fact(DisplayName = "프레임 유실 리셋은 진행 중이던 졸음 카운터도 버린다")]
        public void FrameGap_DropsInFlightClosedEyeCounter()
        {
            Harness h = new Harness();
            h.Run(Seq.Start().Open(1.0).Closed(3.0).Gap(2.0).Closed(1.0));

            // 유실 전 3.0초는 버려지고 유실 후 0.875초만 남는다 → 확정되지 않는다.
            Assert.Equal(0.875, h.Machine.ClosedEyeSec, 9);
            Assert.Equal(0, h.CountOf(AlertType.Drowsy, AlertLevel.Alert));
        }
    }
}
