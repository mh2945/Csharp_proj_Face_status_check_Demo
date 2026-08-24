namespace Etoos.DetectSample
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblEyelid = new System.Windows.Forms.Label();
            this.numEyelid = new System.Windows.Forms.NumericUpDown();
            this.lblLandmarkConfMin = new System.Windows.Forms.Label();
            this.numLandmarkConfMin = new System.Windows.Forms.NumericUpDown();
            this.lblOcclusionMax = new System.Windows.Forms.Label();
            this.numOcclusionMax = new System.Windows.Forms.NumericUpDown();
            this.lblFineOcclusionMax = new System.Windows.Forms.Label();
            this.numFineOcclusionMax = new System.Windows.Forms.NumericUpDown();
            this.lblUseFineOcclusionGate = new System.Windows.Forms.Label();
            this.chkUseFineOcclusionGate = new System.Windows.Forms.CheckBox();
            this.lblPoseYawMaxDeg = new System.Windows.Forms.Label();
            this.numPoseYawMaxDeg = new System.Windows.Forms.NumericUpDown();
            this.lblPosePitchMaxDeg = new System.Windows.Forms.Label();
            this.numPosePitchMaxDeg = new System.Windows.Forms.NumericUpDown();
            this.lblUnknownGraceSec = new System.Windows.Forms.Label();
            this.numUnknownGraceSec = new System.Windows.Forms.NumericUpDown();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnDefaults = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numEyelid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLandmarkConfMin)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numOcclusionMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFineOcclusionMax)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPoseYawMaxDeg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPosePitchMaxDeg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numUnknownGraceSec)).BeginInit();
            this.SuspendLayout();
            //
            // lblEyelid
            //
            this.lblEyelid.Location = new System.Drawing.Point(16, 18);
            this.lblEyelid.Size = new System.Drawing.Size(250, 24);
            this.lblEyelid.Text = "눈 감김 임계값 (EyelidClosedThreshold)";
            //
            // numEyelid
            //
            this.numEyelid.DecimalPlaces = 2;
            this.numEyelid.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numEyelid.Location = new System.Drawing.Point(272, 16);
            this.numEyelid.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this.numEyelid.Minimum = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numEyelid.Size = new System.Drawing.Size(140, 23);
            this.numEyelid.Value = new decimal(new int[] { 24, 0, 0, 65536 });
            //
            // lblLandmarkConfMin
            //
            this.lblLandmarkConfMin.Location = new System.Drawing.Point(16, 52);
            this.lblLandmarkConfMin.Size = new System.Drawing.Size(250, 24);
            this.lblLandmarkConfMin.Text = "얼굴 인식 신뢰도 하한 (LandmarkConfMin)";
            //
            // numLandmarkConfMin
            //
            this.numLandmarkConfMin.DecimalPlaces = 2;
            this.numLandmarkConfMin.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numLandmarkConfMin.Location = new System.Drawing.Point(272, 50);
            this.numLandmarkConfMin.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLandmarkConfMin.Size = new System.Drawing.Size(140, 23);
            this.numLandmarkConfMin.Value = new decimal(new int[] { 75, 0, 0, 131072 });
            //
            // lblOcclusionMax
            //
            this.lblOcclusionMax.Location = new System.Drawing.Point(16, 86);
            this.lblOcclusionMax.Size = new System.Drawing.Size(250, 24);
            this.lblOcclusionMax.Text = "눈 가림 허용 상한 (OcclusionMax)";
            //
            // numOcclusionMax
            //
            this.numOcclusionMax.DecimalPlaces = 2;
            this.numOcclusionMax.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numOcclusionMax.Location = new System.Drawing.Point(272, 84);
            this.numOcclusionMax.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numOcclusionMax.Size = new System.Drawing.Size(140, 23);
            this.numOcclusionMax.Value = new decimal(new int[] { 60, 0, 0, 131072 });
            //
            // lblFineOcclusionMax
            //
            this.lblFineOcclusionMax.Location = new System.Drawing.Point(16, 120);
            this.lblFineOcclusionMax.Size = new System.Drawing.Size(250, 24);
            this.lblFineOcclusionMax.Text = "정밀 가림 허용 상한 (FineOcclusionMax)";
            //
            // numFineOcclusionMax
            //
            this.numFineOcclusionMax.DecimalPlaces = 2;
            this.numFineOcclusionMax.Increment = new decimal(new int[] { 1, 0, 0, 131072 });
            this.numFineOcclusionMax.Location = new System.Drawing.Point(272, 118);
            this.numFineOcclusionMax.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numFineOcclusionMax.Size = new System.Drawing.Size(140, 23);
            this.numFineOcclusionMax.Value = new decimal(new int[] { 60, 0, 0, 131072 });
            //
            // lblUseFineOcclusionGate
            //
            this.lblUseFineOcclusionGate.Location = new System.Drawing.Point(16, 154);
            this.lblUseFineOcclusionGate.Size = new System.Drawing.Size(250, 24);
            this.lblUseFineOcclusionGate.Text = "정밀 가림 게이트 사용 (UseFineOcclusionGate)";
            //
            // chkUseFineOcclusionGate
            //
            this.chkUseFineOcclusionGate.Location = new System.Drawing.Point(272, 154);
            this.chkUseFineOcclusionGate.Size = new System.Drawing.Size(140, 24);
            this.chkUseFineOcclusionGate.Checked = true;
            //
            // lblPoseYawMaxDeg
            //
            this.lblPoseYawMaxDeg.Location = new System.Drawing.Point(16, 188);
            this.lblPoseYawMaxDeg.Size = new System.Drawing.Size(250, 24);
            this.lblPoseYawMaxDeg.Text = "고개 좌우 허용 각도° (PoseYawMaxDeg)";
            //
            // numPoseYawMaxDeg
            //
            this.numPoseYawMaxDeg.DecimalPlaces = 1;
            this.numPoseYawMaxDeg.Increment = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPoseYawMaxDeg.Location = new System.Drawing.Point(272, 186);
            this.numPoseYawMaxDeg.Maximum = new decimal(new int[] { 90, 0, 0, 0 });
            this.numPoseYawMaxDeg.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numPoseYawMaxDeg.Size = new System.Drawing.Size(140, 23);
            this.numPoseYawMaxDeg.Value = new decimal(new int[] { 45, 0, 0, 0 });
            //
            // lblPosePitchMaxDeg
            //
            this.lblPosePitchMaxDeg.Location = new System.Drawing.Point(16, 222);
            this.lblPosePitchMaxDeg.Size = new System.Drawing.Size(250, 24);
            this.lblPosePitchMaxDeg.Text = "고개 상하 허용 각도° (PosePitchMaxDeg)";
            //
            // numPosePitchMaxDeg
            //
            this.numPosePitchMaxDeg.DecimalPlaces = 1;
            this.numPosePitchMaxDeg.Increment = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPosePitchMaxDeg.Location = new System.Drawing.Point(272, 220);
            this.numPosePitchMaxDeg.Maximum = new decimal(new int[] { 90, 0, 0, 0 });
            this.numPosePitchMaxDeg.Minimum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numPosePitchMaxDeg.Size = new System.Drawing.Size(140, 23);
            this.numPosePitchMaxDeg.Value = new decimal(new int[] { 25, 0, 0, 0 });
            //
            // lblUnknownGraceSec
            //
            this.lblUnknownGraceSec.Location = new System.Drawing.Point(16, 256);
            this.lblUnknownGraceSec.Size = new System.Drawing.Size(250, 24);
            this.lblUnknownGraceSec.Text = "판정불가 유예시간 초 (UnknownGraceSec)";
            //
            // numUnknownGraceSec
            //
            this.numUnknownGraceSec.DecimalPlaces = 1;
            this.numUnknownGraceSec.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            this.numUnknownGraceSec.Location = new System.Drawing.Point(272, 254);
            this.numUnknownGraceSec.Maximum = new decimal(new int[] { 30, 0, 0, 0 });
            this.numUnknownGraceSec.Size = new System.Drawing.Size(140, 23);
            this.numUnknownGraceSec.Value = new decimal(new int[] { 30, 0, 0, 65536 });
            //
            // lblStatus
            //
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(96)))), ((int)(((byte)(102)))), ((int)(((byte)(110)))));
            this.lblStatus.Location = new System.Drawing.Point(16, 296);
            this.lblStatus.Size = new System.Drawing.Size(396, 48);
            this.lblStatus.Text = "";
            //
            // btnDefaults
            //
            this.btnDefaults.Location = new System.Drawing.Point(16, 356);
            this.btnDefaults.Size = new System.Drawing.Size(120, 30);
            this.btnDefaults.Text = "기본값으로";
            this.btnDefaults.UseVisualStyleBackColor = true;
            //
            // btnSave
            //
            this.btnSave.Location = new System.Drawing.Point(292, 356);
            this.btnSave.Size = new System.Drawing.Size(120, 30);
            this.btnSave.Text = "저장";
            this.btnSave.UseVisualStyleBackColor = true;
            //
            // SettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 410);
            this.Controls.Add(this.lblEyelid);
            this.Controls.Add(this.numEyelid);
            this.Controls.Add(this.lblLandmarkConfMin);
            this.Controls.Add(this.numLandmarkConfMin);
            this.Controls.Add(this.lblOcclusionMax);
            this.Controls.Add(this.numOcclusionMax);
            this.Controls.Add(this.lblFineOcclusionMax);
            this.Controls.Add(this.numFineOcclusionMax);
            this.Controls.Add(this.lblUseFineOcclusionGate);
            this.Controls.Add(this.chkUseFineOcclusionGate);
            this.Controls.Add(this.lblPoseYawMaxDeg);
            this.Controls.Add(this.numPoseYawMaxDeg);
            this.Controls.Add(this.lblPosePitchMaxDeg);
            this.Controls.Add(this.numPosePitchMaxDeg);
            this.Controls.Add(this.lblUnknownGraceSec);
            this.Controls.Add(this.numUnknownGraceSec);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnDefaults);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "판정 설정 (실시간 튜닝)";
            ((System.ComponentModel.ISupportInitialize)(this.numEyelid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLandmarkConfMin)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numOcclusionMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numFineOcclusionMax)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPoseYawMaxDeg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPosePitchMaxDeg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numUnknownGraceSec)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Label lblEyelid;
        private System.Windows.Forms.NumericUpDown numEyelid;
        private System.Windows.Forms.Label lblLandmarkConfMin;
        private System.Windows.Forms.NumericUpDown numLandmarkConfMin;
        private System.Windows.Forms.Label lblOcclusionMax;
        private System.Windows.Forms.NumericUpDown numOcclusionMax;
        private System.Windows.Forms.Label lblFineOcclusionMax;
        private System.Windows.Forms.NumericUpDown numFineOcclusionMax;
        private System.Windows.Forms.Label lblUseFineOcclusionGate;
        private System.Windows.Forms.CheckBox chkUseFineOcclusionGate;
        private System.Windows.Forms.Label lblPoseYawMaxDeg;
        private System.Windows.Forms.NumericUpDown numPoseYawMaxDeg;
        private System.Windows.Forms.Label lblPosePitchMaxDeg;
        private System.Windows.Forms.NumericUpDown numPosePitchMaxDeg;
        private System.Windows.Forms.Label lblUnknownGraceSec;
        private System.Windows.Forms.NumericUpDown numUnknownGraceSec;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnDefaults;
        private System.Windows.Forms.Button btnSave;
    }
}
