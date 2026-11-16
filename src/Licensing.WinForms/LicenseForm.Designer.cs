using System.Drawing;
using System.Windows.Forms;

namespace Licensing.WinForms
{
    partial class LicenseForm
    {
        System.ComponentModel.IContainer components = null;
        TableLayoutPanel _root;
        Label _status;
        Label _guide;
        Label _clock;
        Label _machineCaption;
        TableLayoutPanel _machineRow;
        TextBox _machineId;
        Button _copyMachine;
        Label _keyCaption;
        TableLayoutPanel _keyRow;
        TextBox _licenseKey;
        Button _copyKey;
        Label _editionCaption;
        Label _edition;
        Label _featureCaption;
        Label _features;
        Label _expiresCaption;
        Label _expires;
        FlowLayoutPanel _buttons;
        Button _activate;
        Button _import;
        Button _rehost;
        Button _recheck;
        Button _done;
        Label _detail;
        Font _uiFont;
        Font _statusFont;
        Font _monoFont;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
                if (_statusFont != null)
                    _statusFont.Dispose();
                if (_monoFont != null)
                    _monoFont.Dispose();
            }

            base.Dispose(disposing);
        }

        void InitializeComponent()
        {
            this._root = new System.Windows.Forms.TableLayoutPanel();
            this._status = new System.Windows.Forms.Label();
            this._guide = new System.Windows.Forms.Label();
            this._clock = new System.Windows.Forms.Label();
            this._machineCaption = new System.Windows.Forms.Label();
            this._machineRow = new System.Windows.Forms.TableLayoutPanel();
            this._machineId = new System.Windows.Forms.TextBox();
            this._copyMachine = new System.Windows.Forms.Button();
            this._keyCaption = new System.Windows.Forms.Label();
            this._keyRow = new System.Windows.Forms.TableLayoutPanel();
            this._licenseKey = new System.Windows.Forms.TextBox();
            this._copyKey = new System.Windows.Forms.Button();
            this._editionCaption = new System.Windows.Forms.Label();
            this._edition = new System.Windows.Forms.Label();
            this._featureCaption = new System.Windows.Forms.Label();
            this._features = new System.Windows.Forms.Label();
            this._expiresCaption = new System.Windows.Forms.Label();
            this._expires = new System.Windows.Forms.Label();
            this._buttons = new System.Windows.Forms.FlowLayoutPanel();
            this._done = new System.Windows.Forms.Button();
            this._recheck = new System.Windows.Forms.Button();
            this._rehost = new System.Windows.Forms.Button();
            this._import = new System.Windows.Forms.Button();
            this._activate = new System.Windows.Forms.Button();
            this._detail = new System.Windows.Forms.Label();
            this._root.SuspendLayout();
            this._machineRow.SuspendLayout();
            this._keyRow.SuspendLayout();
            this._buttons.SuspendLayout();
            this.SuspendLayout();
            // 
            // _root
            // 
            this._root.ColumnCount = 2;
            this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 96F));
            this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.Controls.Add(this._status, 0, 0);
            this._root.Controls.Add(this._guide, 0, 1);
            this._root.Controls.Add(this._clock, 0, 2);
            this._root.Controls.Add(this._machineCaption, 0, 3);
            this._root.Controls.Add(this._machineRow, 1, 3);
            this._root.Controls.Add(this._keyCaption, 0, 4);
            this._root.Controls.Add(this._keyRow, 1, 4);
            this._root.Controls.Add(this._editionCaption, 0, 5);
            this._root.Controls.Add(this._edition, 1, 5);
            this._root.Controls.Add(this._featureCaption, 0, 6);
            this._root.Controls.Add(this._features, 1, 6);
            this._root.Controls.Add(this._expiresCaption, 0, 7);
            this._root.Controls.Add(this._expires, 1, 7);
            this._root.Controls.Add(this._buttons, 0, 9);
            this._root.Controls.Add(this._detail, 0, 10);
            this._root.Dock = System.Windows.Forms.DockStyle.Fill;
            this._root.Location = new System.Drawing.Point(16, 16);
            this._root.Name = "_root";
            this._root.RowCount = 11;
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this._root.Size = new System.Drawing.Size(608, 428);
            this._root.TabIndex = 0;
            // 
            // _status
            // 
            this._status.AutoSize = true;
            this._root.SetColumnSpan(this._status, 2);
            this._status.Dock = System.Windows.Forms.DockStyle.Fill;
            this._status.Location = new System.Drawing.Point(3, 0);
            this._status.MaximumSize = new System.Drawing.Size(590, 0);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(590, 12);
            this._status.TabIndex = 0;
            // 
            // _guide
            // 
            this._guide.AutoSize = true;
            this._root.SetColumnSpan(this._guide, 2);
            this._guide.Dock = System.Windows.Forms.DockStyle.Fill;
            this._guide.Location = new System.Drawing.Point(0, 20);
            this._guide.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._guide.MaximumSize = new System.Drawing.Size(590, 0);
            this._guide.Name = "_guide";
            this._guide.Size = new System.Drawing.Size(590, 12);
            this._guide.TabIndex = 1;
            // 
            // _clock
            // 
            this._clock.AutoSize = true;
            this._root.SetColumnSpan(this._clock, 2);
            this._clock.Dock = System.Windows.Forms.DockStyle.Fill;
            this._clock.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(138)))), ((int)(((byte)(90)))), ((int)(((byte)(0)))));
            this._clock.Location = new System.Drawing.Point(0, 40);
            this._clock.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._clock.MaximumSize = new System.Drawing.Size(590, 0);
            this._clock.Name = "_clock";
            this._clock.Size = new System.Drawing.Size(590, 12);
            this._clock.TabIndex = 2;
            // 
            // _machineCaption
            // 
            this._machineCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._machineCaption.AutoSize = true;
            this._machineCaption.Location = new System.Drawing.Point(0, 63);
            this._machineCaption.Margin = new System.Windows.Forms.Padding(0, 8, 8, 0);
            this._machineCaption.Name = "_machineCaption";
            this._machineCaption.Size = new System.Drawing.Size(65, 12);
            this._machineCaption.TabIndex = 3;
            this._machineCaption.Text = "本机机器码";
            // 
            // _machineRow
            // 
            this._machineRow.AutoSize = true;
            this._machineRow.ColumnCount = 2;
            this._machineRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._machineRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._machineRow.Controls.Add(this._machineId, 0, 0);
            this._machineRow.Controls.Add(this._copyMachine, 1, 0);
            this._machineRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._machineRow.Location = new System.Drawing.Point(96, 52);
            this._machineRow.Margin = new System.Windows.Forms.Padding(0);
            this._machineRow.Name = "_machineRow";
            this._machineRow.Size = new System.Drawing.Size(512, 27);
            this._machineRow.TabIndex = 4;
            // 
            // _machineId
            // 
            this._machineId.BackColor = System.Drawing.SystemColors.Window;
            this._machineId.Dock = System.Windows.Forms.DockStyle.Fill;
            this._machineId.Location = new System.Drawing.Point(0, 3);
            this._machineId.Margin = new System.Windows.Forms.Padding(0, 3, 8, 3);
            this._machineId.Name = "_machineId";
            this._machineId.ReadOnly = true;
            this._machineId.Size = new System.Drawing.Size(429, 21);
            this._machineId.TabIndex = 0;
            // 
            // _copyMachine
            // 
            this._copyMachine.AutoSize = true;
            this._copyMachine.Location = new System.Drawing.Point(437, 2);
            this._copyMachine.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._copyMachine.Name = "_copyMachine";
            this._copyMachine.Size = new System.Drawing.Size(75, 23);
            this._copyMachine.TabIndex = 1;
            this._copyMachine.Text = "复制";
            this._copyMachine.Click += new System.EventHandler(this.CopyMachine_Click);
            // 
            // _keyCaption
            // 
            this._keyCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._keyCaption.AutoSize = true;
            this._keyCaption.Location = new System.Drawing.Point(0, 90);
            this._keyCaption.Margin = new System.Windows.Forms.Padding(0, 8, 8, 0);
            this._keyCaption.Name = "_keyCaption";
            this._keyCaption.Size = new System.Drawing.Size(41, 12);
            this._keyCaption.TabIndex = 5;
            this._keyCaption.Text = "许可码";
            // 
            // _keyRow
            // 
            this._keyRow.AutoSize = true;
            this._keyRow.ColumnCount = 2;
            this._keyRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._keyRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this._keyRow.Controls.Add(this._licenseKey, 0, 0);
            this._keyRow.Controls.Add(this._copyKey, 1, 0);
            this._keyRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this._keyRow.Location = new System.Drawing.Point(96, 79);
            this._keyRow.Margin = new System.Windows.Forms.Padding(0);
            this._keyRow.Name = "_keyRow";
            this._keyRow.Size = new System.Drawing.Size(512, 27);
            this._keyRow.TabIndex = 6;
            // 
            // _licenseKey
            // 
            this._licenseKey.Dock = System.Windows.Forms.DockStyle.Fill;
            this._licenseKey.Location = new System.Drawing.Point(0, 3);
            this._licenseKey.Margin = new System.Windows.Forms.Padding(0, 3, 8, 3);
            this._licenseKey.Name = "_licenseKey";
            this._licenseKey.Size = new System.Drawing.Size(429, 21);
            this._licenseKey.TabIndex = 0;
            // 
            // _copyKey
            // 
            this._copyKey.AutoSize = true;
            this._copyKey.Location = new System.Drawing.Point(437, 2);
            this._copyKey.Margin = new System.Windows.Forms.Padding(0, 2, 0, 2);
            this._copyKey.Name = "_copyKey";
            this._copyKey.Size = new System.Drawing.Size(75, 23);
            this._copyKey.TabIndex = 1;
            this._copyKey.Text = "复制";
            this._copyKey.Click += new System.EventHandler(this.CopyKey_Click);
            // 
            // _editionCaption
            // 
            this._editionCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._editionCaption.AutoSize = true;
            this._editionCaption.Location = new System.Drawing.Point(0, 114);
            this._editionCaption.Margin = new System.Windows.Forms.Padding(0, 8, 8, 0);
            this._editionCaption.Name = "_editionCaption";
            this._editionCaption.Size = new System.Drawing.Size(29, 12);
            this._editionCaption.TabIndex = 7;
            this._editionCaption.Text = "版本";
            // 
            // _edition
            // 
            this._edition.AutoSize = true;
            this._edition.Location = new System.Drawing.Point(96, 114);
            this._edition.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._edition.MaximumSize = new System.Drawing.Size(470, 0);
            this._edition.Name = "_edition";
            this._edition.Size = new System.Drawing.Size(0, 12);
            this._edition.TabIndex = 8;
            // 
            // _featureCaption
            // 
            this._featureCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._featureCaption.AutoSize = true;
            this._featureCaption.Location = new System.Drawing.Point(0, 134);
            this._featureCaption.Margin = new System.Windows.Forms.Padding(0, 8, 8, 0);
            this._featureCaption.Name = "_featureCaption";
            this._featureCaption.Size = new System.Drawing.Size(29, 12);
            this._featureCaption.TabIndex = 9;
            this._featureCaption.Text = "功能";
            // 
            // _features
            // 
            this._features.AutoSize = true;
            this._features.Location = new System.Drawing.Point(96, 134);
            this._features.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._features.MaximumSize = new System.Drawing.Size(470, 0);
            this._features.Name = "_features";
            this._features.Size = new System.Drawing.Size(0, 12);
            this._features.TabIndex = 10;
            // 
            // _expiresCaption
            // 
            this._expiresCaption.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this._expiresCaption.AutoSize = true;
            this._expiresCaption.Location = new System.Drawing.Point(0, 154);
            this._expiresCaption.Margin = new System.Windows.Forms.Padding(0, 8, 8, 0);
            this._expiresCaption.Name = "_expiresCaption";
            this._expiresCaption.Size = new System.Drawing.Size(65, 12);
            this._expiresCaption.TabIndex = 11;
            this._expiresCaption.Text = "许可到期";
            // 
            // _expires
            // 
            this._expires.AutoSize = true;
            this._expires.Location = new System.Drawing.Point(96, 154);
            this._expires.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._expires.MaximumSize = new System.Drawing.Size(470, 0);
            this._expires.Name = "_expires";
            this._expires.Size = new System.Drawing.Size(0, 12);
            this._expires.TabIndex = 12;
            // 
            // _buttons
            // 
            this._buttons.AutoSize = true;
            this._root.SetColumnSpan(this._buttons, 2);
            this._buttons.Controls.Add(this._done);
            this._buttons.Controls.Add(this._recheck);
            this._buttons.Controls.Add(this._rehost);
            this._buttons.Controls.Add(this._import);
            this._buttons.Controls.Add(this._activate);
            this._buttons.Dock = System.Windows.Forms.DockStyle.Fill;
            this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._buttons.Location = new System.Drawing.Point(0, 378);
            this._buttons.Margin = new System.Windows.Forms.Padding(0, 16, 0, 0);
            this._buttons.Name = "_buttons";
            this._buttons.Size = new System.Drawing.Size(608, 30);
            this._buttons.TabIndex = 13;
            this._buttons.WrapContents = false;
            // 
            // _done
            // 
            this._done.AutoSize = true;
            this._done.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._done.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._done.Location = new System.Drawing.Point(549, 0);
            this._done.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._done.Name = "_done";
            this._done.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._done.Size = new System.Drawing.Size(59, 30);
            this._done.TabIndex = 0;
            this._done.Text = "关闭";
            this._done.Click += new System.EventHandler(this.Done_Click);
            // 
            // _recheck
            // 
            this._recheck.AutoSize = true;
            this._recheck.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._recheck.Location = new System.Drawing.Point(458, 0);
            this._recheck.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._recheck.Name = "_recheck";
            this._recheck.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._recheck.Size = new System.Drawing.Size(83, 30);
            this._recheck.TabIndex = 1;
            this._recheck.Text = "重新检查";
            this._recheck.Click += new System.EventHandler(this.Recheck_Click);
            // 
            // _rehost
            // 
            this._rehost.AutoSize = true;
            this._rehost.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._rehost.Location = new System.Drawing.Point(343, 0);
            this._rehost.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._rehost.Name = "_rehost";
            this._rehost.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._rehost.Size = new System.Drawing.Size(107, 30);
            this._rehost.TabIndex = 2;
            this._rehost.Text = "换到这台电脑";
            this._rehost.Click += new System.EventHandler(this.Rehost_Click);
            // 
            // _import
            // 
            this._import.AutoSize = true;
            this._import.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._import.Location = new System.Drawing.Point(228, 0);
            this._import.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._import.Name = "_import";
            this._import.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._import.Size = new System.Drawing.Size(107, 30);
            this._import.TabIndex = 3;
            this._import.Text = "导入授权文件";
            this._import.Click += new System.EventHandler(this.Import_Click);
            // 
            // _activate
            // 
            this._activate.AutoSize = true;
            this._activate.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._activate.Location = new System.Drawing.Point(137, 0);
            this._activate.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this._activate.Name = "_activate";
            this._activate.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._activate.Size = new System.Drawing.Size(83, 30);
            this._activate.TabIndex = 4;
            this._activate.Text = "联网激活";
            this._activate.Click += new System.EventHandler(this.Activate_Click);
            // 
            // _detail
            // 
            this._detail.AutoSize = true;
            this._root.SetColumnSpan(this._detail, 2);
            this._detail.Dock = System.Windows.Forms.DockStyle.Fill;
            this._detail.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(110)))), ((int)(((byte)(110)))));
            this._detail.Location = new System.Drawing.Point(0, 416);
            this._detail.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this._detail.Name = "_detail";
            this._detail.Size = new System.Drawing.Size(608, 12);
            this._detail.TabIndex = 14;
            // 
            // LicenseForm
            // 
            this.AcceptButton = this._activate;
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.CancelButton = this._done;
            this.ClientSize = new System.Drawing.Size(640, 460);
            this.Controls.Add(this._root);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LicenseForm";
            this.Padding = new System.Windows.Forms.Padding(16);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "授权";
            this.Load += new System.EventHandler(this.LicenseForm_Load);
            this._root.ResumeLayout(false);
            this._root.PerformLayout();
            this._machineRow.ResumeLayout(false);
            this._machineRow.PerformLayout();
            this._keyRow.ResumeLayout(false);
            this._keyRow.PerformLayout();
            this._buttons.ResumeLayout(false);
            this._buttons.PerformLayout();
            this.ResumeLayout(false);

        }
    }
}
