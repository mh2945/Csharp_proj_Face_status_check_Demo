using System;
using System.Configuration;
using System.Globalization;
using System.Windows.Forms;
using Etoos.DetectSample.Config;

namespace Etoos.DetectSample
{
    /// <summary>
    /// "판정불가"(SeatState.Unknown) 게이트 임계값을 감시 중에도 실시간으로 조정하는 창.
    ///
    /// 컨트롤 값이 바뀌면 즉시 전달받은 <see cref="AppSettings"/> 인스턴스의 필드를 직접 mutate 한다.
    /// EyeStateGate/SeatStateMachine 이 같은 인스턴스 참조를 들고 매 프레임 필드를 다시 읽으므로
    /// 별도 "적용" 배선 없이 다음 프레임부터 반영된다. (AppSettings.cs 가 "읽기 전용 계약 파일"이라는
    /// 규칙은 필드 '추가'에 대한 것이지, 이미 있는 필드의 런타임 값 조정과는 무관하다.)
    ///
    /// "저장"은 System.Configuration 으로 배포된 exe.config 파일에 값을 써서 재시작해도 유지되게 한다.
    /// FASMH-94 명시값과 카메라/성능 설정은 다루지 않는다 — 이 창의 목적은 "판정불가" 튜닝이지
    /// 전체 설정 편집기가 아니다. 다른 컴포넌트(AlertSnapshotWriter 등)와 같은 원칙으로,
    /// 저장 실패를 포함해 어떤 예외도 밖으로 던지지 않는다.
    /// </summary>
    public partial class SettingsForm : Form
    {
        readonly AppSettings _settings;
        readonly Action<string> _onError;
        bool _loading;

        public SettingsForm(AppSettings settings, Action<string> onError)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            _settings = settings;
            _onError = onError;

            InitializeComponent();
            WireEvents();
            LoadFromSettings();
        }

        void WireEvents()
        {
            numEyelid.ValueChanged += (s, e) => Apply();
            numLandmarkConfMin.ValueChanged += (s, e) => Apply();
            numOcclusionMax.ValueChanged += (s, e) => Apply();
            numFineOcclusionMax.ValueChanged += (s, e) => Apply();
            chkUseFineOcclusionGate.CheckedChanged += (s, e) => Apply();
            numPoseYawMaxDeg.ValueChanged += (s, e) => Apply();
            numPosePitchMaxDeg.ValueChanged += (s, e) => Apply();
            numUnknownGraceSec.ValueChanged += (s, e) => Apply();

            btnDefaults.Click += BtnDefaults_Click;
            btnSave.Click += BtnSave_Click;
        }

        /// <summary>연 시점의 실제 적용 값을 컨트롤에 채운다. ValueChanged 가 같이 튀지 않도록 _loading 으로 막는다.</summary>
        void LoadFromSettings()
        {
            _loading = true;
            try
            {
                numEyelid.Value = ToDecimal(_settings.EyelidClosedThreshold, numEyelid);
                numLandmarkConfMin.Value = ToDecimal(_settings.LandmarkConfMin, numLandmarkConfMin);
                numOcclusionMax.Value = ToDecimal(_settings.OcclusionMax, numOcclusionMax);
                numFineOcclusionMax.Value = ToDecimal(_settings.FineOcclusionMax, numFineOcclusionMax);
                chkUseFineOcclusionGate.Checked = _settings.UseFineOcclusionGate;
                numPoseYawMaxDeg.Value = ToDecimal(_settings.PoseYawMaxDeg, numPoseYawMaxDeg);
                numPosePitchMaxDeg.Value = ToDecimal(_settings.PosePitchMaxDeg, numPosePitchMaxDeg);
                numUnknownGraceSec.Value = ToDecimal(_settings.UnknownGraceSec, numUnknownGraceSec);
            }
            finally
            {
                _loading = false;
            }
            SetStatus("현재 적용 중인 값입니다. 값을 바꾸면 감시 중에도 즉시 반영됩니다.");
        }

