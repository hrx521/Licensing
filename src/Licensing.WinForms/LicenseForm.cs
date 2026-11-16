using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Licensing.WinForms
{
    public partial class LicenseForm : Form
    {
        readonly SingleMachineLicense _license;
        readonly bool _startup;
        bool _busy;

        public LicenseForm()
        {
            InitializeComponent();
            ApplyFonts();
        }

        public LicenseForm(SingleMachineLicense license, bool startup)
            : this()
        {
            if (license == null)
                throw new ArgumentNullException("license");

            _license = license;
            _startup = startup;
        }

        public static bool Ensure(IWin32Window owner, SingleMachineLicense license)
        {
            if (license == null)
                throw new ArgumentNullException("license");

            if (license.CheckOffline().CanEnter)
                return true;

            using (var form = new LicenseForm(license, true))
                return form.ShowDialog(owner) == DialogResult.OK;
        }

        public static void Show(IWin32Window owner, SingleMachineLicense license)
        {
            if (license == null)
                throw new ArgumentNullException("license");

            using (var form = new LicenseForm(license, false))
                form.ShowDialog(owner);
        }

        void LicenseForm_Load(object sender, EventArgs e)
        {
            if (_license == null)
                return;
            Apply(_license.CheckOffline());
        }

        async void Activate_Click(object sender, EventArgs e)
        {
            if (_license == null)
                return;
            await Run(delegate { return _license.ActivateAsync(_licenseKey.Text); });
        }

        async void Rehost_Click(object sender, EventArgs e)
        {
            if (_license == null)
                return;
            await Run(delegate { return _license.RehostAsync(_licenseKey.Text); });
        }

        async void Recheck_Click(object sender, EventArgs e)
        {
            if (_license == null)
                return;
            await Run(delegate { return _license.RecheckAsync(_licenseKey.Text); });
        }

        void CopyMachine_Click(object sender, EventArgs e)
        {
            Copy(_machineId.Text);
        }

        void CopyKey_Click(object sender, EventArgs e)
        {
            Copy(_licenseKey.Text);
        }

        void Done_Click(object sender, EventArgs e)
        {
            Close();
        }

        async void Import_Click(object sender, EventArgs e)
        {
            if (_busy || _license == null)
                return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "选择授权文件";
                dialog.Filter = "授权文件 (*.json)|*.json|所有文件 (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                string json;
                try
                {
                    json = File.ReadAllText(dialog.FileName);
                }
                catch (Exception ex)
                {
                    Apply(LicensePresenter.Failed(_license.MachineId, "读不到这个文件。" + ex.Message));
                    return;
                }

                await Run(delegate { return System.Threading.Tasks.Task.FromResult(_license.Import(json)); });
            }
        }

        async System.Threading.Tasks.Task Run(Func<System.Threading.Tasks.Task<LicenseFormState>> action)
        {
            if (_busy)
                return;

            _busy = true;
            UseWaitCursor = true;
            ToggleActions(false);
            var previous = _status.Text;
            _status.Text = "正在联系授权服务器…";
            _status.ForeColor = Color.FromArgb(28, 28, 28);
            try
            {
                Apply(await action());
            }
            catch (Exception ex)
            {
                _status.Text = previous;
                Apply(LicensePresenter.Failed(_license.MachineId, ex.Message));
            }
            finally
            {
                _busy = false;
                UseWaitCursor = false;
            }
        }

        void Apply(LicenseFormState state)
        {
            if (state == null)
                return;

            _status.Text = state.StatusText ?? "";
            _status.ForeColor = ToneColor(state.Tone);
            _guide.Text = state.Guide ?? "";
            _clock.Text = state.ClockNote ?? "";
            _clock.Visible = _clock.Text.Length > 0;
            _machineId.Text = state.MachineId ?? "";
            _licenseKey.Text = state.LicenseKey ?? "";
            _licenseKey.ReadOnly = state.KeyReadOnly;
            SetRow(_editionCaption, _edition, state.Edition);
            SetRow(_featureCaption, _features, state.Features);
            SetRow(_expiresCaption, _expires, state.ExpiresText);
            _detail.Text = state.Detail ?? "";
            _activate.Visible = state.ShowActivate;
            _activate.Enabled = true;
            _import.Visible = state.ShowImport;
            _import.Enabled = true;
            _rehost.Visible = state.ShowRehost;
            _rehost.Enabled = state.RehostEnabled;
            _recheck.Visible = state.ShowRecheck;
            _recheck.Enabled = true;
            _done.Enabled = true;
            _copyMachine.Enabled = _machineId.Text.Length > 0;
            _copyKey.Enabled = _licenseKey.Text.Length > 0;

            if (_startup && state.CanEnter)
            {
                _done.Text = "进入程序";
                _done.DialogResult = DialogResult.OK;
                AcceptButton = _done;
            }
            else
            {
                _done.Text = "关闭";
                _done.DialogResult = DialogResult.Cancel;
                AcceptButton = _activate.Visible ? _activate : _done;
            }
        }

        void ApplyFonts()
        {
            _uiFont = CreateUiFont();
            _statusFont = new Font(_uiFont.FontFamily, 14F, FontStyle.Bold);
            _monoFont = new Font(FontFamily.GenericMonospace, 10F);
            Font = _uiFont;
            _status.Font = _statusFont;
            _machineId.Font = _monoFont;
            _licenseKey.Font = _monoFont;
        }

        void ToggleActions(bool enabled)
        {
            _activate.Enabled = enabled;
            _import.Enabled = enabled;
            _rehost.Enabled = enabled;
            _recheck.Enabled = enabled;
            _done.Enabled = enabled;
        }

        static void SetRow(Label caption, Label value, string text)
        {
            var shown = !string.IsNullOrWhiteSpace(text);
            caption.Visible = shown;
            value.Visible = shown;
            value.Text = shown ? text : "";
        }

        static Color ToneColor(LicenseTone tone)
        {
            switch (tone)
            {
                case LicenseTone.Ok:
                    return Color.FromArgb(13, 107, 50);
                case LicenseTone.Warn:
                    return Color.FromArgb(138, 90, 0);
                case LicenseTone.Bad:
                    return Color.FromArgb(155, 28, 28);
                default:
                    return Color.FromArgb(28, 28, 28);
            }
        }

        static void Copy(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;
            Clipboard.SetText(text.Trim());
        }

        static Font CreateUiFont()
        {
            try
            {
                return new Font("Microsoft YaHei UI", 9F);
            }
            catch (ArgumentException)
            {
                return new Font(FontFamily.GenericSansSerif, 9F);
            }
        }
    }
}
