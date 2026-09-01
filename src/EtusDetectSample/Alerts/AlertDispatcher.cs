using System;
using System.Collections.Generic;

namespace Etoos.DetectSample.Alerts
{
    /// <summary>
    /// 발생한 <see cref="AlertEvent"/> 를 등록된 sink 들로 fan-out 한다.
    ///
    /// [의도적으로 하지 않는 것] <b>중복 억제(cooldown)를 여기서 하지 않는다.</b>
    /// cooldown 은 이미 SeatStateMachine 이 (Type, Level) 단위로 처리한다.
    /// 두 곳에서 억제하면 "알림이 왜 안 왔는지"를 추적할 수 없게 된다.
    /// 여기 들어온 이벤트는 전부 나간다.
    ///
    /// sink 하나가 예외를 던져도 나머지 sink 는 계속 동작한다(각각 try/catch).
    /// 호출은 워커 스레드에서 일어난다. UI sink 는 <b>내부에서 BeginInvoke 로 마샬링</b>해야 한다
    /// (CONTRACT.md 3절 — 워커 → UI 는 BeginInvoke 만).
    /// </summary>
    public sealed class AlertDispatcher
    {
        private sealed class Sink
        {
            public string Name;
            public Action<AlertEvent> Handler;
        }

        private readonly List<Sink> _sinks = new List<Sink>();
        private readonly Action<string> _onError;

        /// <summary>이 dispatcher 를 통과한 총 이벤트 수(디버그용).</summary>
        public int DispatchedCount;

        public AlertDispatcher() : this(null)
        {
        }

        /// <param name="onError">sink 실패 통보. null 이면 Console 로 떨어뜨린다.</param>
        public AlertDispatcher(Action<string> onError)
        {
            _onError = onError;
        }

        public int SinkCount { get { return _sinks.Count; } }

        /// <summary>임의 sink 등록. name 은 실패 로그 식별용.</summary>
        public void AddSink(string name, Action<AlertEvent> handler)
        {
            if (handler == null) return;
            Sink s = new Sink();
            s.Name = string.IsNullOrEmpty(name) ? "sink" + _sinks.Count.ToString() : name;
            s.Handler = handler;
            _sinks.Add(s);
        }

        /// <summary>UI 콜백. 콜백 구현이 BeginInvoke 로 UI 스레드에 넘겨야 한다.</summary>
        public void AddUiSink(Action<AlertEvent> uiCallback)
        {
            AddSink("ui", uiCallback);
        }

        /// <summary>
        /// 콘솔 1줄 출력. JSON 대신 사람이 읽는 요약을 낸다
        /// (JSON 전문은 JSONL sink 가 남긴다).
        /// </summary>
        public void AddConsoleSink()
        {
            AddSink("console", WriteConsole);
        }

        public void Dispatch(AlertEvent e)
        {
            if (e == null) return;
            DispatchedCount++;

            for (int i = 0; i < _sinks.Count; i++)
            {
                Sink s = _sinks[i];
                try
                {
                    s.Handler(e);
                }
                catch (Exception ex)
                {
                    Report("alert sink '" + s.Name + "' 실패: " + ex.Message);
                }
            }
        }

        /// <summary>SeatStateMachine.Push() 가 돌려준 목록을 그대로 넣는다. null/빈 목록 허용.</summary>
        public void Dispatch(IList<AlertEvent> events)
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++) Dispatch(events[i]);
        }

        private static void WriteConsole(AlertEvent e)
        {
            Console.WriteLine("[ALERT] {0} {1} {2} ({3:F1}s) seat={4}",
                AlertJson.LevelName(e.Level), AlertJson.TypeName(e.Type),
                e.Message, e.DurationSec, e.SeatId);
        }

        private void Report(string message)
        {
            if (_onError != null)
            {
                try { _onError(message); return; }
                catch (Exception) { /* 에러 콜백까지 실패하면 아래 콘솔로 */ }
            }
            try { Console.Error.WriteLine(message); }
            catch (Exception) { /* 마지막 수단도 실패하면 포기 — 앱은 죽이지 않는다 */ }
        }
    }
}
