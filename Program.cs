Program.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Provider;

namespace SAMPLauncher
{
    [Application]
    public class MainApp : Application
    {
        public MainApp(IntPtr h, JniHandleOwnership o) : base(h, o) { }
    }

    [Activity(Label = "SAMPONLINE.NETWORK", MainLauncher = true)]
    public class MainActivity : Activity
    {
        public const string SERVER_IP = "sa-mp.vn";
        public const string SERVER_PORT = "7777";
        public const string LINK_DOWNLOAD = "https://www.mediafire.com/file/uqfli233vb9n9no/LastBrud.rar/file";

        private static readonly string[] HackKeywords = { "hack", "cheat", "injector", "modmenu", "s0beit", "moonloader", "dllinject", "aimbot", "speedhack" };

        public static readonly string RootPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
        public static readonly string GamePath = Path.Combine(RootPath, "SAMPONLINE.NETWORK");
        private const string KEY = "SAMPONLINE_NET_2026_AnToan!@#$%^&*()";
        private const string IV = "1234567890123456";
        private static readonly string WhitelistFile = Path.Combine(RootPath, "sysinfo.bin");
        private readonly string NickFile = Path.Combine(RootPath, "nickname.txt");

        private EditText _txtNick;
        private TextView _lblStatus;
        private Thread _scanThread;
        private bool _isRunning;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RequestStoragePermission();
            SetContentView(Resource.Layout.Main);

            _txtNick = FindViewById<EditText>(Resource.Id.txtNick);
            _lblStatus = FindViewById<TextView>(Resource.Id.lblStatus);
            var btnDownload = FindViewById<Button>(Resource.Id.btnDownload);
            var btnStart = FindViewById<Button>(Resource.Id.btnStart);

            if (File.Exists(NickFile)) _txtNick.Text = File.ReadAllText(NickFile).Trim();

            btnDownload.Click += (_, __) => StartActivity(new Intent(Intent.ActionView, Android.Net.Uri.Parse(LINK_DOWNLOAD)));
            btnStart.Click += async (_, __) => await StartGameAsync();
        }

        private void RequestStoragePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Android.OS.Environment.IsExternalStorageManager)
            {
                var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                intent.SetData(Android.Net.Uri.Parse($"package:{PackageName}"));
                StartActivity(intent);
                Toast.MakeText(this, "Bật quyền quản lý tệp rồi mở lại!", ToastLength.Long).Show();
                Finish();
            }
        }

        private async Task StartGameAsync()
        {
            if (string.IsNullOrWhiteSpace(_txtNick.Text))
            {
                Toast.MakeText(this, "Nhập tên nhân vật!", ToastLength.Short).Show();
                return;
            }

            _lblStatus.Text = "🔄 Kiểm tra Whitelist...";
            var (ok, msg) = await Task.Run(() => CheckWhitelist());
            _lblStatus.Text = msg;

            if (!ok) { Toast.MakeText(this, msg, ToastLength.Long).Show(); return; }

            File.WriteAllText(NickFile, _txtNick.Text.Trim());
            StartAntiCheatScan();

            var sampIntent = PackageManager.GetLaunchIntentForPackage("it.romano.sampmobile");
            if (sampIntent != null) StartActivity(sampIntent);
            else Toast.MakeText(this, $"✅ IP: {SERVER_IP}:{SERVER_PORT}", ToastLength.Long).Show();
        }

        private void StartAntiCheatScan()
        {
            _isRunning = true;
            _scanThread = new Thread(() =>
            {
                while (_isRunning)
                {
                    try
                    {
                        var am = GetSystemService(ActivityService) as ActivityManager;
                        foreach (var proc in am.RunningAppProcesses)
                        {
                            var name = proc.ProcessName.ToLowerInvariant();
                            if (HackKeywords.Any(k => name.Contains(k)))
                            {
                                RunOnUiThread(() =>
                                {
                                    Toast.MakeText(this, "⚠️ Phát hiện hack! Đóng ứng dụng.", ToastLength.Long).Show();
                                    FinishAndRemoveTask();
                                });
                                _isRunning = false;
                                return;
                            }
                        }
                    }
                    catch { }
                    Thread.Sleep(3000);
                }
            }) { IsBackground = true };
            _scanThread.Start();
        }

        public static string Sha256(string path)
        {
            try
            {
                using var sha = SHA256.Create();
                using var fs = File.OpenRead(path);
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
            catch { return ""; }
        }

        private static string Encrypt(string text)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(KEY.PadRight(32).Substring(0, 32));
            aes.IV = Encoding.UTF8.GetBytes(IV);
            return Convert.ToBase64String(aes.CreateEncryptor().TransformFinalBlock(Encoding.UTF8.GetBytes(text), 0, text.Length));
        }

        private static string Decrypt(string data)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = Encoding.UTF8.GetBytes(KEY.PadRight(32).Substring(0, 32));
                aes.IV = Encoding.UTF8.GetBytes(IV);
                return Encoding.UTF8.GetString(aes.CreateDecryptor().TransformFinalBlock(Convert.FromBase64String(data), 0, Convert.FromBase64String(data).Length));
            }
            catch { return ""; }
        }

        public static (bool Ok, string Msg) CheckWhitelist()
        {
            if (!File.Exists(WhitelistFile)) return (false, "❌ Thiếu sysinfo.bin!");
            var raw = Decrypt(File.ReadAllText(WhitelistFile));
            if (string.IsNullOrWhiteSpace(raw)) return (false, "❌ Whitelist lỗi!");

            var list = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.Contains("|"))
                .ToDictionary(l => l.Split('|')[0], l => l.Split('|')[1], StringComparer.OrdinalIgnoreCase);

            var files = Directory.GetFiles(GamePath, "*.*", SearchOption.AllDirectories)
                .Select(f => f.Substring(GamePath.Length + 1).Replace('\\', '/'))
                .Where(f => !Path.GetExtension(f).Equals(".log", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var unknown = files.Where(f => !list.ContainsKey(f)).Take(5).ToList();
            if (unknown.Any()) return (false, $"❌ Tệp lạ:\n{string.Join("\n- ", unknown)}");

            foreach (var kv in list)
            {
                var full = Path.Combine(GamePath, kv.Key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return (false, $"❌ Thiếu: {kv.Key}");
                if (Sha256(full) != kv.Value) return (false, $"❌ Sửa đổi: {kv.Key}");
            }
            return (true, "✅ Kiểm tra hoàn tất!");
        }
    }
}
