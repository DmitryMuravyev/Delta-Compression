using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(DeltaComp.Tests.TestAppBuilder))]

namespace DeltaComp.Tests
{
    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
