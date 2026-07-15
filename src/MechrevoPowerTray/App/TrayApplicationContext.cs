using System.Drawing;
using MechrevoPowerTray.Models;
using MechrevoPowerTray.Services;

namespace MechrevoPowerTray.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _lastAcceptedItem;
    private readonly ToolStripMenuItem _hardwareStatusItem;
    private readonly Dictionary<OemPowerMode, ToolStripMenuItem> _modeItems;
    private readonly ToolStripMenuItem _syncPowerPlanItem;
    private readonly ToolStripMenuItem _restoreAtStartupItem;
    private readonly ToolStripMenuItem _startupStateItem;
    private readonly ToolStripMenuItem _startupActionItem;

    private readonly OemPowerModeService _oemService;
    private readonly WindowsPowerPlanService _powerPlanService = new();
    private readonly WindowsOverlayService _overlayService = new();
    private readonly StartupTaskService _startupTaskService;
    private readonly AppSettingsStore _settingsStore = new();

    private AppSettings _settings;
    private bool _busy;
    private bool _closing;
    private OemPowerMode? _lastNotifiedMode;

    internal TrayApplicationContext()
        : this(new ProcessRunner(), new WmiOemPowerModeBackend())
    {
    }

    internal TrayApplicationContext(IProcessRunner processRunner, IOemPowerModeBackend oemBackend)
    {
        _oemService = new OemPowerModeService(oemBackend);
        _startupTaskService = new StartupTaskService(processRunner);

        _settings = _settingsStore.Load();

        _menu = new ContextMenuStrip();

        _lastAcceptedItem = new ToolStripMenuItem
        {
            Enabled = false
        };
        _menu.Items.Add(_lastAcceptedItem);

        _hardwareStatusItem = new ToolStripMenuItem("控制接口：WMI SetOemPowerSwitch")
        {
            Enabled = false
        };
        _menu.Items.Add(_hardwareStatusItem);
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

        _startupStateItem = new ToolStripMenuItem("登录自动启动：查询中…")
        {
            Enabled = false
        };
        _menu.Items.Add(_startupStateItem);

        _startupActionItem = new ToolStripMenuItem();
        _startupActionItem.Click += StartupActionItem_Click;
        _menu.Items.Add(_startupActionItem);

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

        _menu.Opening += async (_, _) =>
        {
            var state = await _startupTaskService.GetStateAsync();
            ApplyStartupState(state);
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
            _settings.LastOemRequestAcceptedMode is { } lastMode &&
            lastMode.IsWhitelisted())
        {
            _ = SwitchModeAsync(lastMode, isStartupRestore: true);
        }
    }

    internal void ApplyStartupState(StartupTaskState state)
    {
        var (stateText, actionText) = StartupTaskMenuDisplay.GetDisplay(state);
        _startupStateItem.Text = stateText;
        _startupActionItem.Visible = actionText is not null;
        if (actionText is not null)
        {
            _startupActionItem.Text = actionText;
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

            if (oemResult.Outcome == OemSwitchOutcome.Rejected)
            {
                ShowOemRejected(oemResult);
                return;
            }

            if (oemResult.Outcome == OemSwitchOutcome.Indeterminate)
            {
                ShowOemIndeterminate(oemResult);
                return;
            }

            _settings.LastOemRequestAcceptedMode = mode;
            SaveSettings();
            UpdateMenuState();

            PowerPlanSwitchResult planResult;
            if (_settings.SyncWindowsPowerPlan)
            {
                planResult = _powerPlanService.SetForMode(mode);
            }
            else
            {
                planResult = new PowerPlanSwitchResult(true, "Windows 电源计划同步已禁用。");
            }

            var overlayResult = _overlayService.SetForMode(mode);

            var combined = new CombinedModeSwitchResult(mode, oemResult, planResult, overlayResult);
            ShowSwitchResult(combined, isStartupRestore);
        }
        finally
        {
            _busy = false;
            UpdateMenuState();
        }
    }

    private void ShowSwitchResult(
        CombinedModeSwitchResult result,
        bool isStartupRestore)
    {
        if (isStartupRestore && result.AllSuccessful)
        {
            return;
        }

        if (result.AllSuccessful && result.RequestedMode == _lastNotifiedMode)
        {
            return;
        }

        if (result.AllSuccessful)
        {
            _lastNotifiedMode = result.RequestedMode;
            ShowNotification(
                "模式切换",
                $"已切换到{result.RequestedMode.DisplayName()}模式",
                ToolTipIcon.Info);
            return;
        }

        ShowNotification(
            "切换失败",
            result.CombinedMessage,
            ToolTipIcon.Warning);
    }

    private void ShowOemRejected(OemModeSwitchResult result)
    {
        ShowNotification("OEM 请求被拒绝", result.Message, ToolTipIcon.Error);

        MessageBox.Show(
            result.Message,
            "Mechrevo Power Tray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ShowOemIndeterminate(OemModeSwitchResult result)
    {
        ShowNotification(
            "OEM 请求结果未知",
            result.Message,
            ToolTipIcon.Warning);

        MessageBox.Show(
            result.Message,
            "Mechrevo Power Tray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
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

    private async void StartupActionItem_Click(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        UpdateMenuState();

        try
        {
            var state = await _startupTaskService.GetStateAsync();

            if (state == StartupTaskState.Missing)
            {
                var result = await _startupTaskService.CreateAsync();

                if (result.Success)
                {
                    ShowNotification("登录自动启动", "已启用，下次登录时生效。", ToolTipIcon.Info);
                }
                else
                {
                    ShowError($"启用自动启动失败：{result.Message}");
                }
            }
            else if (state == StartupTaskState.Present)
            {
                var result = await _startupTaskService.RemoveAsync();

                if (result.Success)
                {
                    ShowNotification("登录自动启动", "已禁用。", ToolTipIcon.Info);
                }
                else
                {
                    ShowError($"禁用自动启动失败：{result.Message}");
                }
            }
            else
            {
                ShowError("启动任务状态异常，无法执行操作。");
            }
        }
        finally
        {
            _busy = false;
            UpdateMenuState();
        }
    }

    private void UpdateMenuState()
    {
        var lastMode = _settings.LastOemRequestAcceptedMode;

        if (_busy)
        {
            _lastAcceptedItem.Text = "状态：正在切换……";
            _hardwareStatusItem.Visible = false;
        }
        else
        {
            _lastAcceptedItem.Text = lastMode is { } mode
                ? $"上次 OEM 请求已发送：{mode.DisplayName()}"
                : "上次 OEM 请求已发送：无";
            _hardwareStatusItem.Visible = true;
        }

        foreach (var pair in _modeItems)
        {
            pair.Value.Enabled = !_busy;
            pair.Value.Checked = lastMode == pair.Key;
        }

        _syncPowerPlanItem.Enabled = !_busy;
        _restoreAtStartupItem.Enabled = !_busy;
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
        var version = typeof(Program).Assembly.GetName().Version;
        var versionStr = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.2";

        MessageBox.Show(
            $"""
            Mechrevo Power Tray v{versionStr}

            MECHREVO WUJIE 16 Pro（无界 16 Pro）性能模式切换工具
            https://github.com/jnhu76/MechrevoPowerTray

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
