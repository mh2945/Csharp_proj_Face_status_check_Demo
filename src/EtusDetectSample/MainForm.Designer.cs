namespace Etoos.DetectSample
{
    partial class MainForm
    {
        /// <summary>필수 디자이너 변수입니다.</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>사용 중인 모든 리소스를 정리합니다.</summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 폼이 소유한 GDI 리소스(프레임 버퍼 Bitmap 2장, Font)를 회수한다. MainForm.cs 참조.
                DisposeOwnedResources();
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다.
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마십시오.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolStripTop = new System.Windows.Forms.ToolStrip();
            this.tsLblCamera = new System.Windows.Forms.ToolStripLabel();
            this.tsCmbCamera = new System.Windows.Forms.ToolStripComboBox();
            this.tsSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.tsBtnStart = new System.Windows.Forms.ToolStripButton();
            this.tsBtnStop = new System.Windows.Forms.ToolStripButton();
            this.tsSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.tsBtnSnapshots = new System.Windows.Forms.ToolStripButton();
            this.tsBtnSettings = new System.Windows.Forms.ToolStripButton();
            this.tsLblFps = new System.Windows.Forms.ToolStripLabel();
            this.tsLblEngine = new System.Windows.Forms.ToolStripLabel();
            this.statusStripBottom = new System.Windows.Forms.StatusStrip();
            this.tsslModel = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsslInit = new System.Windows.Forms.ToolStripStatusLabel();
            this.tsslLog = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblError = new System.Windows.Forms.Label();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.splitTop = new System.Windows.Forms.SplitContainer();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.lblPreviewOverlay = new System.Windows.Forms.Label();
            this.pnlRight = new System.Windows.Forms.Panel();
            this.tblRight = new System.Windows.Forms.TableLayoutPanel();
            this.tblBadge = new System.Windows.Forms.TableLayoutPanel();
            this.lblStateBig = new System.Windows.Forms.Label();
            this.lblStateCode = new System.Windows.Forms.Label();
            this.lblStateElapsed = new System.Windows.Forms.Label();
            this.lblUnknownReason = new System.Windows.Forms.Label();
            this.lblSigHeader = new System.Windows.Forms.Label();
            this.lblEyeK = new System.Windows.Forms.Label();
            this.lblEyeV = new System.Windows.Forms.Label();
            this.lblClosedK = new System.Windows.Forms.Label();
            this.lblClosedV = new System.Windows.Forms.Label();
            this.lblPerclosK = new System.Windows.Forms.Label();
            this.lblPerclosV = new System.Windows.Forms.Label();
            this.lblNoFaceK = new System.Windows.Forms.Label();
            this.lblNoFaceV = new System.Windows.Forms.Label();
            this.lblLandmarkK = new System.Windows.Forms.Label();
            this.lblLandmarkV = new System.Windows.Forms.Label();
            this.lblMaskK = new System.Windows.Forms.Label();
            this.lblMaskV = new System.Windows.Forms.Label();
            this.lblOcclK = new System.Windows.Forms.Label();
            this.lblOcclV = new System.Windows.Forms.Label();
            this.lblFineK = new System.Windows.Forms.Label();
            this.lblFineV = new System.Windows.Forms.Label();
            this.lblPoseK = new System.Windows.Forms.Label();
            this.lblPoseV = new System.Windows.Forms.Label();
            this.lblFaceIdK = new System.Windows.Forms.Label();
            this.lblFaceIdV = new System.Windows.Forms.Label();
            this.lblAccHeader = new System.Windows.Forms.Label();
            this.lblSeatedK = new System.Windows.Forms.Label();
            this.lblSeatedV = new System.Windows.Forms.Label();
            this.lblStudyK = new System.Windows.Forms.Label();
            this.lblStudyV = new System.Windows.Forms.Label();
            this.lblDrowsyK = new System.Windows.Forms.Label();
            this.lblDrowsyV = new System.Windows.Forms.Label();
            this.lblAwayK = new System.Windows.Forms.Label();
            this.lblAwayV = new System.Windows.Forms.Label();
            this.lblUnknownK = new System.Windows.Forms.Label();
            this.lblUnknownV = new System.Windows.Forms.Label();
            this.lblDisclaimer = new System.Windows.Forms.Label();
            this.lblAlertHeader = new System.Windows.Forms.Label();
            this.lvAlerts = new System.Windows.Forms.ListView();
            this.chTime = new System.Windows.Forms.ColumnHeader();
            this.chType = new System.Windows.Forms.ColumnHeader();
            this.chLevel = new System.Windows.Forms.ColumnHeader();
            this.chMessage = new System.Windows.Forms.ColumnHeader();
            this.chPayload = new System.Windows.Forms.ColumnHeader();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.toolStripTop.SuspendLayout();
            this.statusStripBottom.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitTop)).BeginInit();
            this.splitTop.Panel1.SuspendLayout();
            this.splitTop.Panel2.SuspendLayout();
            this.splitTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.pnlRight.SuspendLayout();
            this.tblRight.SuspendLayout();
            this.tblBadge.SuspendLayout();
            this.SuspendLayout();
            // 
            // toolStripTop
            // 
            this.toolStripTop.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripTop.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStripTop.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsLblCamera,
            this.tsCmbCamera,
            this.tsSep1,
            this.tsBtnStart,
            this.tsBtnStop,
            this.tsSep2,
            this.tsBtnSnapshots,
            this.tsBtnSettings,
            this.tsLblFps,
            this.tsLblEngine});
            this.toolStripTop.Location = new System.Drawing.Point(0, 0);
            this.toolStripTop.Name = "toolStripTop";
            this.toolStripTop.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            this.toolStripTop.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStripTop.Size = new System.Drawing.Size(980, 33);
            this.toolStripTop.TabIndex = 0;
            // 
            // tsLblCamera
            // 
            this.tsLblCamera.Name = "tsLblCamera";
            this.tsLblCamera.Size = new System.Drawing.Size(47, 26);
            this.tsLblCamera.Text = "카메라";
            // 
            // tsCmbCamera
            // 
            this.tsCmbCamera.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.tsCmbCamera.Name = "tsCmbCamera";
            this.tsCmbCamera.Size = new System.Drawing.Size(220, 29);
            // 
            // tsSep1
            // 
            this.tsSep1.Name = "tsSep1";
            this.tsSep1.Size = new System.Drawing.Size(6, 29);
            // 
            // tsBtnStart
            // 
            this.tsBtnStart.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsBtnStart.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.tsBtnStart.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(110)))), ((int)(((byte)(64)))));
            this.tsBtnStart.Name = "tsBtnStart";
            this.tsBtnStart.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.tsBtnStart.Size = new System.Drawing.Size(80, 26);
            this.tsBtnStart.Text = "▶  시작";
            // 
            // tsBtnStop
            // 
            this.tsBtnStop.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsBtnStop.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.tsBtnStop.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.tsBtnStop.Name = "tsBtnStop";
            this.tsBtnStop.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.tsBtnStop.Size = new System.Drawing.Size(80, 26);
            this.tsBtnStop.Text = "■  정지";
            //
            // tsSep2
            //
            this.tsSep2.Name = "tsSep2";
            this.tsSep2.Size = new System.Drawing.Size(6, 29);
            //
            // tsBtnSnapshots
            //
            this.tsBtnSnapshots.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsBtnSnapshots.Name = "tsBtnSnapshots";
            this.tsBtnSnapshots.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.tsBtnSnapshots.Size = new System.Drawing.Size(110, 26);
            this.tsBtnSnapshots.Text = "스냅샷 폴더 열기";
            //
            // tsBtnSettings
            //
            this.tsBtnSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tsBtnSettings.Name = "tsBtnSettings";
            this.tsBtnSettings.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.tsBtnSettings.Size = new System.Drawing.Size(80, 26);
            this.tsBtnSettings.Text = "판정 설정";
            //
            // tsLblFps
            // 
            this.tsLblFps.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.tsLblFps.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.tsLblFps.Name = "tsLblFps";
            this.tsLblFps.Padding = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.tsLblFps.Size = new System.Drawing.Size(70, 26);
            this.tsLblFps.Text = "-- FPS";
            // 
            // tsLblEngine
            // 
            this.tsLblEngine.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.tsLblEngine.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(102)))), ((int)(((byte)(110)))));
            this.tsLblEngine.Name = "tsLblEngine";
            this.tsLblEngine.Size = new System.Drawing.Size(70, 26);
            this.tsLblEngine.Text = "FaceSDK  ·";
            // 
            // statusStripBottom
            // 
            this.statusStripBottom.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsslModel,
            this.tsslInit,
            this.tsslLog});
            this.statusStripBottom.Location = new System.Drawing.Point(0, 878);
            this.statusStripBottom.Name = "statusStripBottom";
            this.statusStripBottom.Size = new System.Drawing.Size(980, 22);
            this.statusStripBottom.SizingGrip = false;
            this.statusStripBottom.TabIndex = 3;
            // 
            // tsslModel
            // 
            this.tsslModel.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.tsslModel.Name = "tsslModel";
            this.tsslModel.Size = new System.Drawing.Size(60, 17);
            this.tsslModel.Text = "모델: -";
            // 
            // tsslInit
            // 
            this.tsslInit.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;
            this.tsslInit.Name = "tsslInit";
            this.tsslInit.Size = new System.Drawing.Size(60, 17);
            this.tsslInit.Text = "초기화: -";
            // 
            // tsslLog
            // 
            this.tsslLog.Name = "tsslLog";
            this.tsslLog.Size = new System.Drawing.Size(60, 17);
            this.tsslLog.Text = "로그: -";
            // 
            // lblError
            // 
            this.lblError.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(198)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblError.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblError.Font = new System.Drawing.Font("맑은 고딕", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblError.ForeColor = System.Drawing.Color.White;
            this.lblError.Location = new System.Drawing.Point(0, 33);
            this.lblError.Name = "lblError";
            this.lblError.Padding = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.lblError.Size = new System.Drawing.Size(980, 28);
            this.lblError.TabIndex = 1;
            this.lblError.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblError.Visible = false;
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitMain.Location = new System.Drawing.Point(0, 33);
            this.splitMain.Name = "splitMain";
            this.splitMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.splitTop);
            this.splitMain.Panel1MinSize = 240;
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.lvAlerts);
            this.splitMain.Panel2.Controls.Add(this.lblAlertHeader);
            this.splitMain.Panel2MinSize = 120;
            this.splitMain.Size = new System.Drawing.Size(980, 845);
            this.splitMain.SplitterDistance = 640;
            this.splitMain.SplitterWidth = 5;
            this.splitMain.TabIndex = 2;
            // 
            // splitTop
            // 
            this.splitTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitTop.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitTop.Location = new System.Drawing.Point(0, 0);
            this.splitTop.Name = "splitTop";
            // 
            // splitTop.Panel1
            // 
            this.splitTop.Panel1.Controls.Add(this.lblPreviewOverlay);
            this.splitTop.Panel1.Controls.Add(this.picPreview);
            this.splitTop.Panel1MinSize = 200;
            // 
            // splitTop.Panel2
            // 
            this.splitTop.Panel2.Controls.Add(this.pnlRight);
            this.splitTop.Panel2MinSize = 420;
            this.splitTop.Size = new System.Drawing.Size(980, 640);
            this.splitTop.SplitterDistance = 400;
            this.splitTop.SplitterWidth = 5;
            this.splitTop.TabIndex = 0;
            // 
            // picPreview
            // 
            this.picPreview.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(26)))), ((int)(((byte)(30)))));
            this.picPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.Location = new System.Drawing.Point(0, 0);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(400, 640);
            this.picPreview.TabIndex = 0;
            this.picPreview.TabStop = false;
            //
            // lblPreviewOverlay
            //
            this.lblPreviewOverlay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(26)))), ((int)(((byte)(30)))));
            this.lblPreviewOverlay.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPreviewOverlay.Font = new System.Drawing.Font("맑은 고딕", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblPreviewOverlay.ForeColor = System.Drawing.Color.White;
            this.lblPreviewOverlay.Location = new System.Drawing.Point(0, 0);
            this.lblPreviewOverlay.Name = "lblPreviewOverlay";
            this.lblPreviewOverlay.Size = new System.Drawing.Size(400, 640);
            this.lblPreviewOverlay.TabIndex = 1;
            this.lblPreviewOverlay.Text = "카메라를 준비하는 중입니다. 잠시만 기다려주세요...";
            this.lblPreviewOverlay.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblPreviewOverlay.Visible = false;
            this.lblPreviewOverlay.BringToFront();
            //
            // pnlRight
            // 
            this.pnlRight.BackColor = System.Drawing.Color.White;
            this.pnlRight.Controls.Add(this.tblRight);
            this.pnlRight.Controls.Add(this.lblDisclaimer);
            this.pnlRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlRight.Location = new System.Drawing.Point(0, 0);
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Size = new System.Drawing.Size(575, 640);
            this.pnlRight.TabIndex = 0;
            // 
            // tblRight
            // 
            this.tblRight.AutoScroll = true;
            this.tblRight.BackColor = System.Drawing.Color.White;
            this.tblRight.ColumnCount = 2;
            this.tblRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 42F));
            this.tblRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58F));
            this.tblRight.Controls.Add(this.tblBadge, 0, 0);
            this.tblRight.Controls.Add(this.lblSigHeader, 0, 1);
            this.tblRight.Controls.Add(this.lblEyeK, 0, 2);
            this.tblRight.Controls.Add(this.lblEyeV, 1, 2);
            this.tblRight.Controls.Add(this.lblClosedK, 0, 3);
            this.tblRight.Controls.Add(this.lblClosedV, 1, 3);
            this.tblRight.Controls.Add(this.lblPerclosK, 0, 4);
            this.tblRight.Controls.Add(this.lblPerclosV, 1, 4);
            this.tblRight.Controls.Add(this.lblNoFaceK, 0, 5);
            this.tblRight.Controls.Add(this.lblNoFaceV, 1, 5);
            this.tblRight.Controls.Add(this.lblLandmarkK, 0, 6);
            this.tblRight.Controls.Add(this.lblLandmarkV, 1, 6);
            this.tblRight.Controls.Add(this.lblMaskK, 0, 7);
            this.tblRight.Controls.Add(this.lblMaskV, 1, 7);
            this.tblRight.Controls.Add(this.lblOcclK, 0, 8);
            this.tblRight.Controls.Add(this.lblOcclV, 1, 8);
            this.tblRight.Controls.Add(this.lblFineK, 0, 9);
            this.tblRight.Controls.Add(this.lblFineV, 1, 9);
            this.tblRight.Controls.Add(this.lblPoseK, 0, 10);
            this.tblRight.Controls.Add(this.lblPoseV, 1, 10);
            this.tblRight.Controls.Add(this.lblFaceIdK, 0, 11);
            this.tblRight.Controls.Add(this.lblFaceIdV, 1, 11);
            this.tblRight.Controls.Add(this.lblAccHeader, 0, 12);
            this.tblRight.Controls.Add(this.lblSeatedK, 0, 13);
            this.tblRight.Controls.Add(this.lblSeatedV, 1, 13);
            this.tblRight.Controls.Add(this.lblStudyK, 0, 14);
            this.tblRight.Controls.Add(this.lblStudyV, 1, 14);
            this.tblRight.Controls.Add(this.lblDrowsyK, 0, 15);
            this.tblRight.Controls.Add(this.lblDrowsyV, 1, 15);
            this.tblRight.Controls.Add(this.lblAwayK, 0, 16);
            this.tblRight.Controls.Add(this.lblAwayV, 1, 16);
            this.tblRight.Controls.Add(this.lblUnknownK, 0, 17);
            this.tblRight.Controls.Add(this.lblUnknownV, 1, 17);
            this.tblRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblRight.Location = new System.Drawing.Point(0, 0);
            this.tblRight.Name = "tblRight";
            this.tblRight.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
            this.tblRight.RowCount = 18;
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 26F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this.tblRight.Size = new System.Drawing.Size(575, 582);
            this.tblRight.TabIndex = 0;
            // 
            // tblBadge
            // 
            this.tblBadge.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(116)))), ((int)(((byte)(124)))));
            this.tblBadge.ColumnCount = 2;
            this.tblRight.SetColumnSpan(this.tblBadge, 2);
            this.tblBadge.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tblBadge.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tblBadge.Controls.Add(this.lblStateBig, 0, 0);
            this.tblBadge.Controls.Add(this.lblStateCode, 0, 1);
            this.tblBadge.Controls.Add(this.lblStateElapsed, 1, 1);
            this.tblBadge.Controls.Add(this.lblUnknownReason, 0, 2);
            this.tblBadge.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tblBadge.Location = new System.Drawing.Point(10, 8);
            this.tblBadge.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.tblBadge.Name = "tblBadge";
            this.tblBadge.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this.tblBadge.RowCount = 3;
            this.tblBadge.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this.tblBadge.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tblBadge.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this.tblBadge.Size = new System.Drawing.Size(555, 102);
            this.tblBadge.TabIndex = 0;
            // 
            // lblStateBig
            // 
            this.lblStateBig.BackColor = System.Drawing.Color.Transparent;
            this.tblBadge.SetColumnSpan(this.lblStateBig, 2);
            this.lblStateBig.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStateBig.Font = new System.Drawing.Font("맑은 고딕", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblStateBig.ForeColor = System.Drawing.Color.White;
            this.lblStateBig.Location = new System.Drawing.Point(10, 4);
            this.lblStateBig.Margin = new System.Windows.Forms.Padding(0);
            this.lblStateBig.Name = "lblStateBig";
            this.lblStateBig.Size = new System.Drawing.Size(535, 50);
            this.lblStateBig.TabIndex = 0;
            this.lblStateBig.Text = "대기";
            this.lblStateBig.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblStateCode
            // 
            this.lblStateCode.BackColor = System.Drawing.Color.Transparent;
            this.lblStateCode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStateCode.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblStateCode.ForeColor = System.Drawing.Color.White;
            this.lblStateCode.Location = new System.Drawing.Point(10, 54);
            this.lblStateCode.Margin = new System.Windows.Forms.Padding(0);
            this.lblStateCode.Name = "lblStateCode";
            this.lblStateCode.Size = new System.Drawing.Size(267, 22);
            this.lblStateCode.TabIndex = 1;
            this.lblStateCode.Text = "IDLE";
            this.lblStateCode.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblStateElapsed
            // 
            this.lblStateElapsed.BackColor = System.Drawing.Color.Transparent;
            this.lblStateElapsed.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStateElapsed.Font = new System.Drawing.Font("맑은 고딕", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblStateElapsed.ForeColor = System.Drawing.Color.White;
            this.lblStateElapsed.Location = new System.Drawing.Point(277, 54);
            this.lblStateElapsed.Margin = new System.Windows.Forms.Padding(0);
            this.lblStateElapsed.Name = "lblStateElapsed";
            this.lblStateElapsed.Size = new System.Drawing.Size(268, 22);
            this.lblStateElapsed.TabIndex = 2;
            this.lblStateElapsed.Text = "00:00:00";
            this.lblStateElapsed.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblUnknownReason
            // 
            this.lblUnknownReason.BackColor = System.Drawing.Color.Transparent;
            this.tblBadge.SetColumnSpan(this.lblUnknownReason, 2);
            this.lblUnknownReason.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUnknownReason.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblUnknownReason.ForeColor = System.Drawing.Color.White;
            this.lblUnknownReason.Location = new System.Drawing.Point(10, 76);
            this.lblUnknownReason.Margin = new System.Windows.Forms.Padding(0);
            this.lblUnknownReason.Name = "lblUnknownReason";
            this.lblUnknownReason.Size = new System.Drawing.Size(535, 22);
            this.lblUnknownReason.TabIndex = 3;
            this.lblUnknownReason.Text = "";
            this.lblUnknownReason.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblSigHeader
            // 
            this.lblSigHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(241)))), ((int)(((byte)(245)))));
            this.tblRight.SetColumnSpan(this.lblSigHeader, 2);
            this.lblSigHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSigHeader.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblSigHeader.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(88)))));
            this.lblSigHeader.Location = new System.Drawing.Point(10, 118);
            this.lblSigHeader.Margin = new System.Windows.Forms.Padding(0);
            this.lblSigHeader.Name = "lblSigHeader";
            this.lblSigHeader.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblSigHeader.Size = new System.Drawing.Size(555, 26);
            this.lblSigHeader.TabIndex = 1;
            this.lblSigHeader.Text = "실시간 신호";
            this.lblSigHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblEyeK
            // 
            this.lblEyeK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEyeK.Location = new System.Drawing.Point(10, 144);
            this.lblEyeK.Margin = new System.Windows.Forms.Padding(0);
            this.lblEyeK.Name = "lblEyeK";
            this.lblEyeK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblEyeK.Size = new System.Drawing.Size(233, 24);
            this.lblEyeK.TabIndex = 2;
            this.lblEyeK.Text = "눈 L / R";
            this.lblEyeK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblEyeV
            // 
            this.lblEyeV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEyeV.Location = new System.Drawing.Point(243, 144);
            this.lblEyeV.Margin = new System.Windows.Forms.Padding(0);
            this.lblEyeV.Name = "lblEyeV";
            this.lblEyeV.Size = new System.Drawing.Size(322, 24);
            this.lblEyeV.TabIndex = 3;
            this.lblEyeV.Text = "-";
            this.lblEyeV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblClosedK
            // 
            this.lblClosedK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblClosedK.Location = new System.Drawing.Point(10, 168);
            this.lblClosedK.Margin = new System.Windows.Forms.Padding(0);
            this.lblClosedK.Name = "lblClosedK";
            this.lblClosedK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblClosedK.Size = new System.Drawing.Size(233, 24);
            this.lblClosedK.TabIndex = 4;
            this.lblClosedK.Text = "연속 감김";
            this.lblClosedK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblClosedV
            // 
            this.lblClosedV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblClosedV.Location = new System.Drawing.Point(243, 168);
            this.lblClosedV.Margin = new System.Windows.Forms.Padding(0);
            this.lblClosedV.Name = "lblClosedV";
            this.lblClosedV.Size = new System.Drawing.Size(322, 24);
            this.lblClosedV.TabIndex = 5;
            this.lblClosedV.Text = "-";
            this.lblClosedV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPerclosK
            // 
            this.lblPerclosK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPerclosK.Location = new System.Drawing.Point(10, 192);
            this.lblPerclosK.Margin = new System.Windows.Forms.Padding(0);
            this.lblPerclosK.Name = "lblPerclosK";
            this.lblPerclosK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblPerclosK.Size = new System.Drawing.Size(233, 24);
            this.lblPerclosK.TabIndex = 6;
            this.lblPerclosK.Text = "PERCLOS";
            this.lblPerclosK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPerclosV
            // 
            this.lblPerclosV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPerclosV.Location = new System.Drawing.Point(243, 192);
            this.lblPerclosV.Margin = new System.Windows.Forms.Padding(0);
            this.lblPerclosV.Name = "lblPerclosV";
            this.lblPerclosV.Size = new System.Drawing.Size(322, 24);
            this.lblPerclosV.TabIndex = 7;
            this.lblPerclosV.Text = "-";
            this.lblPerclosV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblNoFaceK
            // 
            this.lblNoFaceK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNoFaceK.Location = new System.Drawing.Point(10, 216);
            this.lblNoFaceK.Margin = new System.Windows.Forms.Padding(0);
            this.lblNoFaceK.Name = "lblNoFaceK";
            this.lblNoFaceK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblNoFaceK.Size = new System.Drawing.Size(233, 24);
            this.lblNoFaceK.TabIndex = 8;
            this.lblNoFaceK.Text = "얼굴 미검출";
            this.lblNoFaceK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblNoFaceV
            // 
            this.lblNoFaceV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNoFaceV.Location = new System.Drawing.Point(243, 216);
            this.lblNoFaceV.Margin = new System.Windows.Forms.Padding(0);
            this.lblNoFaceV.Name = "lblNoFaceV";
            this.lblNoFaceV.Size = new System.Drawing.Size(322, 24);
            this.lblNoFaceV.TabIndex = 9;
            this.lblNoFaceV.Text = "-";
            this.lblNoFaceV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLandmarkK
            // 
            this.lblLandmarkK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLandmarkK.Location = new System.Drawing.Point(10, 240);
            this.lblLandmarkK.Margin = new System.Windows.Forms.Padding(0);
            this.lblLandmarkK.Name = "lblLandmarkK";
            this.lblLandmarkK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblLandmarkK.Size = new System.Drawing.Size(233, 24);
            this.lblLandmarkK.TabIndex = 10;
            this.lblLandmarkK.Text = "Landmark 신뢰도";
            this.lblLandmarkK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLandmarkV
            // 
            this.lblLandmarkV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLandmarkV.Location = new System.Drawing.Point(243, 240);
            this.lblLandmarkV.Margin = new System.Windows.Forms.Padding(0);
            this.lblLandmarkV.Name = "lblLandmarkV";
            this.lblLandmarkV.Size = new System.Drawing.Size(322, 24);
            this.lblLandmarkV.TabIndex = 11;
            this.lblLandmarkV.Text = "-";
            this.lblLandmarkV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblMaskK
            // 
            this.lblMaskK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMaskK.Location = new System.Drawing.Point(10, 264);
            this.lblMaskK.Margin = new System.Windows.Forms.Padding(0);
            this.lblMaskK.Name = "lblMaskK";
            this.lblMaskK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblMaskK.Size = new System.Drawing.Size(233, 24);
            this.lblMaskK.TabIndex = 12;
            this.lblMaskK.Text = "Mask";
            this.lblMaskK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblMaskV
            // 
            this.lblMaskV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMaskV.Location = new System.Drawing.Point(243, 264);
            this.lblMaskV.Margin = new System.Windows.Forms.Padding(0);
            this.lblMaskV.Name = "lblMaskV";
            this.lblMaskV.Size = new System.Drawing.Size(322, 24);
            this.lblMaskV.TabIndex = 13;
            this.lblMaskV.Text = "-";
            this.lblMaskV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblOcclK
            // 
            this.lblOcclK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOcclK.Location = new System.Drawing.Point(10, 288);
            this.lblOcclK.Margin = new System.Windows.Forms.Padding(0);
            this.lblOcclK.Name = "lblOcclK";
            this.lblOcclK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblOcclK.Size = new System.Drawing.Size(233, 24);
            this.lblOcclK.TabIndex = 14;
            this.lblOcclK.Text = "Occlusion L/R/M";
            this.lblOcclK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblOcclV
            // 
            this.lblOcclV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOcclV.Location = new System.Drawing.Point(243, 288);
            this.lblOcclV.Margin = new System.Windows.Forms.Padding(0);
            this.lblOcclV.Name = "lblOcclV";
            this.lblOcclV.Size = new System.Drawing.Size(322, 24);
            this.lblOcclV.TabIndex = 15;
            this.lblOcclV.Text = "-";
            this.lblOcclV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblFineK
            // 
            this.lblFineK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFineK.Location = new System.Drawing.Point(10, 312);
            this.lblFineK.Margin = new System.Windows.Forms.Padding(0);
            this.lblFineK.Name = "lblFineK";
            this.lblFineK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblFineK.Size = new System.Drawing.Size(233, 24);
            this.lblFineK.TabIndex = 16;
            this.lblFineK.Text = "FineOcclusion";
            this.lblFineK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblFineV
            // 
            this.lblFineV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFineV.Location = new System.Drawing.Point(243, 312);
            this.lblFineV.Margin = new System.Windows.Forms.Padding(0);
            this.lblFineV.Name = "lblFineV";
            this.lblFineV.Size = new System.Drawing.Size(322, 24);
            this.lblFineV.TabIndex = 17;
            this.lblFineV.Text = "-";
            this.lblFineV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPoseK
            // 
            this.lblPoseK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPoseK.Location = new System.Drawing.Point(10, 336);
            this.lblPoseK.Margin = new System.Windows.Forms.Padding(0);
            this.lblPoseK.Name = "lblPoseK";
            this.lblPoseK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblPoseK.Size = new System.Drawing.Size(233, 24);
            this.lblPoseK.TabIndex = 18;
            this.lblPoseK.Text = "Pose Y / P / R";
            this.lblPoseK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPoseV
            // 
            this.lblPoseV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPoseV.Location = new System.Drawing.Point(243, 336);
            this.lblPoseV.Margin = new System.Windows.Forms.Padding(0);
            this.lblPoseV.Name = "lblPoseV";
            this.lblPoseV.Size = new System.Drawing.Size(322, 24);
            this.lblPoseV.TabIndex = 19;
            this.lblPoseV.Text = "-";
            this.lblPoseV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblFaceIdK
            // 
            this.lblFaceIdK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFaceIdK.Location = new System.Drawing.Point(10, 360);
            this.lblFaceIdK.Margin = new System.Windows.Forms.Padding(0);
            this.lblFaceIdK.Name = "lblFaceIdK";
            this.lblFaceIdK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblFaceIdK.Size = new System.Drawing.Size(233, 24);
            this.lblFaceIdK.TabIndex = 20;
            this.lblFaceIdK.Text = "인식 상태";
            this.lblFaceIdK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblFaceIdV
            // 
            this.lblFaceIdV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblFaceIdV.Location = new System.Drawing.Point(243, 360);
            this.lblFaceIdV.Margin = new System.Windows.Forms.Padding(0);
            this.lblFaceIdV.Name = "lblFaceIdV";
            this.lblFaceIdV.Size = new System.Drawing.Size(322, 24);
            this.lblFaceIdV.TabIndex = 21;
            this.lblFaceIdV.Text = "-";
            this.lblFaceIdV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAccHeader
            // 
            this.lblAccHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(241)))), ((int)(((byte)(245)))));
            this.tblRight.SetColumnSpan(this.lblAccHeader, 2);
            this.lblAccHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAccHeader.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblAccHeader.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(88)))));
            this.lblAccHeader.Location = new System.Drawing.Point(10, 384);
            this.lblAccHeader.Margin = new System.Windows.Forms.Padding(0);
            this.lblAccHeader.Name = "lblAccHeader";
            this.lblAccHeader.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblAccHeader.Size = new System.Drawing.Size(555, 26);
            this.lblAccHeader.TabIndex = 22;
            this.lblAccHeader.Text = "누적 데이터";
            this.lblAccHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblSeatedK
            // 
            this.lblSeatedK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSeatedK.Location = new System.Drawing.Point(10, 410);
            this.lblSeatedK.Margin = new System.Windows.Forms.Padding(0);
            this.lblSeatedK.Name = "lblSeatedK";
            this.lblSeatedK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblSeatedK.Size = new System.Drawing.Size(233, 24);
            this.lblSeatedK.TabIndex = 23;
            this.lblSeatedK.Text = "착석 시간";
            this.lblSeatedK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblSeatedV
            // 
            this.lblSeatedV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSeatedV.Location = new System.Drawing.Point(243, 410);
            this.lblSeatedV.Margin = new System.Windows.Forms.Padding(0);
            this.lblSeatedV.Name = "lblSeatedV";
            this.lblSeatedV.Size = new System.Drawing.Size(322, 24);
            this.lblSeatedV.TabIndex = 24;
            this.lblSeatedV.Text = "00:00:00";
            this.lblSeatedV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblStudyK
            // 
            this.lblStudyK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStudyK.Location = new System.Drawing.Point(10, 434);
            this.lblStudyK.Margin = new System.Windows.Forms.Padding(0);
            this.lblStudyK.Name = "lblStudyK";
            this.lblStudyK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblStudyK.Size = new System.Drawing.Size(233, 24);
            this.lblStudyK.TabIndex = 25;
            this.lblStudyK.Text = "공부 시간";
            this.lblStudyK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblStudyV
            // 
            this.lblStudyV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStudyV.Location = new System.Drawing.Point(243, 434);
            this.lblStudyV.Margin = new System.Windows.Forms.Padding(0);
            this.lblStudyV.Name = "lblStudyV";
            this.lblStudyV.Size = new System.Drawing.Size(322, 24);
            this.lblStudyV.TabIndex = 26;
            this.lblStudyV.Text = "00:00:00";
            this.lblStudyV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblDrowsyK
            // 
            this.lblDrowsyK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDrowsyK.Location = new System.Drawing.Point(10, 458);
            this.lblDrowsyK.Margin = new System.Windows.Forms.Padding(0);
            this.lblDrowsyK.Name = "lblDrowsyK";
            this.lblDrowsyK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblDrowsyK.Size = new System.Drawing.Size(233, 24);
            this.lblDrowsyK.TabIndex = 27;
            this.lblDrowsyK.Text = "졸음";
            this.lblDrowsyK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblDrowsyV
            // 
            this.lblDrowsyV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDrowsyV.Location = new System.Drawing.Point(243, 458);
            this.lblDrowsyV.Margin = new System.Windows.Forms.Padding(0);
            this.lblDrowsyV.Name = "lblDrowsyV";
            this.lblDrowsyV.Size = new System.Drawing.Size(322, 24);
            this.lblDrowsyV.TabIndex = 28;
            this.lblDrowsyV.Text = "00:00:00";
            this.lblDrowsyV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAwayK
            // 
            this.lblAwayK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAwayK.Location = new System.Drawing.Point(10, 482);
            this.lblAwayK.Margin = new System.Windows.Forms.Padding(0);
            this.lblAwayK.Name = "lblAwayK";
            this.lblAwayK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblAwayK.Size = new System.Drawing.Size(233, 24);
            this.lblAwayK.TabIndex = 29;
            this.lblAwayK.Text = "이석";
            this.lblAwayK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblAwayV
            // 
            this.lblAwayV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblAwayV.Location = new System.Drawing.Point(243, 482);
            this.lblAwayV.Margin = new System.Windows.Forms.Padding(0);
            this.lblAwayV.Name = "lblAwayV";
            this.lblAwayV.Size = new System.Drawing.Size(322, 24);
            this.lblAwayV.TabIndex = 30;
            this.lblAwayV.Text = "00:00:00";
            this.lblAwayV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblUnknownK
            // 
            this.lblUnknownK.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUnknownK.Location = new System.Drawing.Point(10, 506);
            this.lblUnknownK.Margin = new System.Windows.Forms.Padding(0);
            this.lblUnknownK.Name = "lblUnknownK";
            this.lblUnknownK.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.lblUnknownK.Size = new System.Drawing.Size(233, 24);
            this.lblUnknownK.TabIndex = 31;
            this.lblUnknownK.Text = "판정 불가";
            this.lblUnknownK.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblUnknownV
            // 
            this.lblUnknownV.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblUnknownV.Location = new System.Drawing.Point(243, 506);
            this.lblUnknownV.Margin = new System.Windows.Forms.Padding(0);
            this.lblUnknownV.Name = "lblUnknownV";
            this.lblUnknownV.Size = new System.Drawing.Size(322, 24);
            this.lblUnknownV.TabIndex = 32;
            this.lblUnknownV.Text = "00:00:00";
            this.lblUnknownV.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // lblDisclaimer
            // 
            this.lblDisclaimer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(251)))));
            this.lblDisclaimer.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblDisclaimer.Font = new System.Drawing.Font("맑은 고딕", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblDisclaimer.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(126)))), ((int)(((byte)(134)))));
            this.lblDisclaimer.Location = new System.Drawing.Point(0, 582);
            this.lblDisclaimer.Name = "lblDisclaimer";
            this.lblDisclaimer.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this.lblDisclaimer.Size = new System.Drawing.Size(575, 58);
            this.lblDisclaimer.TabIndex = 1;
            this.lblDisclaimer.Text = "※ 공부 시간 = 깨어 있는 시간입니다. 휴대폰·멍함·대화는 구분하지 못합니다.\r\n※ 안면 인식 기반이므로 엎드림과 이석을 완전히 구분할 수 없습니다.";
            // 
            // lblAlertHeader
            // 
            this.lblAlertHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(241)))), ((int)(((byte)(245)))));
            this.lblAlertHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblAlertHeader.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.lblAlertHeader.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(78)))), ((int)(((byte)(88)))));
            this.lblAlertHeader.Location = new System.Drawing.Point(0, 0);
            this.lblAlertHeader.Name = "lblAlertHeader";
            this.lblAlertHeader.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.lblAlertHeader.Size = new System.Drawing.Size(980, 26);
            this.lblAlertHeader.TabIndex = 0;
            this.lblAlertHeader.Text = "Alert 로그   ·   행을 더블클릭하면 중앙 인포데스크로 전달되는 전체 JSON payload 를 볼 수 있습니다";
            this.lblAlertHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lvAlerts
            // 
            this.lvAlerts.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.chTime,
            this.chType,
            this.chLevel,
            this.chMessage,
            this.chPayload});
            this.lvAlerts.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvAlerts.FullRowSelect = true;
            this.lvAlerts.GridLines = true;
            this.lvAlerts.HideSelection = false;
            this.lvAlerts.Location = new System.Drawing.Point(0, 26);
            this.lvAlerts.MultiSelect = false;
            this.lvAlerts.Name = "lvAlerts";
            this.lvAlerts.Size = new System.Drawing.Size(980, 174);
            this.lvAlerts.TabIndex = 1;
            this.lvAlerts.UseCompatibleStateImageBehavior = false;
            this.lvAlerts.View = System.Windows.Forms.View.Details;
            // 
            // chTime
            // 
            this.chTime.Text = "시각";
            this.chTime.Width = 80;
            // 
            // chType
            // 
            this.chType.Text = "종류";
            this.chType.Width = 64;
            // 
            // chLevel
            // 
            this.chLevel.Text = "수준";
            this.chLevel.Width = 56;
            // 
            // chMessage
            // 
            this.chMessage.Text = "메시지";
            this.chMessage.Width = 320;
            // 
            // chPayload
            // 
            this.chPayload.Text = "payload (더블클릭 = 전체 보기)";
            this.chPayload.Width = 440;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(980, 900);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.statusStripBottom);
            this.Controls.Add(this.lblError);
            this.Controls.Add(this.toolStripTop);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.MinimumSize = new System.Drawing.Size(880, 880);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "[FaceSDK] 학습 집중도 모니터링 Demo";
            this.toolStripTop.ResumeLayout(false);
            this.toolStripTop.PerformLayout();
            this.statusStripBottom.ResumeLayout(false);
            this.statusStripBottom.PerformLayout();
            this.tblBadge.ResumeLayout(false);
            this.tblRight.ResumeLayout(false);
            this.pnlRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.splitTop.Panel1.ResumeLayout(false);
            this.splitTop.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitTop)).EndInit();
            this.splitTop.ResumeLayout(false);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStripTop;
        private System.Windows.Forms.ToolStripLabel tsLblCamera;
        private System.Windows.Forms.ToolStripComboBox tsCmbCamera;
        private System.Windows.Forms.ToolStripSeparator tsSep1;
        private System.Windows.Forms.ToolStripButton tsBtnStart;
        private System.Windows.Forms.ToolStripButton tsBtnStop;
        private System.Windows.Forms.ToolStripSeparator tsSep2;
        private System.Windows.Forms.ToolStripButton tsBtnSnapshots;
        private System.Windows.Forms.ToolStripButton tsBtnSettings;
        private System.Windows.Forms.ToolStripLabel tsLblFps;
        private System.Windows.Forms.ToolStripLabel tsLblEngine;
        private System.Windows.Forms.StatusStrip statusStripBottom;
        private System.Windows.Forms.ToolStripStatusLabel tsslModel;
        private System.Windows.Forms.ToolStripStatusLabel tsslInit;
        private System.Windows.Forms.ToolStripStatusLabel tsslLog;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.SplitContainer splitTop;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Label lblPreviewOverlay;
        private System.Windows.Forms.Panel pnlRight;
        private System.Windows.Forms.TableLayoutPanel tblRight;
        private System.Windows.Forms.TableLayoutPanel tblBadge;
        private System.Windows.Forms.Label lblStateBig;
        private System.Windows.Forms.Label lblStateCode;
        private System.Windows.Forms.Label lblStateElapsed;
        private System.Windows.Forms.Label lblUnknownReason;
        private System.Windows.Forms.Label lblSigHeader;
        private System.Windows.Forms.Label lblEyeK;
        private System.Windows.Forms.Label lblEyeV;
        private System.Windows.Forms.Label lblClosedK;
        private System.Windows.Forms.Label lblClosedV;
        private System.Windows.Forms.Label lblPerclosK;
        private System.Windows.Forms.Label lblPerclosV;
        private System.Windows.Forms.Label lblNoFaceK;
        private System.Windows.Forms.Label lblNoFaceV;
        private System.Windows.Forms.Label lblLandmarkK;
        private System.Windows.Forms.Label lblLandmarkV;
        private System.Windows.Forms.Label lblMaskK;
        private System.Windows.Forms.Label lblMaskV;
        private System.Windows.Forms.Label lblOcclK;
        private System.Windows.Forms.Label lblOcclV;
        private System.Windows.Forms.Label lblFineK;
        private System.Windows.Forms.Label lblFineV;
        private System.Windows.Forms.Label lblPoseK;
        private System.Windows.Forms.Label lblPoseV;
        private System.Windows.Forms.Label lblFaceIdK;
        private System.Windows.Forms.Label lblFaceIdV;
        private System.Windows.Forms.Label lblAccHeader;
        private System.Windows.Forms.Label lblSeatedK;
        private System.Windows.Forms.Label lblSeatedV;
        private System.Windows.Forms.Label lblStudyK;
        private System.Windows.Forms.Label lblStudyV;
        private System.Windows.Forms.Label lblDrowsyK;
        private System.Windows.Forms.Label lblDrowsyV;
        private System.Windows.Forms.Label lblAwayK;
        private System.Windows.Forms.Label lblAwayV;
        private System.Windows.Forms.Label lblUnknownK;
        private System.Windows.Forms.Label lblUnknownV;
        private System.Windows.Forms.Label lblDisclaimer;
        private System.Windows.Forms.Label lblAlertHeader;
        private System.Windows.Forms.ListView lvAlerts;
        private System.Windows.Forms.ColumnHeader chTime;
        private System.Windows.Forms.ColumnHeader chType;
        private System.Windows.Forms.ColumnHeader chLevel;
        private System.Windows.Forms.ColumnHeader chMessage;
        private System.Windows.Forms.ColumnHeader chPayload;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
