using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;

namespace Etus.DetectSample.Config
{
    /// <summary>
    /// App.config 의 &lt;appSettings&gt; 를 읽어 <see cref="AppSettings"/> 를 만든다.
    ///
    /// 이 파일은 <b>Config 폴더에서 유일하게 System.Configuration 에 의존</b>한다.
    /// (AppSettings.cs 자체는 외부 의존 0 을 유지해야 하므로 로더를 분리했다)
    ///
    /// 주의 — net8.0 단위테스트 프로젝트가 Config\*.cs 를 소스 링크한다면
    ///        <b>이 파일은 Compile 대상에서 제외</b>해야 한다. net8.0 에는
    ///        System.Configuration.ConfigurationManager 가 기본 포함되지 않는다.
    ///
    /// 설계 원칙:
    ///  - 절대 예외를 던지지 않는다. 잘못된 config 때문에 데모가 시작조차 못 하면 안 된다.
    ///  - 키가 없거나 파싱 실패하면 AppSettings.cs 의 코드 기본값을 그대로 둔다.
    ///  - 무슨 일이 있었는지는 warnings 목록에만 남긴다. 호출자가 UI/로그에 표시한다.
    ///  - 파싱은 전부 InvariantCulture. 한국어 로케일에서도 "2.4" 가 2.4 로 읽혀야 한다.
    /// </summary>
    public static class AppSettingsLoader
    {
        // ConfigurationManager.AppSettings 는 config 파일이 깨져 있으면 접근할 때마다
        // ConfigurationErrorsException 을 던진다. 한 번만 읽어서 캐시한다.
        static NameValueCollection _raw;
        static bool _rawLoaded;
        static string _rawLoadError;
        static readonly object _rawLock = new object();

        /// <summary>App.config 를 읽어 설정을 만든다. 경고는 버린다.</summary>
        public static AppSettings Load()
        {
            List<string> ignored;
            return Load(out ignored);
        }

