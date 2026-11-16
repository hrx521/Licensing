using System.Drawing;
using System.Windows.Forms;

namespace Licensing.WinForms
{
    sealed class TestShell : Form
    {
        readonly SingleMachineLicense _license;
        readonly Label _body;

        public TestShell(SingleMachineLicense license, TestSettings settings)
        {
            _license = license;
            Text = "授权测试";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 360);
            Font = CreateFont();
            Padding = new Padding(16);

            var menu = new MenuStrip();
            var licenseMenu = new ToolStripMenuItem("授权");
            var open = new ToolStripMenuItem("打开授权窗口", null, delegate { OpenLicense(); });
            var refresh = new ToolStripMenuItem("查看本机授权", null, delegate { ShowState(); });
            var exit = new ToolStripMenuItem("退出", null, delegate { Close(); });
            licenseMenu.DropDownItems.Add(open);
            licenseMenu.DropDownItems.Add(refresh);
            licenseMenu.DropDownItems.Add(new ToolStripSeparator());
            licenseMenu.DropDownItems.Add(exit);
            menu.Items.Add(licenseMenu);
            MainMenuStrip = menu;

            _body = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                TextAlign = ContentAlignment.TopLeft
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(0, 8, 0, 0)
            };
            var openButton = new Button { Text = "打开授权窗口", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
            var refreshButton = new Button { Text = "查看本机授权", AutoSize = true };
            openButton.Click += delegate { OpenLicense(); };
            refreshButton.Click += delegate { ShowState(); };
            buttons.Controls.Add(openButton);
            buttons.Controls.Add(refreshButton);

            Controls.Add(_body);
            Controls.Add(buttons);
            Controls.Add(menu);

            _body.Text = "产品：" + settings.ProductId
                + "\r\n服务器：" + settings.Server
                + "\r\n公钥：" + settings.PublicKeyFile
                + "\r\n\r\n授权已通过，测试程序已进入。";
            Load += delegate { ShowState(); };
        }

        static Font CreateFont()
        {
            try
            {
                return new Font("Microsoft YaHei UI", 9F);
            }
            catch (System.ArgumentException)
            {
                return new Font(FontFamily.GenericSansSerif, 9F);
            }
        }

        void OpenLicense()
        {
            LicenseForm.Show(this, _license);
            ShowState();
        }

        void ShowState()
        {
            var state = _license.CheckOffline();
            var text = "状态：" + (state.StatusText ?? "");
            if (!string.IsNullOrWhiteSpace(state.Edition))
                text += "\r\n版本：" + state.Edition;
            if (!string.IsNullOrWhiteSpace(state.Features))
                text += "\r\n功能：" + state.Features;
            if (!string.IsNullOrWhiteSpace(state.ExpiresText))
                text += "\r\n许可到期：" + state.ExpiresText;
            if (!string.IsNullOrWhiteSpace(state.LicenseKey))
                text += "\r\n许可码：" + state.LicenseKey;
            if (!string.IsNullOrWhiteSpace(state.MachineId))
                text += "\r\n机器码：" + state.MachineId;
            if (!string.IsNullOrWhiteSpace(state.Guide))
                text += "\r\n\r\n" + state.Guide;
            _body.Text = text;
        }
    }
}
