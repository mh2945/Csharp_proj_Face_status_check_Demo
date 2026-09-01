using System;
using System.Diagnostics;
using System.IO;
using Alchera.FaceSDK;
using Etoos.DetectSample.Analysis;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample.Sdk
{
    /// <summary>FaceSDK 초기화 결과. 실패 사유를 사용자에게 보여줄 수 있는 한국어 문구로 담는다.</summary>
    public sealed class InitResult
    {
        public bool Ok;

        /// <summary>메시지 박스 제목에 쓸 한 줄 요약.</summary>
        public string ErrorTitle;

        /// <summary>무엇을 해야 하는지까지 적은 상세 문구. 여러 줄일 수 있다.</summary>
        public string ErrorDetail;

        /// <summary>Initialize 에 걸린 시간(ms). 모델 로딩이 대부분이다.</summary>
        public long ElapsedMs;

        /// <summary>실제로 시도한 절대 모델 경로. 실패했을 때 이걸 보여줘야 원인 파악이 된다.</summary>
        public string ModelPath;

        public override string ToString()
        {
            if (Ok) return "OK (" + ElapsedMs + "ms, " + ModelPath + ")";
            return ErrorTitle + " : " + ErrorDetail;
        }
    }

    /// <summary>
    /// FaceSDK C# wrapper 를 감싸 한 프레임을 <see cref="FrameObservation"/> 으로 바꾸는 계층.
    /// <b>판정은 하지 않는다.</b> 원시 수치를 그대로 옮기고, 못 잰 값은 NaN 으로 남긴다.
    ///
    /// 스레딩: 이 클래스는 스레드 안전하지 않다. 워커 스레드 1개가 독점 소유해야 한다.
    ///         (SDK 의 continuous tracking 캐시가 프레임 순서에 의존한다 — CONTRACT.md 3장)
    ///
    /// 실패 정책:
    ///  - SDK 호출 결과는 <c>IsOk()</c> 가 true 일 때만 값을 읽는다.
    ///    wrapper 의 NewRst 는 에러 시 값 필드를 0 으로 남기므로, 0 을 그대로 쓰면
    ///    "눈꺼풀 거리 0 = 눈 감음" 으로 오독하게 된다. 못 잰 값은 반드시 float.NaN.
    ///  - 개별 속성 호출이 실패해도 프레임 전체를 버리지 않는다. 해당 항목만 NaN 이 된다.
    ///  - <see cref="FrameObservation.SdkError"/> 는 DTO 주석대로 <b>DetectFace 실패에만</b> 채운다.
    ///    속성 함수 실패는 <see cref="LastAttributeError"/> 로만 노출한다
    ///    (그렇지 않으면 CheckMask 한 번 실패했다고 판정 게이트가 프레임 전체를 Unknown 으로 버린다).
    /// </summary>
    public sealed class FaceEngine : IDisposable
    {
        readonly AppSettings _settings;

        FaceSDK _sdk;
        string _modelPath = "";
        long _initElapsedMs;
        bool _initialized;
        InitResult _initResult;
        bool _disposed;

        // --- DetectFineOcclusion 런타임 가용성 ---
        // 모델 패키지에 fine-occlusion 모델이 없을 수 있다. 한 번 실패하면 영구 비활성.
        bool _fineOcclusionAvailable = true;
        string _fineOcclusionDisabledReason;

        // --- AttrIntervalFrames 주기 호출 항목의 캐시 ---
        // 호출하지 않은 프레임은 직전 값을 재사용한다. 캐시가 없으면 NaN.
        bool _hasMaskCache;
        float _cachedMaskConf = float.NaN;
        bool _cachedIsMasked;

        bool _hasFineCache;
        float _cachedFineOcc = float.NaN;

        // tracking id 가 바뀌면 = 다른 사람이거나 추적이 끊긴 것. 캐시된 속성값은 더 이상 그 얼굴 것이 아니다.
        int _lastTrackId = int.MinValue;

        /// <summary>
        /// 106점 landmark 를 채울지. 오버레이 전용이며 판정에는 쓰지 않는다.
        /// <b>비용</b>: wrapper 의 <c>LandMark.ToArray()</c> 는 프레임당 리플렉션
        /// <c>FieldInfo.GetValue</c> 106회 + struct 박싱 106회를 한다.
        /// 오버레이가 필요 없거나 FPS 가 모자라면 끌 것. 런타임 중 토글해도 안전하다.
        /// </summary>
        public bool CaptureLandmarks { get; set; }

        /// <summary>Initialize 가 사용한 절대 모델 경로.</summary>
        public string ModelPath { get { return _modelPath; } }

        /// <summary>Initialize 에 걸린 시간(ms).</summary>
        public long InitElapsedMs { get { return _initElapsedMs; } }

        /// <summary>Initialize 성공 여부.</summary>
        public bool IsInitialized { get { return _initialized; } }

        /// <summary>마지막 Initialize 결과. 아직 호출 전이면 null.</summary>
        public InitResult LastInitResult { get { return _initResult; } }

        /// <summary>DetectFineOcclusion 을 계속 쓸 수 있는지. 런타임 실패 시 false 로 내려가고 다시 올라오지 않는다.</summary>
        public bool FineOcclusionAvailable { get { return _fineOcclusionAvailable; } }

        /// <summary>FineOcclusion 이 꺼진 이유. 켜져 있으면 null.</summary>
        public string FineOcclusionDisabledReason { get { return _fineOcclusionDisabledReason; } }

        /// <summary>
        /// 마지막으로 실패한 '속성' 호출의 사유(CheckMask / Occlusion / ClosedEyes / Landmark).
        /// 진단 표시용. 판정에 쓰지 말 것 — 프레임 단위로 지워지지 않는다.
        /// </summary>
        public string LastAttributeError { get; private set; }

        /// <summary>App.config 의 EnableLandmark106 을 반영해 만든다(기본 true).</summary>
        public FaceEngine(AppSettings settings)
            : this(settings, AppSettingsLoader.GetBool("EnableLandmark106", true))
        {
        }

        public FaceEngine(AppSettings settings, bool captureLandmarks)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            _settings = settings;
            CaptureLandmarks = captureLandmarks;

            // Initialize() 전에도 ModelPath 를 읽는 곳이 있다(초기화 대기 중 UI 표시).
            // 여기서 미리 확정해 둔다. Initialize() 는 같은 값을 다시 계산한다.
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
        }

        //
        // Initialize
        //

        /// <summary>
        /// FaceSDK 를 초기화한다. 모델 경로는 <c>AppDomain.CurrentDomain.BaseDirectory\models</c> (절대경로),
        /// sdk_path 는 빈 문자열(CONTRACT.md 1장).
        ///
        /// 성공하면 결과를 캐시하고 재호출 시 그대로 돌려준다.
        /// 실패는 캐시하지 않는다 — models/license 를 고친 뒤 다시 시도할 수 있어야 한다.
        ///
        /// 이 호출은 모델 로딩 때문에 수 초가 걸린다. UI 스레드에서 부르면 창이 멈춘다.
        /// CONTRACT.md 3장(UI 스레드에서 SDK 호출 금지)에 맞춰 <b>워커 스레드에서 부르는 것을 권장</b>한다.
        /// (AnalysisWorker 가 시작 시 아직 초기화되지 않았으면 자기 스레드에서 대신 불러준다)
        /// </summary>
        public InitResult Initialize()
        {
            if (_initialized && _initResult != null)
                return _initResult;

            Stopwatch sw = Stopwatch.StartNew();
            InitResult r = new InitResult();

            try
            {
                if (_disposed)
                {
                    r.Ok = false;
                    r.ErrorTitle = "이미 종료된 엔진입니다";
                    r.ErrorDetail = "FaceEngine 이 Dispose 된 뒤에는 다시 초기화할 수 없습니다. 앱을 재시작하세요.";
                    return r;
                }

                _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
                r.ModelPath = _modelPath;

                // SDK 에 넘기기 전에 먼저 확인해 준다. SDK 에러코드보다 이쪽이 훨씬 친절하다.
                if (!Directory.Exists(_modelPath))
                {
                    r.Ok = false;
                    r.ErrorTitle = "모델 폴더가 없습니다";
                    r.ErrorDetail =
                        "찾은 경로: " + _modelPath + Environment.NewLine + Environment.NewLine +
                        "tools\\setup-runtime.bat 을 실행해 models 폴더를 실행 폴더로 복사하세요.";
                    return r;
                }

                string licensePath = Path.Combine(_modelPath, "license.cer");
                if (!File.Exists(licensePath))
                {
                    r.Ok = false;
                    r.ErrorTitle = "라이선스 파일(license.cer)이 없습니다";
                    r.ErrorDetail =
                        "찾은 경로: " + licensePath + Environment.NewLine + Environment.NewLine +
                        "models 폴더 안에 license.cer 이 있어야 합니다." + Environment.NewLine +
                        "tools\\setup-runtime.bat 으로 models 를 복사하면 함께 따라갑니다." + Environment.NewLine +
                        "(별도 활성화 절차는 없습니다 — 파일만 있으면 됩니다)";
                    return r;
                }

                _sdk = FaceSDK.Instance();

                // CONTRACT.md 1장: sdk_path 는 빈 문자열.
                FaceSDK.Rst rst = _sdk.Initialize(_modelPath, "");

                if (rst.IsOk())
                {
                    _initialized = true;
                    r.Ok = true;
                    return r;
                }

                r.Ok = false;
                FillInitError(r, rst);
                return r;
            }
            catch (DllNotFoundException ex)
            {
                r.Ok = false;
                r.ErrorTitle = "SDK DLL 을 찾지 못했습니다";
                r.ErrorDetail =
                    "AlcheraFaceSDKCS.dll / AlcheraFaceSDK.dll / opencv_world455.dll 이 exe 폴더에 있어야 합니다." +
                    Environment.NewLine + Environment.NewLine +
                    "tools\\setup-runtime.bat 을 실행하고 프로젝트를 다시 빌드하세요." +
                    Environment.NewLine + Environment.NewLine + ex.Message;
                return r;
            }
            catch (BadImageFormatException ex)
            {
                r.Ok = false;
                r.ErrorTitle = "32/64비트가 맞지 않습니다";
                r.ErrorDetail =
                    "FaceSDK native 는 x64 전용입니다. 빌드 플랫폼이 x64 인지(PlatformTarget=x64, Prefer32Bit=false) 확인하세요." +
                    Environment.NewLine + Environment.NewLine + ex.Message;
                return r;
            }
            catch (Exception ex)
            {
                r.Ok = false;
                r.ErrorTitle = "SDK 초기화 중 예외가 발생했습니다";
                r.ErrorDetail = ex.GetType().Name + ": " + ex.Message;
                return r;
            }
            finally
            {
                sw.Stop();
                _initElapsedMs = sw.ElapsedMilliseconds;
                r.ElapsedMs = _initElapsedMs;
                if (string.IsNullOrEmpty(r.ModelPath)) r.ModelPath = _modelPath;
                _initResult = r;
            }
        }

        /// <summary>Error enum 을 현장에서 바로 조치할 수 있는 한국어 문구로 바꾼다.</summary>
        static void FillInitError(InitResult r, FaceSDK.Rst rst)
        {
            string raw = rst.GetLastErrStr() + " / " + (rst.GetLastErrDesc() ?? "");

            switch (rst.GetLastErr())
            {
                case FaceSDK.Error.CanNotReadModel:
                    r.ErrorTitle = "모델 파일을 읽을 수 없습니다";
                    r.ErrorDetail =
                        "models 폴더에 모델 파일이 없거나 손상되었습니다." + Environment.NewLine +
                        "경로: " + r.ModelPath + Environment.NewLine + Environment.NewLine +
                        "tools\\setup-runtime.bat 으로 models 를 다시 복사하세요." +
                        " (0.f 는 260MB 입니다. 복사가 중간에 끊기지 않았는지 크기를 확인하세요.)" +
                        Environment.NewLine + Environment.NewLine + "SDK: " + raw;
                    break;

                case FaceSDK.Error.InvalidLicense:
                case FaceSDK.Error.CanNotReadLicense:
                    r.ErrorTitle = "라이선스가 유효하지 않습니다";
                    r.ErrorDetail =
                        "models 폴더에 license.cer 이 있는지 확인하세요." + Environment.NewLine +
                        "경로: " + r.ModelPath + "\\license.cer" + Environment.NewLine + Environment.NewLine +
                        "파일이 있는데도 이 오류가 나면 라이선스 자체가 이 SDK 버전/기능에 맞지 않는 것입니다." +
                        Environment.NewLine +
                        "(파일만 있으면 되며 별도 활성화 절차는 없습니다)" +
                        Environment.NewLine + Environment.NewLine + "SDK: " + raw;
                    break;

                case FaceSDK.Error.LicenseExpired:
                    r.ErrorTitle = "라이선스 기간이 만료되었습니다";
                    r.ErrorDetail =
                        "라이선스 유효기간이 지났습니다. PC 의 날짜/시각이 맞는지 먼저 확인하고," +
                        " 그래도 같으면 새 라이선스를 발급받아야 합니다." +
                        Environment.NewLine + Environment.NewLine + "SDK: " + raw;
                    break;

                case FaceSDK.Error.LicenseNotStarted:
                    r.ErrorTitle = "라이선스 시작일이 아직 되지 않았습니다";
                    r.ErrorDetail =
                        "PC 의 날짜/시각이 라이선스 시작일보다 이전입니다. 시스템 시각을 확인하세요." +
                        Environment.NewLine + Environment.NewLine + "SDK: " + raw;
                    break;

                case FaceSDK.Error.SystemTimeTampered:
                    r.ErrorTitle = "시스템 시각이 변조된 것으로 판단되었습니다";
                    r.ErrorDetail =
                        "PC 시각을 과거로 되돌린 흔적이 감지되었습니다. 시각을 현재로 맞추고 다시 실행하세요." +
                        Environment.NewLine + Environment.NewLine + "SDK: " + raw;
                    break;

                default:
                    r.ErrorTitle = "SDK 초기화 실패";
                    r.ErrorDetail = raw;
                    break;
            }
        }

        //
        // Analyze
        //

        /// <summary>
        /// 프레임 1장을 분석한다. 반드시 <paramref name="bgr"/> 은 연속된 BGR 8UC3 버퍼여야 하고
        /// 길이가 w*h*3 이상이어야 한다.
        ///
        /// 호출 순서 (CONTRACT.md 1장 — 속성 함수는 DetectFace 가 준 Face 를 ref 로 재사용한다):
        ///   1) DetectFace(continuous=true)
        ///   2) face_cnt == 0 이면 조기 반환 (Presence 판정은 Analysis 계층 몫)
        ///   3) ComputeLandmarkConfidence   (매 프레임)
        ///   4) CheckMask                   (AttrIntervalFrames 마다, 그 외에는 캐시 재사용)
        ///   5) DetectFaceOcclusion         (매 프레임)
        ///   6) DetectFineOcclusion         (AttrIntervalFrames 마다, 가용할 때만)
        ///   7) DetectClosedEyes            (매 프레임, DetectFace 와 같은 버퍼)
        /// <b>좌표계</b>: 결과의 box / Landmark106 은 여기 넘긴 이미지(<paramref name="w"/> x <paramref name="h"/>)
        /// 의 픽셀 좌표다. 실제로는 CameraCapture 가 전처리한 분석 이미지(기본 720x1280)이며
        /// 카메라 캡처 원본 좌표가 아니다 (CONTRACT.md 부록 A-7).
        /// </summary>
        /// <param name="bgr">
        /// 전처리가 끝난 BGR 연속 버퍼. 호출 중에만 유효하면 된다(내부에서 보관하지 않는다).
        /// </param>
        /// <param name="monotonicSec">Stopwatch 기반 단조 증가 시각(초). DateTime 금지.</param>
        public FrameObservation Analyze(byte[] bgr, int w, int h, long frameIndex, double monotonicSec)
        {
            FrameObservation obs = NewObservation(frameIndex, monotonicSec);

            // 조기 반환 경로에서도 값이 있어야 한다. 이 필드는 프레임 단위가 아니라 '기능 가용성' 이다.
            obs.FineOcclusionAvailable = _fineOcclusionAvailable;

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                if (_disposed)
                {
                    obs.SdkError = "FaceEngine 이 이미 Dispose 되었습니다.";
                    return obs;
                }

                if (!_initialized || _sdk == null)
                {
                    obs.SdkError = "FaceSDK 가 초기화되지 않았습니다.";
                    return obs;
                }

                if (bgr == null || w <= 0 || h <= 0 || bgr.LongLength < (long)w * h * 3)
                {
                    obs.SdkError = "입력 프레임이 유효하지 않습니다 (w=" + w + " h=" + h +
                                   " len=" + (bgr == null ? 0 : bgr.Length) + ", 필요=" + ((long)w * h * 3) + ")";
                    return obs;
                }

                // --- 1) DetectFace ---
                FaceSDK.Rst det;
                try
                {
                    // 카메라 연속 입력이므로 continuous tracking on.
                    det = _sdk.DetectFace(bgr, w, h, true);
                }
                catch (Exception ex)
                {
                    obs.SdkError = "DetectFace 예외: " + ex.GetType().Name + ": " + ex.Message;
                    ResetAttrCache();
                    return obs;
                }

                if (det.IsErr())
                {
                    obs.SdkError = "DetectFace: " + det.GetLastErrStr() + " / " + det.GetLastErrDesc();
                    ResetAttrCache();
                    return obs;
                }

                obs.FaceCount = det.GetFaceCnt();

                // --- 2) 얼굴 없음 → 조기 반환 ---
                if (!det.IsFaceDetected())
                {
                    obs.FaceDetected = false;
                    // 얼굴이 사라졌다면 캐시된 마스크/미세가림 값은 더 이상 이 프레임의 것이 아니다.
                    ResetAttrCache();
                    _lastTrackId = int.MinValue;
                    return obs;
                }

                obs.FaceDetected = true;

                FaceSDK.Face face = det.GetFace();

                obs.FaceTrackId = face.id;
                obs.BoxX = face.box.x;
                obs.BoxY = face.box.y;
                obs.BoxW = face.box.w;
                obs.BoxH = face.box.h;
                obs.Yaw = face.pose.yaw;
                obs.Pitch = face.pose.pitch;
                obs.Roll = face.pose.roll;

                if (face.id != _lastTrackId)
                {
                    ResetAttrCache();
                    _lastTrackId = face.id;
                }

                // --- 3) landmark confidence (매 프레임) ---
                try
                {
                    FaceSDK.Rst lm = _sdk.ComputeLandmarkConfidence(bgr, w, h, ref face);
                    if (lm.IsOk())
                        obs.LandmarkConfidence = lm.face_landmark_confidence;
                    else
                        NoteAttrError("ComputeLandmarkConfidence", lm);
                }
                catch (Exception ex)
                {
                    NoteAttrError("ComputeLandmarkConfidence", ex);
                }

                bool attrTick = _settings.AttrIntervalFrames <= 1 ||
                                (frameIndex % _settings.AttrIntervalFrames) == 0;

                // --- 4) CheckMask (주기 호출 + 캐시) ---
                if (attrTick)
                {
                    try
                    {
                        FaceSDK.Rst mk = _sdk.CheckMask(bgr, w, h, ref face);
                        if (mk.IsOk())
                        {
                            _cachedMaskConf = mk.face_mask_confidence;
                            // 반전 로직을 재구현하지 말 것 (CONTRACT.md 1장).
                            // face_mask_confidence 는 '낮을수록' 마스크 착용이다.
                            _cachedIsMasked = mk.IsFaceMasked();
                            _hasMaskCache = true;
                        }
                        else
                        {
                            // IsOk()==false 면 face_mask_confidence 는 0 으로 남는다.
                            // 0 < 0.5 이므로 IsFaceMasked() 는 true 를 돌려준다 = 가짜 '마스크 착용'.
                            // 그래서 절대 읽지 않는다.
                            NoteAttrError("CheckMask", mk);
                        }
                    }
                    catch (Exception ex)
                    {
                        NoteAttrError("CheckMask", ex);
                    }
                }

                if (_hasMaskCache)
                {
                    obs.MaskConfidence = _cachedMaskConf;
                    obs.IsMasked = _cachedIsMasked;
                }
                // 캐시가 없으면 MaskConfidence 는 NaN 으로 남는다.
                // IsMasked 는 bool 이라 NaN 이 없다 → 미측정 여부는 반드시 MaskConfidence 의 NaN 으로 판단할 것.

                // --- 5) DetectFaceOcclusion (매 프레임) ---
                try
                {
                    FaceSDK.Rst oc = _sdk.DetectFaceOcclusion(bgr, w, h, ref face);
                    if (oc.IsOk())
                    {
                        obs.OcclusionLeftEye = oc.face_occlu_conf_left_eye;
                        obs.OcclusionRightEye = oc.face_occlu_conf_right_eye;
                        obs.OcclusionMouth = oc.face_occlu_conf_mouth;
                    }
                    else
                    {
                        NoteAttrError("DetectFaceOcclusion", oc);
                    }
                }
                catch (Exception ex)
                {
                    NoteAttrError("DetectFaceOcclusion", ex);
                }

                // --- 6) DetectFineOcclusion (주기 호출 + 캐시, 가용할 때만) ---
                if (_fineOcclusionAvailable && attrTick)
                    TryFineOcclusion(bgr, w, h, ref face);

                if (_hasFineCache)
                    obs.FineOcclusion = _cachedFineOcc;

                // 이 프레임에서 비활성으로 떨어졌을 수 있으므로 다시 반영한다.
                obs.FineOcclusionAvailable = _fineOcclusionAvailable;

                // --- 7) DetectClosedEyes (매 프레임, DetectFace 와 같은 버퍼) ---
                try
                {
                    FaceSDK.Rst ey = _sdk.DetectClosedEyes(bgr, w, h, ref face);
                    if (ey.IsOk())
                    {
                        obs.EyelidLeft = ey.img_face_eye_state_left_eyelid_dist;
                        obs.EyelidRight = ey.img_face_eye_state_right_eyelid_dist;
                        obs.EyelidValid = true;
                    }
                    else
                    {
                        // 에러 시 눈꺼풀 거리는 0 으로 남는다. 0 을 '눈 감김'으로 읽으면 안 되므로 NaN 유지.
                        obs.EyelidValid = false;
                        NoteAttrError("DetectClosedEyes", ey);
                    }
                }
                catch (Exception ex)
                {
                    obs.EyelidValid = false;
                    NoteAttrError("DetectClosedEyes", ex);
                }

                // --- 8) landmark 106 (오버레이 전용, 옵션) ---
                if (CaptureLandmarks)
                    obs.Landmark106 = FlattenLandmark(ref face);

                return obs;
            }
            finally
            {
                sw.Stop();
                obs.SdkElapsedMs = sw.Elapsed.TotalMilliseconds;
            }
        }

        void TryFineOcclusion(byte[] bgr, int w, int h, ref FaceSDK.Face face)
        {
            try
            {
                FaceSDK.Rst fo = _sdk.DetectFineOcclusion(bgr, w, h, ref face);

                if (fo.IsOk())
                {
                    // 주의: wrapper 는 face_fine_occlu_score 를 IsOk() 밖에서 대입한다(FaceSDK.cs:1342 부근).
                    //       즉 에러여도 0 이 들어 있다. 반드시 IsOk() 안에서만 읽는다.
                    _cachedFineOcc = fo.face_fine_occlu_score;
                    _hasFineCache = true;
                    return;
                }

                // 모델이 없으면 매 프레임 같은 에러가 난다. 한 번 실패하면 다시 부르지 않는다.
                DisableFineOcclusion(fo.GetLastErrStr() + " / " + fo.GetLastErrDesc());
            }
            catch (EntryPointNotFoundException ex)
            {
                // native 가 구버전이라 fsdkc_attr_detect_fine_occlusion 자체가 없는 경우.
                DisableFineOcclusion("native 에 함수가 없습니다(구버전 AlcheraFaceSDKCS.dll): " + ex.Message);
            }
            catch (DllNotFoundException ex)
            {
                DisableFineOcclusion("DLL 을 찾지 못했습니다: " + ex.Message);
            }
            catch (Exception ex)
            {
                // 예상 못 한 예외로 앱이 죽으면 안 된다. 기능만 끄고 계속 간다.
                DisableFineOcclusion(ex.GetType().Name + ": " + ex.Message);
            }
        }

        void DisableFineOcclusion(string reason)
        {
            _fineOcclusionAvailable = false;
            _fineOcclusionDisabledReason = reason;
            _hasFineCache = false;
            _cachedFineOcc = float.NaN;
            NoteAttrError("DetectFineOcclusion 자동 비활성화: " + reason);
        }

        /// <summary>106점을 float[212] (x0,y0,x1,y1,...) 로 평탄화한다. 오버레이 전용.</summary>
        static float[] FlattenLandmark(ref FaceSDK.Face face)
        {
            try
            {
                // ToArray() 는 리플렉션 + 박싱을 106회 한다. 프레임당 비용이 있으니 옵션으로 끌 수 있게 해 뒀다.
                FaceSDK.Point[] pts = face.landmark.ToArray();
                if (pts == null) return null;

                float[] flat = new float[pts.Length * 2];
                for (int i = 0; i < pts.Length; i++)
                {
                    flat[i * 2] = pts[i].x;
                    flat[i * 2 + 1] = pts[i].y;
                }
                return flat;
            }
            catch (Exception)
            {
                // 오버레이는 없어도 되는 정보다. 실패해도 조용히 null.
                return null;
            }
        }

        static FrameObservation NewObservation(long frameIndex, double monotonicSec)
        {
            FrameObservation obs = new FrameObservation();

            obs.FrameIndex = frameIndex;
            obs.MonotonicSec = monotonicSec;
            obs.WallClock = DateTime.Now;   // 로그 표기 전용. 판정에 쓰지 말 것.
            obs.FaceTrackId = -1;

            // 못 잰 값은 0 이 아니라 NaN 이다. 0 은 '측정했더니 0' 과 구분되지 않는다.
            obs.LandmarkConfidence = float.NaN;
            obs.MaskConfidence = float.NaN;
            obs.OcclusionLeftEye = float.NaN;
            obs.OcclusionRightEye = float.NaN;
            obs.OcclusionMouth = float.NaN;
            obs.FineOcclusion = float.NaN;
            obs.EyelidLeft = float.NaN;
            obs.EyelidRight = float.NaN;

            return obs;
        }

        void ResetAttrCache()
        {
            _hasMaskCache = false;
            _cachedMaskConf = float.NaN;
            _cachedIsMasked = false;

            _hasFineCache = false;
            _cachedFineOcc = float.NaN;
        }

        void NoteAttrError(string what, FaceSDK.Rst rst)
        {
            NoteAttrError(what + ": " + rst.GetLastErrStr() + " / " + rst.GetLastErrDesc());
        }

        void NoteAttrError(string what, Exception ex)
        {
            NoteAttrError(what + " 예외: " + ex.GetType().Name + ": " + ex.Message);
        }

        void NoteAttrError(string message)
        {
            LastAttributeError = message;
        }

        //
        // Dispose
        //

        /// <summary>
        /// FaceSDK.DestroyInstance() 를 호출한다(내부에서 DeInitialize 까지 한다). 이중 호출 안전.
        /// 워커 스레드를 먼저 세운 뒤에 부를 것 — 분석 중에 부르면 native 가 사용 중인 자원을 내린다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _initialized = false;

            try
            {
                FaceSDK.DestroyInstance();
            }
            catch (Exception)
            {
                // 종료 경로다. 여기서 예외를 올려봐야 앱만 지저분하게 죽는다.
            }
            finally
            {
                _sdk = null;
            }
        }
    }
}