        /// <summary>App.config 를 읽어 설정을 만든다. 무시된 키/파싱 실패는 warnings 로 돌려준다.</summary>
        public static AppSettings Load(out List<string> warnings)
        {
            warnings = new List<string>();

            AppSettings s = new AppSettings();

            NameValueCollection raw = GetRaw();
            if (raw == null)
            {
                warnings.Add("App.config 를 읽지 못했습니다. 모든 값에 코드 기본값을 사용합니다." +
                             (string.IsNullOrEmpty(_rawLoadError) ? "" : " (" + _rawLoadError + ")"));
                s.Clamp();
                return s;
            }

            // --- 좌석 ---
            s.SeatId = ReadString(raw, "SeatId", s.SeatId, warnings);

            // --- 눈 개폐 ---
            s.EyelidClosedThreshold = ReadDouble(raw, "EyelidClosedThreshold", s.EyelidClosedThreshold, warnings);

            // --- 게이트 ---
            s.LandmarkConfMin = ReadDouble(raw, "LandmarkConfMin", s.LandmarkConfMin, warnings);
            s.OcclusionMax = ReadDouble(raw, "OcclusionMax", s.OcclusionMax, warnings);
            s.FineOcclusionMax = ReadDouble(raw, "FineOcclusionMax", s.FineOcclusionMax, warnings);
            s.UseFineOcclusionGate = ReadBool(raw, "UseFineOcclusionGate", s.UseFineOcclusionGate, warnings);
            s.PoseYawMaxDeg = ReadDouble(raw, "PoseYawMaxDeg", s.PoseYawMaxDeg, warnings);
            s.PosePitchMaxDeg = ReadDouble(raw, "PosePitchMaxDeg", s.PosePitchMaxDeg, warnings);

            // --- 시간 임계 ---
            s.ClosedEyeSuspectSec = ReadDouble(raw, "ClosedEyeSuspectSec", s.ClosedEyeSuspectSec, warnings);
            s.ClosedEyeConfirmSec = ReadDouble(raw, "ClosedEyeConfirmSec", s.ClosedEyeConfirmSec, warnings);
            s.NoFaceSuspectSec = ReadDouble(raw, "NoFaceSuspectSec", s.NoFaceSuspectSec, warnings);
            s.NoFaceConfirmSec = ReadDouble(raw, "NoFaceConfirmSec", s.NoFaceConfirmSec, warnings);
            s.UnknownGraceSec = ReadDouble(raw, "UnknownGraceSec", s.UnknownGraceSec, warnings);
            s.AlertCooldownSec = ReadDouble(raw, "AlertCooldownSec", s.AlertCooldownSec, warnings);

            // --- PERCLOS ---
            s.PerclosWindowSec = ReadDouble(raw, "PerclosWindowSec", s.PerclosWindowSec, warnings);
            s.PerclosSuspectRatio = ReadDouble(raw, "PerclosSuspectRatio", s.PerclosSuspectRatio, warnings);

            // --- Blink ---
            s.BlinkMinMs = ReadDouble(raw, "BlinkMinMs", s.BlinkMinMs, warnings);
            s.BlinkMaxMs = ReadDouble(raw, "BlinkMaxMs", s.BlinkMaxMs, warnings);

            // --- 엎드림 휴리스틱 ---
            s.EnableSlumpHeuristic = ReadBool(raw, "EnableSlumpHeuristic", s.EnableSlumpHeuristic, warnings);

            // --- 프레임 유실 / tracking ---
            s.FrameGapResetSec = ReadDouble(raw, "FrameGapResetSec", s.FrameGapResetSec, warnings);
            s.ResetOnTrackIdChange = ReadBool(raw, "ResetOnTrackIdChange", s.ResetOnTrackIdChange, warnings);

            // --- 카메라 ---
            s.CameraIndex = ReadInt(raw, "CameraIndex", s.CameraIndex, warnings);
            s.CaptureWidth = ReadInt(raw, "CaptureWidth", s.CaptureWidth, warnings);
            s.CaptureHeight = ReadInt(raw, "CaptureHeight", s.CaptureHeight, warnings);
            s.CaptureCodec = ReadString(raw, "CaptureCodec", s.CaptureCodec, warnings);
            s.FlipHorizontal = ReadBool(raw, "FlipHorizontal", s.FlipHorizontal, warnings);

            // --- 분석 입력 이미지 ---
            s.AnalyzeWidth = ReadInt(raw, "AnalyzeWidth", s.AnalyzeWidth, warnings);
            s.AnalyzeHeight = ReadInt(raw, "AnalyzeHeight", s.AnalyzeHeight, warnings);
            s.SrcRotateDegrees = ReadInt(raw, "SrcRotateDegrees", s.SrcRotateDegrees, warnings);
            s.EnableAspectCrop = ReadBool(raw, "EnableAspectCrop", s.EnableAspectCrop, warnings);
            s.EnableAnalyzeResize = ReadBool(raw, "EnableAnalyzeResize", s.EnableAnalyzeResize, warnings);

            // --- 성능 ---
            s.AttrIntervalFrames = ReadInt(raw, "AttrIntervalFrames", s.AttrIntervalFrames, warnings);
            s.TargetAnalyzeFps = ReadDouble(raw, "TargetAnalyzeFps", s.TargetAnalyzeFps, warnings);

            // --- 로깅 ---
            s.EnableFrameCsv = ReadBool(raw, "EnableFrameCsv", s.EnableFrameCsv, warnings);
            s.LogDirectory = ReadString(raw, "LogDirectory", s.LogDirectory, warnings);

            // 범위를 벗어난 값이 Clamp 에 걸려 조용히 바뀌면 현장에서 "왜 안 먹지" 가 된다.
            // 바뀐 사실만이라도 남긴다.
            string before = s.ToString();
            s.Clamp();
            string after = s.ToString();
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                warnings.Add("일부 값이 허용 범위를 벗어나 보정되었습니다. before=[" + before + "] after=[" + after + "]");
            }

            return s;
        }

        //
        // AppSettings 에 없는 확장 키를 읽기 위한 공개 헬퍼.
        // (AppSettings.cs 는 읽기 전용 계약 파일이라 필드를 추가할 수 없다)
        //   - "CaptureApi"        : CameraCapture 백엔드 (ANY / MSMF / DSHOW / FFMPEG)
        //   - "EnableLandmark106" : 106점 landmark 채우기 on/off
        //

        /// <summary>확장 키를 문자열로 읽는다. 없으면 fallback.</summary>
        public static string GetString(string key, string fallback)
        {
            NameValueCollection raw = GetRaw();
            if (raw == null) return fallback;

            string v = raw[key];
            if (v == null) return fallback;

            v = v.Trim();
            return v.Length == 0 ? fallback : v;
        }

