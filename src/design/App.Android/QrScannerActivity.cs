using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using Camera = Android.Hardware.Camera;

namespace App.Android;

#pragma warning disable CS0618 // Legacy Camera API remains supported and avoids a large CameraX dependency tree.

[Activity(
    Label = "Scan Bitcoin QR",
    Theme = "@style/MyTheme.Scanner",
    ScreenOrientation = ScreenOrientation.Portrait,
    Exported = false)]
public sealed class QrScannerActivity : Activity, ISurfaceHolderCallback, Camera.IPreviewCallback
{
    public const string RequestIdExtra = "request-id";
    private const int CameraPermissionRequest = 7001;

    // Angor design tokens (dark variant)
    private static readonly Color AppBackground = Color.ParseColor("#0A0A0A");
    private static readonly Color CardSurface = Color.ParseColor("#262626");
    private static readonly Color BorderStroke = Color.ParseColor("#404040");
    private static readonly Color TextStrong = Color.ParseColor("#FAFAFA");
    private static readonly Color TextMuted = Color.ParseColor("#A0A0A0");
    private static readonly Color BrandGreen = Color.ParseColor("#5FAF78");

    private const string LogTag = "AngorQr";

    private readonly QRCodeReader reader = new();
    private SurfaceView? preview;
    private LinearLayout? scanFrameContainer;
    private Camera? camera;
    private string? requestId;
    private bool completed;
    private bool previewStarted;
    private bool decoding;
    private bool surfaceReady;
    private bool cameraReady;
    private long lastDecodeTicks;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        requestId = Intent?.GetStringExtra(RequestIdExtra);
        BuildUi();

