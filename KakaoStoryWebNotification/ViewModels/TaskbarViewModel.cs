using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KakaoStoryWebNotification.Helpers;
using System.Reflection;

namespace KakaoStoryWebNotification.ViewModels;

public partial class TaskbarViewModel : ObservableObject
{
	[ObservableProperty]
	private string _versionText = $"버전: {Assembly.GetExecutingAssembly().GetName().Version}";

	public string FavoriteFriendsNotificationsConfigurationText => CheckFavoriteFriendsNotifications ? "관심 친구 알림 받지 않기" : "관심 친구 알림 받기";

	public string EmotionsNotificationsConfigurationText => CheckEmotionNotifications ? "느낌 알림 받지 않기" : "느낌 알림 받기";

	public string StartupProcessText => IsStartupProgram ? "시스템 시작 시 자동 시작 해제" : "시스템 시작 시 자동 시작 설정";

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(FavoriteFriendsNotificationsConfigurationText))]
	private bool _checkFavoriteFriendsNotifications;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(EmotionsNotificationsConfigurationText))]
	private bool _checkEmotionNotifications;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(StartupProcessText))]
	private bool _isStartupProgram;

	public TaskbarViewModel()
	{
		CheckFavoriteFriendsNotifications = Configuration.GetValue<bool?>("CheckFavoriteFriendsNotifications") ?? true;
		CheckEmotionNotifications = Configuration.GetValue<bool?>("CheckEmotionNotifications") ?? true;
		IsStartupProgram = StartupProcessHelper.IsStartupProcess;
	}

	[RelayCommand]
	public void SetupFavoriteFriendsNotificationsConfiguration()
	{
		CheckFavoriteFriendsNotifications = !CheckFavoriteFriendsNotifications;
		Configuration.SetValue("CheckFavoriteFriendsNotifications", CheckFavoriteFriendsNotifications);
		OnPropertyChanged(nameof(FavoriteFriendsNotificationsConfigurationText));
	}

	[RelayCommand]
	public void SetupEmotionNotificationsConfiguration()
	{
		CheckEmotionNotifications = !CheckEmotionNotifications;
		Configuration.SetValue("CheckEmotionNotifications", CheckEmotionNotifications);
		OnPropertyChanged(nameof(EmotionsNotificationsConfigurationText));
	}

	[RelayCommand]
	public void SetupStartupProcess()
	{
		IsStartupProgram = !StartupProcessHelper.IsStartupProcess;
		if (IsStartupProgram) StartupProcessHelper.SetupStartupProcess();
		else StartupProcessHelper.RemoveStartupProcess();
	}

	[RelayCommand]
	public void ExitApplication() => Environment.Exit(0);
}