        static decimal ToDecimal(double v, NumericUpDown target)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return target.Minimum;
            decimal d;
            try { d = (decimal)v; }
            catch (OverflowException) { return target.Minimum; }
            if (d < target.Minimum) return target.Minimum;
            if (d > target.Maximum) return target.Maximum;
            return d;
        }

        /// <summary>화면 값을 실제 실행 중인 AppSettings 인스턴스에 즉시 반영한다.</summary>
        void Apply()
        {
            if (_loading) return;

            _settings.EyelidClosedThreshold = (double)numEyelid.Value;
            _settings.LandmarkConfMin = (double)numLandmarkConfMin.Value;
            _settings.OcclusionMax = (double)numOcclusionMax.Value;
            _settings.FineOcclusionMax = (double)numFineOcclusionMax.Value;
            _settings.UseFineOcclusionGate = chkUseFineOcclusionGate.Checked;
            _settings.PoseYawMaxDeg = (double)numPoseYawMaxDeg.Value;
            _settings.PosePitchMaxDeg = (double)numPosePitchMaxDeg.Value;
            _settings.UnknownGraceSec = (double)numUnknownGraceSec.Value;

            SetStatus("적용됨 — 재시작하면 사라집니다. 유지하려면 \"저장\"을 누르세요.");
        }

        void BtnDefaults_Click(object sender, EventArgs e)
        {
            AppSettings d = new AppSettings();

            _loading = true;
            try
            {
                numEyelid.Value = ToDecimal(d.EyelidClosedThreshold, numEyelid);
                numLandmarkConfMin.Value = ToDecimal(d.LandmarkConfMin, numLandmarkConfMin);
                numOcclusionMax.Value = ToDecimal(d.OcclusionMax, numOcclusionMax);
                numFineOcclusionMax.Value = ToDecimal(d.FineOcclusionMax, numFineOcclusionMax);
                chkUseFineOcclusionGate.Checked = d.UseFineOcclusionGate;
                numPoseYawMaxDeg.Value = ToDecimal(d.PoseYawMaxDeg, numPoseYawMaxDeg);
                numPosePitchMaxDeg.Value = ToDecimal(d.PosePitchMaxDeg, numPosePitchMaxDeg);
                numUnknownGraceSec.Value = ToDecimal(d.UnknownGraceSec, numUnknownGraceSec);
            }
            finally
            {
                _loading = false;
            }

            Apply();
            SetStatus("코드 기본값으로 되돌렸습니다 — 재시작하면 사라집니다. 유지하려면 \"저장\"을 누르세요.");
        }

        void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                Configuration cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                SetKey(cfg, "EyelidClosedThreshold", _settings.EyelidClosedThreshold);
                SetKey(cfg, "LandmarkConfMin", _settings.LandmarkConfMin);
                SetKey(cfg, "OcclusionMax", _settings.OcclusionMax);
                SetKey(cfg, "FineOcclusionMax", _settings.FineOcclusionMax);
                SetKey(cfg, "UseFineOcclusionGate", _settings.UseFineOcclusionGate ? "true" : "false");
                SetKey(cfg, "PoseYawMaxDeg", _settings.PoseYawMaxDeg);
                SetKey(cfg, "PosePitchMaxDeg", _settings.PosePitchMaxDeg);
                SetKey(cfg, "UnknownGraceSec", _settings.UnknownGraceSec);

                cfg.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                SetStatus("저장되었습니다. 다음 실행부터 이 값이 기본으로 적용됩니다.");
            }
            catch (Exception ex)
            {
                SetStatus("저장 실패: " + ex.Message);
                if (_onError != null) _onError("판정 설정 저장 실패: " + ex.Message);
            }
        }

        static void SetKey(Configuration cfg, string key, double value)
        {
            SetKey(cfg, key, value.ToString(CultureInfo.InvariantCulture));
        }

        static void SetKey(Configuration cfg, string key, string value)
        {
            if (cfg.AppSettings.Settings[key] == null)
                cfg.AppSettings.Settings.Add(key, value);
            else
                cfg.AppSettings.Settings[key].Value = value;
        }

        void SetStatus(string message)
        {
            lblStatus.Text = message;
        }
    }
}
