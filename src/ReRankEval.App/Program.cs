using Avalonia;
using ReRankEval.App;

AppBuilder.Configure<ReRankApp>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
