using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using Etoos.DetectSample.Alerts;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Logging
{
    /// <summary>
    /// 확정(Alert) 알림이 발생한 순간의 화면을 <c>logs/snapshots/</c> 아래에 jpg 로 남긴다.
    ///
    /// <b>파일 위치가 Logging\ 이 아니라 Capture\ 인 이유</b>: 네임스페이스는 역할에 맞춰
    /// Logging 을 쓰지만, tests\EtoosDetectSample.Tests.csproj 가 <c>Logging\*.cs</c> 를 통째로
    /// net8.0(macOS 포함 교차 플랫폼)으로 링크해서 컴파일한다(CONTRACT.md 0.3 "외부 의존 0" 검증용).
    /// 이 클래스는 System.Drawing.Bitmap(WinForms 전용)에 의존하므로 Logging\ 에 두면
    /// 그 교차 플랫폼 테스트 빌드가 깨진다. 그래서 Bitmap 을 이미 쓰는 Capture\ 에 물리적으로 둔다.
    ///
    /// 설계
    ///  - <see cref="AlertJsonlLogger"/>/<see cref="FrameCsvLogger"/>와 같은 패턴: 생성자에서
    ///    (AppSettings, onError) 만 받고, 어떤 예외도 밖으로 던지지 않는다(CONTRACT.md 5절).
    ///  - Warn(의심) 은 저장하지 않는다 — 확정된 사건만 "증거"로 남긴다. 그렇지 않으면
    ///    파일이 과도하게 쌓인다.
    ///  - 파일명에 시각/유형/레벨/EventId 를 인코딩해서 별도 사이드카 없이도
    ///    뷰어가 목록을 바로 만들 수 있게 한다.
    ///  - 넘겨받은 Bitmap 은 <see cref="AnalysisWorker"/>가 프레임마다 재사용하는
    ///    풀 버퍼일 수 있으므로, 저장 전에 반드시 즉시 Clone 한다(호출자 프레임 콜백 안에서
    ///    동기 호출된다는 전제).
    /// </summary>
    public sealed class AlertSnapshotWriter
    {
        private readonly string _directory;
        private readonly Action<string> _onError;

        /// <summary>저장 성공 횟수 / 실패 횟수(디버그용).</summary>
        public int WrittenCount;
        public int FailedCount;

        public AlertSnapshotWriter(AppSettings settings, Action<string> onError)
        {
            AppSettings s = settings != null ? settings : new AppSettings();
            _directory = Path.Combine(LogPaths.ResolveDirectory(s.LogDirectory), "snapshots");
            _onError = onError;
        }

        public string Directory { get { return _directory; } }

        /// <summary>AlertLevel.Alert(확정) 인 경우에만 frame 을 jpg 로 저장한다.</summary>
        public void Write(AlertEvent e, Bitmap frame)
        {
            if (e == null || frame == null) return;
            if (e.Level != AlertLevel.Alert) return;
            if (frame.Width <= 0 || frame.Height <= 0) return;

            try
            {
                if (!LogPaths.EnsureDirectory(_directory))
                {
                    Fail("스냅샷 디렉토리를 만들 수 없습니다: " + _directory);
                    return;
                }

                string fileName = string.Format(CultureInfo.InvariantCulture,
                    "{0}_{1}_{2}_{3}.jpg",
                    e.OccurredAt.ToLocalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                    AlertJson.TypeName(e.Type), AlertJson.LevelName(e.Level), e.EventId);

                string path = Path.Combine(_directory, fileName);

                using (Bitmap clone = (Bitmap)frame.Clone())
                {
                    clone.Save(path, ImageFormat.Jpeg);
                }
                WrittenCount++;
            }
            catch (Exception ex)
            {
                Fail("스냅샷 저장 실패: " + ex.Message);
            }
        }

        private void Fail(string message)
        {
            FailedCount++;
            if (_onError == null) return;
            try { _onError(message); }
            catch (Exception) { /* 에러 콜백 실패는 무시 */ }
        }
    }
}
