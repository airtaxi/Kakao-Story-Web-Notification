using H.NotifyIcon;
using H.NotifyIcon.EfficiencyMode;
using KakaoStoryWebNotification.Api;
using KakaoStoryWebNotification.DataTypes;
using KakaoStoryWebNotification.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Windows.UI.Notifications;

namespace KakaoStoryWebNotification;

public partial class App
{

	// Constants
	private const int NotificationCheckIntervalInMilliseconds = 1000;
	private const string AccountCredentialsFileName = "account.json";
	private const int UpdateCheckIntervalInMinutes = 10;

	// Semi-constants
	private static readonly string AccountCredentialsFilePath = Path.Combine(AppContext.BaseDirectory, AccountCredentialsFileName);

	// Readonly static fields
	private static readonly Timer UpdateCheckTimer;
	private static readonly ToastNotifierCompat SharedToastNotifier;

	// Fields
	private static Timer s_checkNotificationCheckTimer;
	private static string s_latestNotificationId;
	private static DateTime? s_lastNotificationTimestamp;

	static App()
	{
		UpdateCheckTimer = new(UpdateCheckTimerCallback, null, (int)TimeSpan.FromMinutes(UpdateCheckIntervalInMinutes).TotalMilliseconds, Timeout.Infinite);

		// Cacahe the toast notifier to save the system memory
		SharedToastNotifier = ToastNotificationManagerCompat.CreateToastNotifier();
	}

	private const string UpdateAvailableTitle = "Update Available";
	private static async void UpdateCheckTimerCallback(object state) => await CheckForUpdateAsync();

	private static async Task CheckForUpdateAsync()
	{
		try
		{
			var url = "https://raw.githubusercontent.com/airtaxi/Kakao-Story-Web-Notification/master/latest";
			var remoteVersionString = await HttpHelper.GetContentFromUrlAsync(url);
			if (remoteVersionString is null) return;

			var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
			var remoteVersion = new Version(remoteVersionString);
			if (localVersion >= remoteVersion) return;

			var configurationKey = "versionChecked" + remoteVersionString;
			var hasNotificationShownForRemoteVersion = Configuration.GetValue<bool?>(configurationKey) ?? false;
			if (hasNotificationShownForRemoteVersion) return;
			Configuration.SetValue(configurationKey, true);

			var builder = new ToastContentBuilder()
				.AddText(UpdateAvailableTitle)
				.AddText($"새 버전 ({remoteVersion})이 발견되었습니다.\n다운로드 받으시겠습니까?")
				.AddArgument("versionString", remoteVersionString);

			builder.Show();
		}
		finally { UpdateCheckTimer.Change((int)TimeSpan.FromMinutes(UpdateCheckIntervalInMinutes).TotalMilliseconds, Timeout.Infinite); }
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		Application.Current.DispatcherUnhandledException += OnApplicationUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;

		// Initializations
		InitialzeToastActivationEvent();
		InitialzeApiHandlerEvent();
		InitialzeTimer();

		// Check if the configuration file is valid
		CheckAccountCredentials();

		// Create Icon
		var notifyIcon = (TaskbarIcon)FindResource("TaskbarIcon");
		notifyIcon.ForceCreate();

		// Set the efficiency mode
		EfficiencyModeUtilities.SetEfficiencyMode(true);

		TempMethod();
	}


    private async void TempMethod()
    {
		var result = await ApiHandler.GetFriends();

		var stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"{DateTime.Now:yyyy년 MM월 dd일 HH시 mm분 ss.fff초} 기준 카카오스토리 이상 현상 기록용 친구 목록 보고서 ({result.profiles.Count}명의 친구)");


		foreach (var friend in result.profiles)
		{
			var line = $"[{friend.display_name}] 아이디: {friend.id} / 프사 썸네일: {friend.profile_thumbnail_url}";
			stringBuilder.AppendLine(line);
		}

		stringBuilder.AppendLine("이상 보고서 작성 완료");

