using System;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Analysis
{
    /// <summary>
    /// 세션 누적 집계. 프레임마다 <see cref="Accumulate"/> 로 시간 조각을 더한다.
    ///
    /// [알려진 한계 — CONTRACT.md 5절]
    /// <b>StudySec 는 "실제 공부한 시간"이 아니라 "깨어 있는(Awake) 시간"이다.</b>
    /// 안면 인식만으로는 눈을 뜨고 딴짓하는 것과 공부하는 것을 구분할 수 없다.
    /// UI/보고서에 노출할 때 반드시 "깨어 있는 시간"으로 표기할 것.
    ///
    /// SeatState.Idle 구간은 어느 항목에도 적립하지 않는다(아직 판정 전이므로).
    /// 스레드 안전하지 않다. 워커 스레드 1개가 소유한다(CONTRACT.md 3절).
    /// </summary>
    public sealed class SessionStats
    {
        public double SeatedSec, StudySec, DrowsySec, AwaySec, UnknownSec;
        public int DrowsyCount, AwayCount;

        /// <summary>
        /// 이 값을 넘는 deltaSec 은 "실제로 관측하지 않은 시간"으로 보고 버린다.
        /// 기본값은 AppSettings.FrameGapResetSec (매직 넘버 금지).
        /// </summary>
        public double MaxDeltaSec;

        public SessionStats() : this(null)
        {
        }

        public SessionStats(AppSettings settings)
        {
            AppSettings s = settings != null ? settings : new AppSettings();
            MaxDeltaSec = s.FrameGapResetSec;
        }

        /// <summary>
        /// [prev, now] 구간의 시간 조각을 적립한다.
        /// deltaSec 이 음수/NaN/Infinity 이거나 <see cref="MaxDeltaSec"/> 를 넘으면 무시한다.
        /// </summary>
        public void Accumulate(SeatState state, Presence presence, double deltaSec)
        {
            if (double.IsNaN(deltaSec) || double.IsInfinity(deltaSec)) return;
            if (deltaSec <= 0.0) return;
            if (deltaSec > MaxDeltaSec) return;

            if (presence == Presence.Present) SeatedSec += deltaSec;

            switch (state)
            {
                case SeatState.Awake:
                    StudySec += deltaSec;
                    break;
                case SeatState.DrowsySuspect:
                case SeatState.Drowsy:
                    DrowsySec += deltaSec;
                    break;
                case SeatState.AwaySuspect:
                case SeatState.Away:
                    AwaySec += deltaSec;
                    break;
                case SeatState.Unknown:
                    UnknownSec += deltaSec;
                    break;
                default:
                    // SeatState.Idle — 판정 시작 전. 어디에도 적립하지 않는다.
                    break;
            }
        }

        public void Reset()
        {
            SeatedSec = 0.0;
            StudySec = 0.0;
            DrowsySec = 0.0;
            AwaySec = 0.0;
            UnknownSec = 0.0;
            DrowsyCount = 0;
            AwayCount = 0;
        }

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "seated={0:F1}s awake={1:F1}s drowsy={2:F1}s away={3:F1}s unknown={4:F1}s / drowsy={5} away={6}",
                SeatedSec, StudySec, DrowsySec, AwaySec, UnknownSec,
                DrowsyCount, AwayCount);
        }
    }
}
