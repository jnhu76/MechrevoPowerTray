using System.Drawing;
using MechrevoPowerTray.Models;
using MechrevoPowerTray.Services;

namespace MechrevoPowerTray.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly Dictionary<OemPowerMode, ToolStripMenuItem> _modeItems;
    private readonly ToolStripMenuItem _syncPowerPlanItem;
    private readonly ToolStripMenuItem _restoreAtStartupItem;
    private readonly ToolStripMenuItem _startupItem;

    private readonly OemPowerModeService _oemService = new();
    private readonly WindowsPowerPlanService _powerPlanService = new();
    private readonly StartupTaskService _startupTaskService = new();
    private readonly AppSettingsStore _settingsStore = new();

    private AppSettings _settings;
    private bool _busy;
    private bool _closing;

    internal TrayApplicationContext()
    {
        _settings = _settingsStore.Load();

        _menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem
        {
            Enabled = false
        };
        _menu.Items.Add(_statusItem);
        _menu.Items.Add(new ToolStripSeparator());

        _modeItems = new Dictionary<OemPowerMode, ToolStripMenuItem>
        {
            [OemPowerMode.Quiet] = CreateModeItem(OemPowerMode.Quiet, "安静模式"),
            [OemPowerMode.Balanced] = CreateModeItem(OemPowerMode.Balanced, "均衡模式"),
            [OemPowerMode.Performance] = CreateModeItem(OemPowerMode.Performance, "性能模式")
        };

        foreach (var item in _modeItems.Values)
        {
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());

        _syncPowerPlanItem = new ToolStripMenuItem("同步 Windows 电源计划")
        {
            CheckOnClick = true,
            Checked = _settings.SyncWindowsPowerPlan
        };
        _syncPowerPlanItem.CheckedChanged += (_, _) =>
        {
            _settings.SyncWindowsPowerPlan = _syncPowerPlanItem.Checked;
            SaveSettings();
        };
        _menu.Items.Add(_syncPowerPlanItem);

        _restoreAtStartupItem = new ToolStripMenuItem("启动时恢复上次模式")
        {
            CheckOnClick = true,
            Checked = _settings.RestoreLastModeAtStartup
        };
        _restoreAtStartupItem.CheckedChanged += (_, _) =>
        {
            _settings.RestoreLastModeAtStartup = _restoreAtStartupItem.Checked;
            SaveSettings();
        };
        _menu.Items.Add(_restoreAtStartupItem);

        _startupItem = new ToolStripMenuItem("登录后自动启动")
        {
            CheckOnClick = true
        };
        _startupItem.Click += StartupItem_Click;
        _menu.Items.Add(_startupItem);

        _menu.Items.Add(new ToolStripSeparator());

        var diagnosticsItem = new ToolStripMenuItem("运行诊断");
        diagnosticsItem.Click += async (_, _) => await RunDiagnosticsAsync();
        _menu.Items.Add(diagnosticsItem);

        var aboutItem = new ToolStripMenuItem("关于");
        aboutItem.Click += (_, _) => ShowAbout();
        _menu.Items.Add(aboutItem);

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitApplication();
        _menu.Items.Add(exitItem);

        _menu.Opening += (_, _) =>
        {
            try
            {
                _startupItem.Checked = _startupTaskService.IsEnabled();
            }
            catch
            {
                _startupItem.Checked = false;
            }

            UpdateMenuState();
        };

        using var iconStream = typeof(Program).Assembly.GetManifestResourceStream("MechrevoPowerTray.icon.ico");

        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon(iconStream!),
            Text = "Mechrevo Power Tray",
            ContextMenuStrip = _menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += async (_, _) =>
            await SwitchModeAsync(OemPowerMode.Balanced);

        UpdateMenuState();

        if (_settings.RestoreLastModeAtStartup &&
            _settings.LastSuccessfulMode is { } lastMode &&
            lastMode.IsWhitelisted())
        {
            _ = SwitchModeAsync(lastMode, isStartupRestore: true);
        }
    }

    private ToolStripMenuItem CreateModeItem(
        OemPowerMode mode,
        string text)
    {
        var item = new ToolStripMenuItem(text)
        {
            Tag = mode
        };

        item.Click += async (_, _) => await SwitchModeAsync(mode);
        return item;
    }

    private async Task SwitchModeAsync(
        OemPowerMode mode,
        bool isStartupRestore = false)
    {
        if (_busy)
        {
            return;
        }

        if (!mode.IsWhitelisted())
        {
            ShowError($"拒绝未知模式值：{(byte)mode}。");
            return;
        }

        _busy = true;
        UpdateMenuState();

        try
        {
            var oemResult = await _oemService.SetModeAsync(mode);

            if (!oemResult.Success)
            {
                ShowError(oemResult.Message);
                return;
            }

            PowerPlanSwitchResult? planResult = null;
            if (_settings.SyncWindowsPowerPlan)
            {
                planResult = _powerPlanService.SetForMode(mode);
            }

            _settings.LastSuccessfulMode = mode;
            SaveSettings();
            UpdateMenuState();

            var title = isStartupRestore
                ? "启动模式已恢复"
                : "性能模式已切换";

            var message = oemResult.Message;

            if (planResult is { Success: false })
            {
                message += Environment.NewLine +
                           $"但 {planResult.Message}";
                ShowNotification(title, message, ToolTipIcon.Warning);
            }
            else
            {
                ShowNotification(title, message, ToolTipIcon.Info);
            }
        }
        finally
        {
            _busy = false;
            UpdateMenuState();
        }
    }

    private async Task RunDiagnosticsAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        UpdateMenuState();

        try
        {
            var result = await _oemService.DiagnoseAsync();

            MessageBox.Show(
                $"{result.Summary}{Environment.NewLine}{Environment.NewLine}{result.Details}",
                "Mechrevo Power Tray 诊断",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            UpdateMenuState();
        }
    }

    private void StartupItem_Click(object? sender, EventArgs e)
    {
        var requested = _startupItem.Checked;
        var result = _startupTaskService.SetEnabled(requested);

        if (!result.Success)
        {
            _startupItem.Checked = !requested;
            ShowError(result.Message);
            return;
        }

        ShowNotification(
            "启动设置",
            result.Message,
            ToolTipIcon.Info);
    }

    private void UpdateMenuState()
    {
        var lastMode = _settings.LastSuccessfulMode;
        _statusItem.Text = _busy
            ? "状态：正在切换……"
            : lastMode is { } mode
                ? $"上次成功请求：{mode.DisplayName()}"
                : "上次成功请求：无";

        foreach (var pair in _modeItems)
        {
            pair.Value.Enabled = !_busy;
            pair.Value.Checked = lastMode == pair.Key;
        }

        _syncPowerPlanItem.Enabled = !_busy;
        _restoreAtStartupItem.Enabled = !_busy;
        _startupItem.Enabled = !_busy;
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            ShowNotification(
                "设置保存失败",
                ex.Message,
                ToolTipIcon.Warning);
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            """
            Mechrevo Power Tray

            OEM 模式白名单：
            1 = 安静
            2 = 均衡
            3 = 性能

            程序不会调用未知数值，也不会读写 EC。
            双击托盘图标可切换到均衡模式。
            """,
            "关于",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowError(string message)
    {
        ShowNotification(
            "操作失败",
            message,
            ToolTipIcon.Error);

        MessageBox.Show(
            message,
            "Mechrevo Power Tray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ShowNotification(
        string title,
        string message,
        ToolTipIcon icon)
    {
        if (_closing)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void ExitApplication()
    {
        _closing = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_closing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
        }

        base.Dispose(disposing);
    }
}