		Clipboard.SetText(stringBuilder.ToString());
    }

    private void OnTaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e) => WriteException(e.Exception);
	private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) => WriteException(e.ExceptionObject as Exception);
	private void OnApplicationUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		WriteException(e.Exception);
	}

	private static void WriteException(Exception exception)
	{
		var baseDirectory = AppContext.BaseDirectory;
		var path = Path.Combine(baseDirectory, "error.log");

		if (exception is null)
		{
			File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UNKNOWN\n({baseDirectory})\n\n");
			return;
		}

		var exceptionName = exception.GetType().Name;

		var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ({exceptionName}) {exception?.Message ?? "UNKNOWN"}: {exception?.StackTrace ?? "UNKNOWN"}\n({baseDirectory})\n\n";
		File.AppendAllText(path, text);

		if (exception.InnerException is not null) WriteException(exception.InnerException);
	}


	private static void InitialzeToastActivationEvent()
	{
		ToastNotificationManagerCompat.OnActivated += OnToastActivated;
	}

	private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
	{
		var args = ToastArguments.Parse(e.Argument);

		if(args.Any(x => x.Key == "url")) // Kakao Story Toast Notification
		{
			var url = args["url"];
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		else if(args.Any(x => x.Key == "versionString")) // Program Update Toast Notification
		{
			var versionString = args["versionString"];
			var url = "https://github.com/airtaxi/Kakao-Story-Web-Notification/releases/tag/" + versionString;
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
	}

	private static void InitialzeApiHandlerEvent()
	{
		ApiHandler.OnReloginRequired += () =>
		{
			if (LoginManager.IsInLogin) return false;
			CheckAccountCredentials();
			var accountCredentials = JsonConvert.DeserializeObject<AccountCredentials>(File.ReadAllText(AccountCredentialsFilePath));
			return LoginManager.LoginWithSelenium(accountCredentials.Email, accountCredentials.Password);
		};
	}

	private static void InitialzeTimer() => s_checkNotificationCheckTimer = new Timer(CheckNotificationTimerCallback, null, NotificationCheckIntervalInMilliseconds, Timeout.Infinite);

	private static void CheckAccountCredentials()
	{
		var fileExists = File.Exists(AccountCredentialsFilePath);
		if (!fileExists)
		{
			var accountCredentials = new AccountCredentials()
			{
				Email = "이메일을 입력하세요",
				Password = "비밀번호를 입력하세요",
			};
			var jsonText = JsonConvert.SerializeObject(accountCredentials, Formatting.Indented);
			File.WriteAllText(AccountCredentialsFilePath, jsonText);
			new ToastContentBuilder()
				.AddText("설정 파일이 존재하지 않아 새로 생성했습니다.\n프로그램 폴더의 account.json 파일을 수정하신 뒤, 프로그램을 재시작해주세요.")
				.Show();
			Environment.Exit(0);
		}

		try
		{
			JsonConvert.DeserializeObject<AccountCredentials>(File.ReadAllText(AccountCredentialsFilePath));
		}
		catch (JsonException)
		{
			new ToastContentBuilder()
				.AddText("설정 파일이 손상되었습니다.\n프로그램 폴더의 account.json 파일을 수정하신 뒤, 프로그램을 재시작해주세요.")
				.Show();
			Environment.Exit(0);
		}

	}

	private static void CheckNotificationTimerCallback(object state)
	{
		Task.Run(async () =>
		{
			try
			{
				var status = await ApiHandler.GetNotificationStatus();
				if ((status?.NotificationCount ?? 0) == 0 && s_latestNotificationId != null)
					return;

				var notifications = await ApiHandler.GetNotifications();

				var first = notifications.FirstOrDefault();
				try
				{
					for (int i = 0; i < notifications.Count; i++)
					{
						ApiHandler.DataType.Notification notification = notifications[i];
						if (s_lastNotificationTimestamp != null && notification?.created_at > s_lastNotificationTimestamp && notification.is_new)
							await ShowNotificationToastAsync(notification);
						else break;
					}
				}
				finally
				{
					s_latestNotificationId = first.id;
					s_lastNotificationTimestamp = first?.created_at;
				}

			}
			finally { s_checkNotificationCheckTimer.Change(NotificationCheckIntervalInMilliseconds, Timeout.Infinite); }
		});
	}

	private static async Task ShowNotificationToastAsync(ApiHandler.DataType.Notification notification)
	{
		var willShow = true;

		// Get the configuration
		var checkEmotionNotifications = Configuration.GetValue<bool?>("CheckEmotionNotifications") ?? true;
		var checkFavoriteFriendsNotifications = Configuration.GetValue<bool?>("CheckFavoriteFriendsNotifications") ?? true;

		// Check if the notification should be shown
		if (!checkEmotionNotifications && notification.emotion != null) willShow = false;
		else if (!checkFavoriteFriendsNotifications && (notification.decorators?.FirstOrDefault()?.text?.StartsWith("관심친구") ?? false)) willShow = false;

		// If we don't need to show the notification, return immediately to prevent run the code below
		if (!willShow) return;

		var builder = new ToastContentBuilder();

		if (notification.message != null) builder.AddText(notification.message);
		if (notification.content != null) builder.AddText(notification.content);

		var thumbnailUrl = notification.thumbnail_url;

		var scheme = notification.scheme;
		string tag = null;

		if (scheme.StartsWith("kakaostory://profiles/"))
		{
			var profileId = scheme.Replace("kakaostory://profiles/", "");
			var url = $"https://story.kakao.com/" + profileId;
			builder.AddArgument("url", url);
		}
		else if (notification.scheme.StartsWith("kakaostory://activities/"))
		{
			var activityId = GetActivityIdFromNotification(notification);
			tag = activityId;

			if (string.IsNullOrEmpty(thumbnailUrl))
			{
				var post = await ApiHandler.GetPost(activityId);
				var mediaCount = post?.media?.Count ?? 0;
				var mediaType = post?.media_type;
				if (mediaCount > 0 && mediaType != "video")
					thumbnailUrl = post?.media[0]?.origin_url ?? thumbnailUrl;
			}

			var url = $"https://story.kakao.com/" + activityId.Replace('.', '/');
			builder.AddArgument("url", url);
		}

		if (!string.IsNullOrEmpty(thumbnailUrl))
			builder.AddHeroImage(new Uri(thumbnailUrl));

		// Remove the previous notification
		ToastNotificationManagerCompat.History.Remove(scheme);

		// Manually generate the toast notification to set the tag
		var toast = new ToastNotification(builder.GetToastContent().GetXml());
		if (tag != null) toast.Tag = tag; // Set the tag if it exists

		// Show the toast notification
		SharedToastNotifier.Show(toast);
	}

	private static string GetActivityIdFromNotification(ApiHandler.DataType.Notification notification)
	{
		var scheme = notification.scheme;
		if (!scheme.StartsWith("kakaostory://activities/")) return null;

		var activityId = scheme.Replace("kakaostory://activities/", "");
		if (activityId.Contains("?profile_id="))
			activityId = activityId.Split("?profile_id=")[0];

		return activityId;
	}
}