        if (CheckSelfPermission(Manifest.Permission.Camera) == Permission.Granted)
            PrepareCamera();
        else
            RequestPermissions([Manifest.Permission.Camera], CameraPermissionRequest);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != CameraPermissionRequest)
            return;

        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
            PrepareCamera();
        else
            FinishScan(null);
    }

    /// <summary>
    /// Opens and configures the camera up-front so the preview surface can be sized
    /// to the selected resolution's aspect ratio BEFORE its surface is created —
    /// that is what prevents the stretched/skewed preview. Preview start is gated
    /// on BOTH the camera being ready AND the surface being ready: whichever
    /// arrives last triggers <see cref="TryStartPreview"/> (fixes black screen on
    /// first launch when SurfaceCreated fires before/without our callback).
    /// </summary>
    private void PrepareCamera()
    {
        try
        {
            OpenAndConfigureCamera();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(LogTag, $"Camera init failed: {ex}");
            FinishScan(null);
            return;
        }

        // The surface may already exist by the time we add the callback (or the
        // callback may never fire again) — detect a live surface immediately.
        if (preview!.Holder!.Surface?.IsValid == true)
            surfaceReady = true;

        preview.Holder.AddCallback(this);
        TryStartPreview();
    }

    private void OpenAndConfigureCamera()
    {
        camera = Camera.Open();
        camera.SetDisplayOrientation(90);

        Camera.Parameters parameters = camera.GetParameters()!;
        IList<string>? focusModes = parameters.SupportedFocusModes;
        if (focusModes?.Contains(Camera.Parameters.FocusModeContinuousPicture) == true)
            parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;

        if (parameters.SupportedPreviewFormats?.Any(
                format => format.IntValue() == (int)ImageFormatType.Nv21) == true)
            parameters.PreviewFormat = ImageFormatType.Nv21;

        Camera.Size previewSize = ChoosePreviewSize(parameters);
        parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
        camera.SetParameters(parameters);

        // Preview frames are landscape; with a 90° display rotation the on-screen
        // dimensions are swapped. Size the surface as a centered "cover" crop so
        // no stretching occurs.
        DisplayMetrics metrics = Resources!.DisplayMetrics!;
        float displayedWidth = previewSize.Height;
        float displayedHeight = previewSize.Width;
        float scale = Math.Max(metrics.WidthPixels / displayedWidth, metrics.HeightPixels / displayedHeight);

        preview!.LayoutParameters = new FrameLayout.LayoutParams(
            (int)(displayedWidth * scale), (int)(displayedHeight * scale))
        {
            Gravity = GravityFlags.Center,
        };

        cameraReady = true;
    }

    private void TryStartPreview()
    {
        if (completed || previewStarted || !cameraReady || !surfaceReady || camera == null || preview?.Holder == null)
            return;

        try
        {
            camera.SetPreviewDisplay(preview.Holder);
            camera.SetPreviewCallback(this);
            camera.StartPreview();
            previewStarted = true;
            global::Android.Util.Log.Info(LogTag, "Preview started");
        }
        catch (Exception ex)
        {
            // Not fatal — SurfaceChanged will call TryStartPreview again.
            global::Android.Util.Log.Warn(LogTag, $"Preview start deferred: {ex.Message}");
        }
    }

    private Camera.Size ChoosePreviewSize(Camera.Parameters parameters)
    {
        DisplayMetrics metrics = Resources!.DisplayMetrics!;
        double screenAspect = (double)Math.Max(metrics.WidthPixels, metrics.HeightPixels) /
                              Math.Max(1, Math.Min(metrics.WidthPixels, metrics.HeightPixels));

        List<Camera.Size> supported = (parameters.SupportedPreviewSizes ?? []).ToList();
        List<Camera.Size> bounded = supported
            .Where(size => (long)size.Width * size.Height <= 1280L * 720)
            .ToList();
        IEnumerable<Camera.Size> candidates = bounded.Count > 0 ? bounded : supported;

        Camera.Size? best = null;
        double bestAspectDiff = double.MaxValue;
        foreach (Camera.Size candidate in candidates)
        {
            double displayedAspect = (double)Math.Max(candidate.Width, candidate.Height) /
                                     Math.Max(1, Math.Min(candidate.Width, candidate.Height));
            double diff = Math.Abs(displayedAspect - screenAspect);
            bool higherResolution = best == null ||
                (long)candidate.Width * candidate.Height > (long)best.Width * best.Height;

            if (diff < bestAspectDiff - 0.01 || (diff <= bestAspectDiff + 0.01 && higherResolution))
            {
                best = candidate;
                bestAspectDiff = diff;
            }
        }

        return best ?? parameters.PreviewSize;
    }

    private void BuildUi()
    {
        var root = new FrameLayout(this)
        {
            Background = new ColorDrawable(AppBackground),
        };

        preview = new SurfaceView(this);
        root.AddView(preview, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        // Centered scan frame + status hint
        int frameSide = (int)(Math.Min(Resources!.DisplayMetrics!.WidthPixels,
            Resources.DisplayMetrics.HeightPixels) * 0.68f);

        scanFrameContainer = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        scanFrameContainer.SetGravity(GravityFlags.CenterHorizontal);

        // Use a standard Android View + drawable instead of a custom JNI-drawn
        // View. Private nested native View subclasses can fail activation on
        // trimmed/AOT Android builds even though Debug compilation succeeds.
        var scanFrame = new View(this);
        var frameBackground = new GradientDrawable();
        frameBackground.SetColor(Color.Argb(60, 0, 0, 0));
        frameBackground.SetStroke((int)Dp(3), BrandGreen);
        frameBackground.SetCornerRadius(Dp(24));
        scanFrame.Background = frameBackground;
        scanFrameContainer.AddView(scanFrame, new LinearLayout.LayoutParams(frameSide, frameSide));

        var status = new TextView(this)
        {
            Text = "Align the QR code inside the frame",
            TextSize = 13,
            Gravity = GravityFlags.Center,
        };
        status.SetTextColor(TextMuted);
        scanFrameContainer.AddView(status, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = (int)Dp(20),
        });

        root.AddView(scanFrameContainer, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Center,
        });

        // Header: title + subtitle
        var header = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        header.SetPadding((int)Dp(24), (int)Dp(40), (int)Dp(24), (int)Dp(16));

        var title = new TextView(this)
        {
            Text = "Scan Bitcoin QR code",
            TextSize = 18,
            Gravity = GravityFlags.Center,
        };
        title.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        title.SetTextColor(TextStrong);
        header.AddView(title, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var hint = new TextView(this)
        {
            Text = "Supports an on-chain address or bitcoin: payment request",
            TextSize = 13,
            Gravity = GravityFlags.Center,
        };
        hint.SetTextColor(TextMuted);
        header.AddView(hint, new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            TopMargin = (int)Dp(6),
        });

        root.AddView(header, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.Top,
        });

        // Cancel pinned to the bottom, styled like app secondary buttons
        var cancel = new Button(this)
        {
            Text = "Cancel",
            TransformationMethod = null,
            StateListAnimator = null!,
        };
        cancel.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        cancel.SetTextColor(TextStrong);
        cancel.TextSize = 15;
        cancel.SetMinimumHeight(0);
        cancel.SetMinimumWidth(0);
        var cancelBackground = new GradientDrawable();
        cancelBackground.SetColor(CardSurface);
        cancelBackground.SetStroke((int)Dp(1), BorderStroke);
        cancelBackground.SetCornerRadius(Dp(12));
        cancel.Background = cancelBackground;
        root.AddView(cancel, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, (int)Dp(52))
        {
            Gravity = GravityFlags.Bottom,
            LeftMargin = (int)Dp(16),
            RightMargin = (int)Dp(16),
            BottomMargin = (int)Dp(16),
        });
        cancel.Click += (_, _) => FinishScan(null);

        SetContentView(root);
    }

    private float Dp(float value) => value * Resources!.DisplayMetrics!.Density;

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        surfaceReady = true;
        try
        {
            if (camera == null && !completed)
                OpenAndConfigureCamera();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error(LogTag, $"Camera reopen failed: {ex}");
            FinishScan(null);
            return;
        }

        TryStartPreview();
    }

    public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
    {
        surfaceReady = true;
        TryStartPreview();
    }

    public void SurfaceDestroyed(ISurfaceHolder holder)
    {
        surfaceReady = false;
        StopCamera();
    }

    public void OnPreviewFrame(byte[]? data, Camera? source)
    {
        if (completed || decoding || data == null || source == null)
            return;

        // Decoding full camera frames on every callback can overwhelm mobile
        // CPUs and the GC. Five scans/second is responsive and bounded.
        long now = DateTime.UtcNow.Ticks;
        if (now - lastDecodeTicks < TimeSpan.FromMilliseconds(200).Ticks)
            return;
        lastDecodeTicks = now;
        decoding = true;

        try
        {
            Camera.Size? size = source.GetParameters()?.PreviewSize;
            if (size == null || data.Length < size.Width * size.Height)
                return;

            string? decoded = Decode(data, size.Width, size.Height);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                string sanitized = Sanitize(decoded);
                if (sanitized.Length > 0)
                    RunOnUiThread(() => FinishScan(sanitized));
            }
        }
        catch (Exception ex)
        {
            // Never let an exception cross the native Camera callback boundary;
            // Android terminates the process when that happens.
            global::Android.Util.Log.Warn(LogTag, $"Frame decode failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            decoding = false;
        }
    }

    private string? Decode(byte[] data, int width, int height)
    {
        try
        {
            var source = new PlanarYUVLuminanceSource(data, width, height, 0, 0, width, height, false);
            return reader.decode(new BinaryBitmap(new HybridBinarizer(source)))?.Text;
        }
        catch (Exception ex) when (ex is ReaderException or ArgumentException or IndexOutOfRangeException)
        {
            reader.reset();
            return null;
        }
    }

    /// <summary>Strips control/format characters some wallet apps embed in QR payloads.</summary>
    internal static string Sanitize(string value)
    {
        return new string(value
            .Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format)
            .ToArray()).Trim();
    }

    public override void OnBackPressed()
    {
        FinishScan(null);
    }

    protected override void OnDestroy()
    {
        StopCamera();
        if (!completed)
            AndroidQrCodeScanner.Complete(requestId, null);
        base.OnDestroy();
    }

    private void FinishScan(string? value)
    {
        if (completed)
            return;

        completed = true;
        StopCamera();
        if (value != null)
            global::Android.Util.Log.Info(LogTag, $"Decoded {value.Length} chars, starts '{value[..Math.Min(12, value.Length)]}'");
        AndroidQrCodeScanner.Complete(requestId, value);
        Finish();
    }

    private void StopCamera()
    {
        Camera? active = camera;
        camera = null;
        cameraReady = false;
        previewStarted = false;
        if (active == null)
            return;

        try { active.SetPreviewCallback(null); } catch { }
        if (previewStarted)
        {
            try { active.StopPreview(); } catch { }
        }
        try { active.Release(); } catch { }
        active.Dispose();
    }
}

#pragma warning restore CS0618
