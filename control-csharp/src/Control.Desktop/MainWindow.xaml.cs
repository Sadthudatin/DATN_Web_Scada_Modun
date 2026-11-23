using System.Windows;
using System.Windows.Threading;
using Control.Domain.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace Control.Desktop;

public partial class MainWindow : Window
{
    private readonly ApiClient _api = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private HubConnection? _hub;
    private List<TagDto> _tags = new();

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += async (_, _) => await LoadAllAsync(silent: true);
        Closed += async (_, _) =>
        {
            _timer.Stop();
            await StopHubAsync();
        };
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        LoginError.Text = string.Empty;
        try
        {
            _api.BaseUrl = ApiUrlBox.Text.Trim();
            var result = await _api.LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password);
            _api.Token = result.AccessToken;
            UserText.Text = result.DisplayName;
            LoginPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            LogoutButton.Visibility = Visibility.Visible;
            await LoadAllAsync();
            await StartHubAsync();
            _timer.Start();
        }
        catch (Exception ex)
        {
            LoginError.Text = $"Đăng nhập thất bại: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private async Task LoadAllAsync(bool silent = false)
    {
        try
        {
            if (!silent) StatusText.Text = "Đang tải...";
            var gateways = await _api.GetGatewaysAsync();
            var tags = await _api.GetTagsAsync();
            var alarms = await _api.GetAlarmsAsync();
            var commands = await _api.GetCommandsAsync();

            _tags = tags;
            GatewayGrid.ItemsSource = gateways;
            TagGrid.ItemsSource = tags;
            AlarmGrid.ItemsSource = alarms;
            CommandGrid.ItemsSource = commands;
            TagCombo.ItemsSource = tags.Where(x => x.Writable).ToList();

            GatewayCountText.Text = $"{gateways.Count(x => x.Status == "online")}/{gateways.Count}";
            TagCountText.Text = tags.Count.ToString();
            AlarmCountText.Text = alarms.Count(x => x.IsActive).ToString();
            StatusText.Text = $"Cập nhật {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Lỗi tải dữ liệu";
            if (!silent) MessageBox.Show(this, ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await LoadAllAsync();

    private async void AckButton_Click(object sender, RoutedEventArgs e)
    {
        if (AlarmGrid.SelectedItem is not AlarmDto alarm) return;
        try
        {
            await _api.AckAlarmAsync(alarm.Id);
            await LoadAllAsync(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không xác nhận được alarm", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SendCommandButton_Click(object sender, RoutedEventArgs e)
    {
        if (TagCombo.SelectedItem is not TagDto tag)
        {
            MessageBox.Show(this, "Chọn tag cần ghi.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Gửi lệnh {tag.Address} = {CommandValueBox.Text}?",
            "Xác nhận điều khiển", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await _api.CreateCommandAsync(tag.Id, CommandValueBox.Text.Trim());
            await LoadAllAsync(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Không gửi được lệnh", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        await StopHubAsync();
        _api.Token = null;
        UserText.Text = string.Empty;
        LogoutButton.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        LoginError.Text = string.Empty;
    }

    private async Task StartHubAsync()
    {
        if (_hub is not null) return;

        var hub = new HubConnectionBuilder()
            .WithUrl($"{_api.BaseUrl.TrimEnd('/')}/hubs/scada", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_api.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        hub.On<object>("telemetry", _ => { _ = Dispatcher.InvokeAsync(() => _ = LoadAllAsync(true)); });
        hub.On<object>("gatewayStatus", _ => { _ = Dispatcher.InvokeAsync(() => _ = LoadAllAsync(true)); });
        hub.On<object>("alarm", _ => { _ = Dispatcher.InvokeAsync(() => _ = LoadAllAsync(true)); });
        hub.On<object>("commandStatus", _ => { _ = Dispatcher.InvokeAsync(() => _ = LoadAllAsync(true)); });

        try
        {
            await hub.StartAsync();
            _hub = hub;
            ConnectionText.Text = "● LIVE";
        }
        catch
        {
            await hub.DisposeAsync();
            ConnectionText.Text = "● POLLING";
        }
    }

    private async Task StopHubAsync()
    {
        if (_hub is null) return;
        var hub = _hub;
        _hub = null;
        try { await hub.StopAsync(); } catch { }
        await hub.DisposeAsync();
        ConnectionText.Text = "● POLLING";
    }
}