        /// <summary>확장 키를 bool 로 읽는다. 파싱 실패 시 fallback.</summary>
        public static bool GetBool(string key, bool fallback)
        {
            string v = GetString(key, null);
            if (v == null) return fallback;

            bool parsed;
            return TryParseBool(v, out parsed) ? parsed : fallback;
        }

        /// <summary>확장 키를 int 로 읽는다. 파싱 실패 시 fallback.</summary>
        public static int GetInt(string key, int fallback)
        {
            string v = GetString(key, null);
            if (v == null) return fallback;

            int parsed;
            return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        /// <summary>확장 키를 double 로 읽는다. 파싱 실패 시 fallback.</summary>
        public static double GetDouble(string key, double fallback)
        {
            string v = GetString(key, null);
            if (v == null) return fallback;

            double parsed;
            return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed : fallback;
        }

        //
        // internals
        //

        static NameValueCollection GetRaw()
        {
            if (_rawLoaded) return _raw;

            lock (_rawLock)
            {
                if (_rawLoaded) return _raw;

                try
                {
                    _raw = ConfigurationManager.AppSettings;
                }
                catch (Exception ex)
                {
                    // ConfigurationErrorsException 등. config 파일이 깨졌어도 앱은 떠야 한다.
                    _raw = null;
                    _rawLoadError = ex.Message;
                }

                _rawLoaded = true;
                return _raw;
            }
        }

        static string ReadRaw(NameValueCollection raw, string key)
        {
            string v = raw[key];
            if (v == null) return null;

            v = v.Trim();
            return v.Length == 0 ? null : v;
        }

        static string ReadString(NameValueCollection raw, string key, string fallback, List<string> warnings)
        {
            string v = ReadRaw(raw, key);
            if (v == null)
            {
                warnings.Add("키 없음: " + key + " → 기본값 \"" + fallback + "\" 사용");
                return fallback;
            }
            return v;
        }

        static double ReadDouble(NameValueCollection raw, string key, double fallback, List<string> warnings)
        {
            string v = ReadRaw(raw, key);
            if (v == null)
            {
                warnings.Add("키 없음: " + key + " → 기본값 " +
                             fallback.ToString(CultureInfo.InvariantCulture) + " 사용");
                return fallback;
            }

            double parsed;
            if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                warnings.Add("파싱 실패: " + key + "=\"" + v + "\" (소수점은 마침표) → 기본값 " +
                             fallback.ToString(CultureInfo.InvariantCulture) + " 사용");
                return fallback;
            }

            if (double.IsNaN(parsed) || double.IsInfinity(parsed))
            {
                warnings.Add("허용되지 않는 값: " + key + "=\"" + v + "\" → 기본값 " +
                             fallback.ToString(CultureInfo.InvariantCulture) + " 사용");
                return fallback;
            }

            return parsed;
        }

        static int ReadInt(NameValueCollection raw, string key, int fallback, List<string> warnings)
        {
            string v = ReadRaw(raw, key);
            if (v == null)
            {
                warnings.Add("키 없음: " + key + " → 기본값 " +
                             fallback.ToString(CultureInfo.InvariantCulture) + " 사용");
                return fallback;
            }

            int parsed;
            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                warnings.Add("파싱 실패: " + key + "=\"" + v + "\" → 기본값 " +
                             fallback.ToString(CultureInfo.InvariantCulture) + " 사용");
                return fallback;
            }

            return parsed;
        }

        static bool ReadBool(NameValueCollection raw, string key, bool fallback, List<string> warnings)
        {
            string v = ReadRaw(raw, key);
            if (v == null)
            {
                warnings.Add("키 없음: " + key + " → 기본값 " + (fallback ? "true" : "false") + " 사용");
                return fallback;
            }

            bool parsed;
            if (!TryParseBool(v, out parsed))
            {
                warnings.Add("파싱 실패: " + key + "=\"" + v + "\" (true/false) → 기본값 " +
                             (fallback ? "true" : "false") + " 사용");
                return fallback;
            }

            return parsed;
        }

        /// <summary>true/false 외에 1/0, yes/no, on/off 도 받아준다. 현장에서 손으로 고치는 파일이라.</summary>
        static bool TryParseBool(string v, out bool result)
        {
            result = false;
            if (string.IsNullOrEmpty(v)) return false;

            string t = v.Trim();

            if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "1", StringComparison.Ordinal) ||
                string.Equals(t, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "on", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "0", StringComparison.Ordinal) ||
                string.Equals(t, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "n", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(t, "off", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return false;
        }
    }
}
