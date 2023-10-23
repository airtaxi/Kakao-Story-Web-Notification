using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.EfficiencyMode;
using KakaoStroyWebNotification.Api;
using KakaoStroyWebNotification.DataTypes;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace KakaoStroyWebNotification;

public partial class App
{
	// Constants
	private const int NotificationCheckIntervalInMilliseconds = 1000;
	private const string AccountCredentialsFileName = "account.json";

	// Fields
	private static Timer s_checkNotificationCheckTimer;
	private static string s_latestNotificationId;
	private static DateTime? s_lastNotificationTimestamp;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

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
	}


	private static void InitialzeToastActivationEvent()
	{
		ToastNotificationManagerCompat.OnActivated += OnToastActivated;
	}

	private static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
	{
		var args = ToastArguments.Parse(e.Argument);
		var url = args["url"];
		Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
	}

	private static void InitialzeApiHandlerEvent()
	{
		ApiHandler.OnReloginRequired += () =>
		{
			CheckAccountCredentials();
			var accountCredentials = JsonConvert.DeserializeObject<AccountCredentials>(File.ReadAllText(AccountCredentialsFileName));
			return LoginManager.LoginWithSelenium(accountCredentials.Email, accountCredentials.Password);
		};
	}

	private static void InitialzeTimer() => s_checkNotificationCheckTimer = new Timer(CheckNotificationTimerCallback, null, NotificationCheckIntervalInMilliseconds, Timeout.Infinite);

	private static void CheckAccountCredentials()
	{
		var fileExists = File.Exists(AccountCredentialsFileName);
		if (!fileExists)
		{
			var accountCredentials = new AccountCredentials()
			{
				Email = "이메일을 입력하세요",
				Password = "비밀번호를 입력하세요",
			};
			var jsonText = JsonConvert.SerializeObject(accountCredentials, Formatting.Indented);
			File.WriteAllText(AccountCredentialsFileName, jsonText);
			new ToastContentBuilder()
				.AddText("카카오스토리 웹 알리미")
				.AddText("설정 파일이 존재하지 않아 새로 생성했습니다.\n프로그램 폴더의 account.json 파일을 수정하신 뒤, 프로그램을 재시작해주세요.")
				.Show();
			Environment.Exit(0);
		}

		try
		{
			JsonConvert.DeserializeObject<AccountCredentials>(File.ReadAllText(AccountCredentialsFileName));
		}
		catch (JsonException)
		{
			new ToastContentBuilder()
				.AddText("카카오스토리 웹 알리미")
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

		var titleMessage = notification.message ?? "제목 없음";
		var contentMessage = notification.content ?? "내용 없음";
		var builder = new ToastContentBuilder()
		.AddText(titleMessage)
		.AddText(contentMessage);

		var thumbnailUrl = notification.thumbnail_url;

		var scheme = notification.scheme;

		if (scheme.StartsWith("kakaostory://profiles/"))
		{
			var profileId = scheme.Replace("kakaostory://profiles/", "");
			var url = $"https://story.kakao.com/" + profileId;
			builder.AddArgument("url", url);
		}
		else if (notification.scheme.StartsWith("kakaostory://activities/"))
		{
			var activityId = GetActivityIdFromNotification(notification);

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

		builder.Show();
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
