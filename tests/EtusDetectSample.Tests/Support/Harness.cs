using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Etoos.DetectSample.Alerts;
using Etoos.DetectSample.Analysis;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Tests.Support
{
    /// <summary>
    /// EyeStateGate → SeatStateMachine → SessionStats 를 실제 배선 그대로 묶어 돌린다.
    ///
    /// 게이트를 흉내내지 않고 <b>진짜 EyeStateGate 를 통과시키는</b> 이유:
    /// 게이트와 상태머신 사이의 계약(특히 Presence 유지, Unknown 사유)이 어긋나면
    /// 각각을 따로 테스트했을 때는 통과하고 통합에서만 터진다.
    /// </summary>
    public sealed class Harness
    {
        public readonly AppSettings Settings;
        public readonly EyeStateGate Gate;
        public readonly SeatStateMachine Machine;
        public readonly SessionStats Stats;

        /// <summary>발생 순서대로 쌓인 알림 전부.</summary>
        public readonly List<AlertEvent> Alerts = new List<AlertEvent>();

        /// <summary>프레임마다의 스냅샷. 인덱스는 입력 프레임 인덱스와 1:1 이다.</summary>
        public readonly List<SeatSnapshot> Snapshots = new List<SeatSnapshot>();

        /// <summary>리셋 사유 기록(ResetLogger 콜백).</summary>
        public readonly List<string> ResetReasons = new List<string>();

        public Harness() : this(NewSettings())
        {
        }

        public Harness(AppSettings settings)
        {
            Settings = settings;
            Gate = new EyeStateGate(settings);
            Machine = new SeatStateMachine(settings);
            Stats = new SessionStats(settings);
            Machine.ResetLogger = ResetReasons.Add;
        }

        /// <summary>App.config 를 읽지 않은 순수 기본값. 테스트에서 개별 키만 덮어쓴다.</summary>
        public static AppSettings NewSettings()
        {
            return new AppSettings();
        }

        public SeatSnapshot Last
        {
            get { return Snapshots.Count == 0 ? null : Snapshots[Snapshots.Count - 1]; }
        }

        public SeatState State { get { return Machine.State; } }

        public void Run(IEnumerable<FrameObservation> frames)
        {
            foreach (FrameObservation o in frames)
            {
                EyeDecision eye = Gate.Evaluate(o);
                SeatSnapshot snap;
                IList<AlertEvent> emitted = Machine.Push(o, eye, Stats, out snap);
                Snapshots.Add(snap);
                for (int i = 0; i < emitted.Count; i++) Alerts.Add(emitted[i]);
            }
        }

        public void Run(Seq seq)
        {
            Run(seq.Build());
        }

        // ------------------------------------------------------------------
        // 조회 헬퍼
        // ------------------------------------------------------------------

        public List<AlertEvent> AlertsOf(AlertType type, AlertLevel level)
        {
            List<AlertEvent> found = new List<AlertEvent>();
            for (int i = 0; i < Alerts.Count; i++)
                if (Alerts[i].Type == type && Alerts[i].Level == level) found.Add(Alerts[i]);
            return found;
        }

        public int CountOf(AlertType type, AlertLevel level)
        {
            return AlertsOf(type, level).Count;
        }

        public List<AlertEvent> AlertsWithMessage(string message)
        {
            List<AlertEvent> found = new List<AlertEvent>();
            for (int i = 0; i < Alerts.Count; i++)
                if (Alerts[i].Message == message) found.Add(Alerts[i]);
            return found;
        }

        /// <summary>해당 상태가 한 번이라도 나타났는지.</summary>
        public bool EverEntered(SeatState state)
        {
            for (int i = 0; i < Snapshots.Count; i++)
                if (Snapshots[i].State == state) return true;
            return false;
        }

        /// <summary>
        /// PERCLOS sliding window 에 남아 있는 표본 수.
        /// private 필드라 리플렉션으로 본다 — 메모리 상한은 공개 API 로는 확인할 수 없고,
        /// 이것이 무한히 늘면 장시간 시연에서 메모리가 계속 증가한다.
        /// </summary>
        public int PerclosSampleCount()
        {
            FieldInfo f = typeof(SeatStateMachine).GetField(
                "_perclosWindow", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) throw new InvalidOperationException(
                "SeatStateMachine._perclosWindow 필드를 찾지 못했습니다. 필드명이 바뀌었으면 테스트를 고칠 것.");
            ICollection q = (ICollection)f.GetValue(Machine);
            return q.Count;
        }
    }
}
